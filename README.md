# NauVi
Tên đề tài (VN):Phát triển NauVi - Nền tảng khám phá và chia sẻ món ăn nhận diện món Việt từ ảnh dựa trên học chuyển giao.

Tên đề tài (Eng):Developing NauVi - A Food Discovery and Sharing Platform Recognizing Vietnamese Dishes from Photos Based on Transfer Learning.

“NauVi giải quyết vấn đề tìm kiếm công thức từ hình ảnh món ăn và chia sẻ thông tin ẩm thực phân tán trên nhiều nền tảng cho người yêu thích ẩm thực, hướng tới giảm ít nhất 30% thời gian tìm công thức so với cách tìm kiếm thông thường.”

## Chỉ số đánh giá

| Chỉ số | Cách đo | Trước | Sau (mục tiêu) | Ngày đo | Bằng chứng |
|---|---|---|---|---|---|
| **Top-3 accuracy** của mô hình nhận diện món ăn | Chạy mô hình trên tập ảnh kiểm thử đã gán nhãn; tính tỉ lệ ảnh có nhãn đúng nằm trong 3 dự đoán có xác suất cao nhất | Chưa đo (baseline: mô hình cơ sở) | --- | W1 - W2 và W8 | Bảng kết quả đánh giá trên tập kiểm thử, confusion matrix, nhật ký chạy mô hình |
| **Thời gian tìm thông tin và công thức từ ảnh món ăn** | Bấm giờ từ lúc bắt đầu tìm đến khi tìm được công thức phù hợp | Chưa đo | Giảm dự kiến ít nhất 30% so với trước | W1 - W2 và W8 | Bảng ghi kết quả thử nghiệm |
