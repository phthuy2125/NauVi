"""
Script đánh giá mô hình nhận diện món ăn Việt – Câu hỏi nghiên cứu CH1
So sánh mô hình chung (baseline Gemini) vs mô hình tinh chỉnh trên 30 lớp Việt.

Chạy (chế độ mẫu, chưa có ảnh):
    python evaluate.py

Chạy (có ảnh thật):
    python evaluate.py --test_dir data/test --train_dir data/train --api_key YOUR_KEY

Đầu ra:
    - evaluation_results.csv    (kết quả thật hoặc đánh dấu SAMPLE)
    - confusion_matrix.png      (nếu có ảnh thật)
    - Bảng in ra terminal để paste vào báo cáo
"""

import os
import json
import csv
import random
import hashlib
import argparse
from pathlib import Path
from datetime import datetime

# ĐẶT SEED CỐ ĐỊNH – đảm bảo chạy lại được cùng kết quả
random.seed(42)

# 30 lớp món ăn Việt (tên slug tương ứng với tên thư mục ảnh)
VIETNAMESE_CLASSES = [
    "bahn_mi", "bun_bo_hue", "bun_cha", "ca_kho_to", "canh_chua",
    "cha_gio", "che_ba_mau", "chao_ga", "com_rang", "hu_tieu",
    "bun_dau_mam_tom", "banh_xeo", "banh_cuon", "banh_beo", "bun_thit_nuong",
    "oc_len_xao_dua", "mi_quang", "pho_bo", "bun_rieu", "xoi_xeo",
    "goi_cuon", "banh_mi_thit", "com_tam", "banh_khot", "lau_thai",
    "nem_cuon", "thit_kho_tau", "ca_hap", "ga_nuong", "rau_muong_xao",
]


# =============================================================================
# KIỂM TRA RÒ RỈ DỮ LIỆU (data leakage)
# =============================================================================

def hash_file(path: Path) -> str:
    """Tính MD5 hash của file để phát hiện ảnh trùng."""
    with open(path, "rb") as f:
        return hashlib.md5(f.read()).hexdigest()


def check_data_leakage(train_dir: Path, test_dir: Path) -> list:
    """
    Kiểm tra rò rỉ dữ liệu: ảnh trùng hoặc cùng file ở cả train và test.
    Trả về danh sách các cặp (test_path, train_path) bị rò rỉ.
    """
    print("\n🔍 Kiểm tra rò rỉ dữ liệu (so sánh MD5 hash)...")

    IMG_EXTS = {".jpg", ".jpeg", ".png", ".webp"}

    # Lập chỉ mục hash của tập train
    train_hashes: dict[str, Path] = {}
    for f in train_dir.rglob("*"):
        if f.suffix.lower() in IMG_EXTS:
            h = hash_file(f)
            train_hashes[h] = f

    # So sánh với tập test
    leaks = []
    for f in test_dir.rglob("*"):
        if f.suffix.lower() in IMG_EXTS:
            h = hash_file(f)
            if h in train_hashes:
                leaks.append((f, train_hashes[h]))

    if leaks:
        print(f"  ⚠️  Phát hiện {len(leaks)} ảnh rò rỉ dữ liệu (trùng hash train/test)!")
        for test_f, train_f in leaks[:5]:
            print(f"     test : {test_f}")
            print(f"     train: {train_f}")
        if len(leaks) > 5:
            print(f"     ... và {len(leaks) - 5} cặp khác.")
        print("  ➡️  Hãy xóa các ảnh trùng khỏi tập test trước khi đánh giá!")
    else:
        print("  ✅ Không phát hiện rò rỉ dữ liệu.")

    return leaks


# =============================================================================
# ĐÁNH GIÁ MẪU (khi chưa có ảnh thật)
# =============================================================================

def run_sample_evaluation() -> dict:
    """
    ⚠️ DỮ LIỆU MẪU – chạy khi chưa có đủ ảnh thật.
    Dùng kết quả giả để kiểm tra format output và pipeline.
    KHÔNG dùng số liệu này trong báo cáo nghiên cứu.
    """
    print("\n" + "=" * 65)
    print("⚠️  CHẾ ĐỘ DỮ LIỆU MẪU")
    print("    Kết quả dưới đây là GIẢ, chỉ để kiểm tra format pipeline.")
    print("    Thay thế bằng kết quả thật khi có đủ ảnh 30 lớp.")
    print("=" * 65)

    # Số liệu giả – không có ý nghĩa khoa học
    results = {
        "baseline": {
            "top1": 0.312,
            "top3": 0.558,
            "note": "DỮ LIỆU MẪU – Gemini 1.5 Flash không tinh chỉnh",
        },
        "finetuned": {
            "top1": 0.721,
            "top3": 0.887,
            "note": "DỮ LIỆU MẪU – Gemini prompt-tuned trên 30 lớp Việt",
        },
    }
    delta_top3 = results["finetuned"]["top3"] - results["baseline"]["top3"]

    _print_table(results, delta_top3)

    # Xuất CSV với nhãn rõ ràng
    out_csv = "evaluation_results_SAMPLE.csv"
    _export_csv(results, delta_top3, out_csv, suffix="⚠️ DỮ LIỆU MẪU")

    print(f"\n✅ Đã xuất {out_csv}")
    print("   Thư mục hiện tại:", Path.cwd())

    # Mô phỏng danh sách lớp hay bị nhầm
    print("\n📊 Lớp hay bị nhầm (DỮ LIỆU MẪU):")
    sample_confused = [
        ("pho_bo", "bun_bo_hue", 8),
        ("cha_gio", "nem_cuon", 6),
        ("com_rang", "com_tam", 5),
    ]
    for true_cls, pred_cls, count in sample_confused:
        print(f"   {true_cls:25} → nhầm thành {pred_cls:25} ({count} lần)")

    return results, delta_top3


def _print_table(results: dict, delta_top3: float):
    """In bảng kết quả dạng dễ paste vào báo cáo."""
    print("\n" + "=" * 55)
    print(f"{'Mô hình':<25} {'Top-1':>8} {'Top-3':>8}")
    print("-" * 55)
    b = results["baseline"]
    f = results["finetuned"]
    print(f"{'Chung (baseline)':<25} {b['top1']:>8.1%} {b['top3']:>8.1%}")
    print(f"{'Tinh chỉnh (finetuned)':<25} {f['top1']:>8.1%} {f['top3']:>8.1%}")
    print(f"{'ΔTop-3 (tinh chỉnh−chung)':<25} {'':>8} {delta_top3:>+8.1%}")
    print("=" * 55)

    h1_pass = f["top3"] >= 0.85 and delta_top3 >= 0.20
    if h1_pass:
        print("✅ Giả thuyết H1 ĐẠT: Top-3 ≥ 85% VÀ ΔTop-3 ≥ 20 điểm %")
    else:
        print(f"❌ Giả thuyết H1 CHƯA ĐẠT: Top-3={f['top3']:.1%}, ΔTop-3={delta_top3:+.1%}")
        print("   Báo số thật, không làm đẹp kết quả.")


def _export_csv(results: dict, delta_top3: float, out_path: str, suffix: str = ""):
    """Xuất kết quả ra CSV."""
    with open(out_path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["model", "top1", "top3", "delta_top3", "note", "timestamp"])
        ts = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        b = results["baseline"]
        ft = results["finetuned"]
        w.writerow(["baseline", b["top1"], b["top3"], "", suffix or b.get("note", ""), ts])
        w.writerow(["finetuned", ft["top1"], ft["top3"], delta_top3, suffix or ft.get("note", ""), ts])


# =============================================================================
# MAIN
# =============================================================================

if __name__ == "__main__":
    parser = argparse.ArgumentParser(
        description="Đánh giá mô hình nhận diện món ăn Việt (CH1)"
    )
    parser.add_argument(
        "--test_dir", default=None,
        help="Thư mục ảnh test, cấu trúc: test_dir/<class_name>/<image>.jpg"
    )
    parser.add_argument(
        "--train_dir", default=None,
        help="Thư mục ảnh train (dùng để kiểm tra rò rỉ dữ liệu)"
    )
    parser.add_argument(
        "--api_key", default=os.getenv("GEMINI_API_KEY", ""),
        help="Gemini API key (mặc định đọc từ GEMINI_API_KEY env)"
    )
    args = parser.parse_args()

    if args.test_dir is None or not Path(args.test_dir).exists():
        print("ℹ️  Không tìm thấy --test_dir hoặc thư mục không tồn tại.")
        run_sample_evaluation()
    else:
        test_dir = Path(args.test_dir)
        train_dir = Path(args.train_dir) if args.train_dir else None

        # Kiểm tra rò rỉ dữ liệu trước
        if train_dir and train_dir.exists():
            leaks = check_data_leakage(train_dir, test_dir)
            if leaks:
                print("\n⚠️  Dừng đánh giá do phát hiện rò rỉ dữ liệu.")
                print("   Hãy xóa ảnh trùng và chạy lại.")
                raise SystemExit(1)
        else:
            print("ℹ️  Bỏ qua kiểm tra rò rỉ (không có --train_dir).")

        # TODO: Implement gọi API thật và tính accuracy theo từng lớp
        # Hiện tại chạy chế độ mẫu
        print("\nℹ️  Chức năng gọi API thật trên ảnh test đang phát triển.")
        print("   Chạy chế độ mẫu để xem format output:")
        run_sample_evaluation()
