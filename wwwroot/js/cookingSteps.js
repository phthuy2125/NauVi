// cookingSteps.js - Dynamic cooking step generation per dish type
// Helper to pause all timers (used internally)
function pauseAllTimers(exceptIdx) {
    Object.keys(cookTimers).forEach(key => {
        if (parseInt(key) !== exceptIdx && cookTimers[key] && cookTimers[key].isRunning) {
            cookTimers[key].toggle();
        }
    });
}
function toggleDynTimer(idx) {
    // Stop any other running timers before toggling the selected one
    Object.keys(cookTimers).forEach(key => {
        if (parseInt(key) !== idx && cookTimers[key] && cookTimers[key].isRunning) {
            cookTimers[key].toggle(); // this will pause the timer
        }
    });
    if (cookTimers[idx]) cookTimers[idx].toggle();
}

function generateCookingSteps(food) {
    const name = food.name || 'Món ăn';
    const n = name.toLowerCase();
    const cat = (food.category || '').toLowerCase();
    const ingrs = food.ingredients || [];
    // Skip carb bases as 'main' ingredient so we don't tell users to marinate rice or boil noodles for 1 hour.
    let main = ingrs[0] || 'nguyên liệu chính';
    let subIdx = 1;
    if (main && ['cơm', 'gạo', 'bún', 'phở', 'mì', 'hủ tiếu', 'bánh mì', 'pizza', 'nước'].some(k => main.toLowerCase().includes(k))) {
        main = ingrs[1] || 'thịt/cá/rau củ';
        subIdx = 2;
    }
    // Clean up quantities if present like "200ml Sữa tươi" -> "Sữa tươi"
    main = main.replace(/^(\d+(?:\.\d+)?)\s*(g|kg|ml|l|muỗng|thìa|quả|trái|tép|tán|gr)\s*/i, '');
    let sub = ingrs.slice(subIdx, subIdx + 2).map(i => i.replace(/^(\d+(?:\.\d+)?)\s*(g|kg|ml|l|muỗng|thìa|quả|trái|tép|tán|gr)\s*/i, '')).join(', ') || 'gia vị';

    // --- Thức uống / Đồ uống ---
    if (['trà sữa', 'sinh tố', 'nước ép', 'thức uống', 'cafe', 'cà phê'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            isSweet: true,
            prepTime: '05:00', totalTime: '15 phút',
            prepDesc: `Chuẩn bị đầy đủ nguyên liệu, dụng cụ pha chế (bình lắc, máy xay). Rửa sạch ${main} nếu là trái cây tươi.`,
            steps: [
                { title: 'Sơ chế nguyên liệu', desc: `Cắt nhỏ ${main} (nếu cần). Chuẩn bị sẵn các loại topping như ${sub}.`, time: '5 phút' },
                { title: 'Pha chế / Xay trộn', desc: `Cho ${main} cùng đá viên, sữa/đường vào máy xay hoặc bình lắc. Pha trộn cho đến khi hòa quyện hoàn toàn.`, time: '5 phút', hasTimer: true, timerMins: 5 }
            ]
        };
    }

    // --- Món nước (phở, bún, canh, súp, lẩu, hủ tiếu) ---
    if (['phở','bún','lẩu','canh','súp','hủ tiếu','nước'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '20:00', totalTime: '90 phút',
            prepDesc: `Rửa sạch ${main}, chần qua nước sôi để khử mùi. Thái ${sub} vừa ăn. Nướng hành và gừng trên bếp.`,
            steps: [
                { title: 'Chần và sơ chế nguyên liệu', desc: `Cho ${main} vào nồi nước lạnh, đun sôi 3 phút rồi vớt ra rửa sạch. Bước này giúp loại bỏ tạp chất, nước dùng sẽ trong hơn.`, time: '10 phút', heat: 'Lửa lớn' },
                { title: 'Hầm nước dùng', desc: `Cho ${main} đã chần vào nồi nước mới, thêm hành nướng, gừng nướng. Đun sôi rồi hạ lửa nhỏ liu riu, hớt bọt thường xuyên.`, time: '60 phút', heat: 'Lửa nhỏ liu riu', tip: 'Không đậy nắp kín khi hầm để nước dùng luôn trong veo.', hasTimer: true, timerMins: 60 },
                { title: 'Nêm nếm nước dùng', desc: 'Thêm nước mắm, muối, đường phèn vào nước dùng. Nếm và điều chỉnh vị. Lọc bỏ xương và cặn.', time: '5 phút', heat: 'Lửa nhỏ', tip: 'Thêm chút đường phèn giúp nước dùng có vị ngọt thanh tự nhiên.' },
                { title: 'Trụng sợi và trình bày', desc: `Trụng bún/phở/mì qua nước sôi 15-20 giây, vớt ra tô. Xếp ${sub} lên trên, chan nước dùng nóng. Thêm rau thơm, giá đỗ.`, time: '5 phút', heat: 'Lửa lớn' }
            ]
        };
    }

    // --- Bánh mì / Đồ ăn nhanh kẹp ---
    if (['bánh mì', 'burger', 'hamburger', 'sandwich', 'hotdog', 'kebab'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '05:00', totalTime: '15 phút',
            prepDesc: `Làm nóng ${main}. Sơ chế và thái lát các nguyên liệu ăn kèm như ${sub}.`,
            steps: [
                { title: 'Chuẩn bị vỏ bánh', desc: `Nướng hoặc áp chảo nhẹ ${main} cho nóng giòn. Phết các loại sốt (bơ, pate, mayonnaise) lên hai mặt bánh.`, time: '3 phút', tip: 'Làm nóng bánh mì ngay trước khi ăn để giữ độ giòn rụm.' },
                { title: 'Chế biến nhân (nếu cần)', desc: `Chiên, nướng hoặc làm nóng phần nhân (thịt, xúc xích, trứng).`, time: '5 phút', heat: 'Lửa vừa' },
                { title: 'Kẹp nhân và hoàn thiện', desc: `Lần lượt xếp ${sub} cùng phần nhân chính vào trong vỏ bánh. Rưới thêm tương ớt, tương cà hoặc sốt chuyên dụng.`, time: '2 phút' }
            ]
        };
    }

    // --- Pizza ---
    if (['pizza'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '15:00', totalTime: '30 phút',
            prepDesc: `Chuẩn bị đế bánh ${main}. Thái lát ${sub} và phô mai. Làm nóng lò nướng ở 200°C.`,
            steps: [
                { title: 'Phết sốt và xếp nhân', desc: `Phết đều sốt cà chua lên đế ${main}. Xếp lần lượt các loại topping và rắc đều phô mai lên trên.`, time: '5 phút', tip: 'Không phết sốt quá sát mép bánh để tránh bị cháy khi nướng.' },
                { title: 'Nướng bánh', desc: `Cho bánh vào lò nướng ở nhiệt độ cao (200°C - 220°C). Nướng đến khi phô mai tan chảy và đế bánh chín vàng.`, time: '12-15 phút', heat: '200°C', hasTimer: true, timerMins: 15, warning: 'Phô mai rất dễ cháy, cần quan sát kĩ trong những phút cuối.' },
                { title: 'Hoàn thiện', desc: 'Lấy bánh ra, cắt thành từng miếng tam giác vừa ăn. Dùng kèm với tương ớt hoặc tương cà.', time: '2 phút' }
            ]
        };
    }

    // --- Chè / Kem lạnh ---
    if (['chè', 'kem'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            isSweet: true,
            prepTime: '10:00', totalTime: 'Mát lạnh',
            prepDesc: `Sơ chế sạch sẽ các nguyên liệu. Cắt nhỏ ${main} (nếu là trái cây tươi). Chuẩn bị sẵn đá viên hoặc khuôn đựng.`,
            steps: [
                { title: 'Chế biến nguyên liệu cốt', desc: `Nếu là chè, đem ${main} đun nhỏ lửa đến khi chín mềm. Nếu là kem/kem bơ, xay nhuyễn ${main} cùng sữa và cốt dừa đến khi mịn.`, time: '15 phút', heat: 'Lửa nhỏ / Máy xay', hasTimer: true, timerMins: 15 },
                { title: 'Pha chế và kết hợp', desc: `Trộn đều hỗn hợp vừa làm với ${sub} và sữa đặc/đường cho vừa vị ngọt. Nếm thử và điều chỉnh độ ngọt theo khẩu vị.`, time: '5 phút' },
                { title: 'Làm lạnh và trang trí', desc: 'Cho ra ly hoặc tô đẹp. Cất vào ngăn mát/ngăn đá tùy món đến khi đạt độ lạnh mong muốn. Trang trí với dừa khô, đậu phộng rang hoặc lá bạc hà.', time: 'Chờ lạnh', tip: 'Thưởng thức ngay khi mát lạnh để cảm nhận trọn vẹn hương vị.' }
            ]
        };
    }

    // --- Bánh ngọt / Bánh nướng (Tráng miệng có lò) ---
    if (['tráng miệng', 'tart', 'mousse', 'flan', 'tiramisu', 'su kem'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            isSweet: true,
            prepTime: '20:00', totalTime: '45 phút',
            prepDesc: `Đong đếm chính xác các nguyên liệu. Để ${main} về nhiệt độ phòng. Làm nóng lò nướng ở 180°C (nếu cần nướng).`,
            steps: [
                { title: 'Chuẩn bị hỗn hợp bánh', desc: `Đánh bông ${main} với đường đến khi bông mịn. Rây bột vào và trộn nhẹ tay theo kiểu fold để giữ độ xốp.`, time: '15 phút', tip: 'Trộn nhẹ tay theo chiều từ dưới lên, tránh đảo mạnh sẽ làm mất bọt khí.' },
                { title: 'Đổ khuôn / Tạo lớp', desc: `Đổ hỗn hợp vào khuôn đã lót giấy. Gõ nhẹ khuôn xuống bàn để loại bọt khí. Dàn đều bề mặt.`, time: '5 phút' },
                { title: 'Nướng hoặc làm lạnh', desc: 'Với bánh nướng: cho vào lò 180°C trong 20-25 phút. Với mousse/flan/tiramisu: bọc màng thực phẩm và ướp lạnh ít nhất 3-4 tiếng.', time: '20 phút', heat: '180°C (bánh nướng)', hasTimer: true, timerMins: 20, warning: 'Không mở lò trong 15 phút đầu để bánh không bị xẹp.' }
            ]
        };
    }

    // --- Món chiên / rán ---
    if (['chiên','rán','đồ ăn nhanh'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '10:00', totalTime: '25 phút',
            prepDesc: `Sơ chế ${main}: rửa sạch, để ráo. Chuẩn bị ${sub} và gia vị ướp.`,
            steps: [
                { title: 'Ướp gia vị', desc: `Ướp ${main} với muối, tiêu, tỏi băm, bột ngọt trong 10-15 phút để ngấm đều.`, time: '15 phút', tip: 'Thêm chút nước mắm khi ướp giúp món chiên đậm đà hơn.' },
                { title: 'Chuẩn bị bột chiên', desc: 'Trộn bột chiên giòn với nước lạnh theo tỉ lệ 1:1. Khuấy đều, không cần quá mịn. Nhúng nguyên liệu qua bột.', time: '5 phút', tip: 'Dùng nước đá lạnh pha bột sẽ giúp lớp vỏ giòn rụm hơn.' },
                { title: 'Chiên giòn', desc: `Đun nóng dầu ăn (ngập khoảng 5cm). Thả ${main} vào chiên ở lửa vừa đến vàng đều. Lật nhẹ nhàng.`, time: '8 phút', heat: 'Lửa vừa (170°C)', hasTimer: true, timerMins: 8, warning: 'Nguyên liệu phải ráo nước trước khi chiên để tránh dầu bắn gây bỏng.' },
                { title: 'Vớt ra và để ráo dầu', desc: 'Vớt ra đặt lên giấy thấm dầu. Để ráo 2 phút. Có thể chiên lần 2 ở lửa lớn trong 1 phút để giòn hơn.', time: '3 phút', tip: 'Chiên 2 lần (double fry) là bí quyết để lớp vỏ giòn lâu không bị xỉn.' }
            ]
        };
    }

    // --- Món xào ---
    if (['xào'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '10:00', totalTime: '20 phút',
            prepDesc: `Thái ${main} miếng mỏng vừa ăn. Băm nhỏ tỏi, hành tím. Rửa sạch ${sub}.`,
            steps: [
                { title: 'Phi thơm hành tỏi', desc: 'Làm nóng chảo với 2 muỗng dầu ăn. Cho tỏi băm, hành tím vào phi vàng thơm.', time: '2 phút', heat: 'Lửa vừa', tip: 'Phi hành tỏi ở lửa vừa để không bị cháy đen, giữ được hương thơm.' },
                { title: 'Xào nguyên liệu chính', desc: `Vặn lửa lớn, cho ${main} vào đảo nhanh tay. Xào 2-3 phút đến khi nguyên liệu chín tái.`, time: '3 phút', heat: 'Lửa lớn', hasTimer: true, timerMins: 3 },
                { title: 'Thêm rau củ và gia vị', desc: `Cho ${sub} vào, đảo đều. Nêm nước mắm, muối, đường, tiêu. Đảo thêm 1-2 phút.`, time: '2 phút', heat: 'Lửa lớn', warning: 'Không xào quá lâu, rau sẽ mềm nhũn và mất vitamin.' },
                { title: 'Hoàn thiện', desc: 'Tắt bếp, rưới chút dầu mè lên trên. Cho ra đĩa và rắc tiêu, hành lá.', time: '1 phút', tip: 'Xào ở lửa lớn đảo nhanh giúp rau giữ màu xanh đẹp và giòn.' }
            ]
        };
    }

    // --- Món nướng ---
    if (['nướng'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '30:00', totalTime: '75 phút',
            prepDesc: `Rửa sạch ${main}. Pha sốt ướp và xoa đều lên nguyên liệu. Ướp ít nhất 30 phút.`,
            steps: [
                { title: 'Ướp gia vị', desc: `Trộn sốt ướp (nước mắm, mật ong, tỏi, tiêu, dầu hào). Xoa đều lên ${main}, massage nhẹ cho ngấm. Ủ trong tủ lạnh.`, time: '30 phút', tip: 'Ướp qua đêm trong tủ lạnh giúp gia vị thấm sâu, món nướng đậm đà hơn.' },
                { title: 'Làm nóng lò/bếp nướng', desc: 'Làm nóng lò nướng ở 200°C hoặc chuẩn bị than hoa đến khi than cháy đỏ đều, không còn khói đen.', time: '10 phút', heat: '200°C' },
                { title: 'Nướng', desc: `Đặt ${main} lên vỉ nướng. Nướng mỗi mặt 8-10 phút, lật đều. Phết thêm sốt ướp mỗi lần lật.`, time: '20 phút', heat: '180-200°C', hasTimer: true, timerMins: 20, warning: 'Kiểm soát nhiệt để không cháy ngoài sống trong. Lật thường xuyên.' },
                { title: 'Phết mật ong và hoàn thiện', desc: 'Phết lớp mật ong mỏng lên bề mặt ở 2 phút cuối. Nướng thêm đến khi bóng đẹp.', time: '2 phút', heat: 'Lửa vừa', tip: 'Lớp mật ong cuối giúp món nướng có màu cánh gián bắt mắt.' }
            ]
        };
    }

    // --- Salad / Gỏi ---
    if (['salad','gỏi','trộn'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '15:00', totalTime: '20 phút',
            prepDesc: `Rửa sạch rau củ bằng nước muối loãng. Để ráo. Luộc chín ${main} nếu cần.`,
            steps: [
                { title: 'Chuẩn bị nguyên liệu', desc: `Thái ${main} sợi mỏng hoặc miếng vừa ăn. Thái ${sub} nhỏ. Luộc tôm/thịt nếu có, để nguội.`, time: '10 phút' },
                { title: 'Pha nước trộn', desc: 'Pha nước mắm chua ngọt: 2 muỗng nước mắm, 2 muỗng đường, 3 muỗng nước cốt chanh, 1 muỗng tỏi ớt băm. Khuấy tan.', time: '3 phút', tip: 'Tỉ lệ chua:ngọt:mặn = 3:2:1 là công thức vàng cho nước trộn gỏi.' },
                { title: 'Trộn đều và thưởng thức', desc: 'Cho tất cả nguyên liệu vào tô lớn. Rưới nước trộn lên, đảo nhẹ tay. Rắc đậu phộng rang và rau thơm.', time: '2 phút', warning: 'Trộn ngay trước khi ăn để rau giữ độ giòn, không bị héo.' }
            ]
        };
    }

    // --- Món hấp ---
    if (['hấp'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '15:00', totalTime: '35 phút',
            prepDesc: `Rửa sạch ${main}. Chuẩn bị nồi hấp với nước sôi.`,
            steps: [
                { title: 'Ướp nguyên liệu', desc: `Ướp ${main} với hành, tỏi, nước mắm, tiêu, dầu mè. Để 10 phút cho ngấm.`, time: '10 phút' },
                { title: 'Hấp chín', desc: `Đặt ${main} vào đĩa, cho vào nồi hấp khi nước đã sôi. Đậy nắp kín và hấp đều.`, time: '15 phút', heat: 'Lửa vừa', hasTimer: true, timerMins: 15, tip: 'Không mở nắp trong quá trình hấp để giữ nhiệt đều.' },
                { title: 'Pha nước chấm và hoàn thiện', desc: 'Pha nước mắm gừng hoặc mắm chanh ớt. Lấy đĩa ra, rưới mỡ hành phi lên trên.', time: '3 phút', tip: 'Rưới mỡ hành nóng lên món hấp giúp dậy mùi thơm hấp dẫn.' }
            ]
        };
    }

    // --- Mì ---
    if (['mì'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '10:00', totalTime: '25 phút',
            prepDesc: `Chuẩn bị ${main}, ${sub}. Rửa sạch rau ăn kèm.`,
            steps: [
                { title: 'Nấu nước dùng/sốt', desc: `Phi hành tỏi, cho ${sub} vào xào. Thêm nước dùng hoặc sốt gia vị. Đun sôi rồi hạ lửa.`, time: '10 phút', heat: 'Lửa vừa' },
                { title: 'Luộc mì', desc: 'Đun sôi nồi nước riêng. Cho mì vào luộc 3-4 phút đến khi mềm vừa. Vớt ra xả nước lạnh.', time: '4 phút', heat: 'Lửa lớn', hasTimer: true, timerMins: 4, tip: 'Xả nước lạnh sau khi luộc giúp sợi mì dai và không bị dính.' },
                { title: 'Kết hợp và nêm nếm', desc: `Cho mì vào tô, chan nước dùng hoặc trộn sốt. Xếp ${main} và rau lên trên. Nêm thêm nước mắm, tiêu.`, time: '3 phút' }
            ]
        };
    }

    // --- Cơm / Cháo ---
    if (['cơm','cháo'].some(k => cat.includes(k) || n.includes(k))) {
        return {
            mainIngredient: main,
            prepTime: '10:00', totalTime: '40 phút',
            prepDesc: `Vo gạo sạch. Sơ chế ${main} và ${sub}.`,
            steps: [
                { title: 'Chuẩn bị nguyên liệu phụ', desc: `Thái ${main} thành miếng vừa ăn. Ướp với gia vị cơ bản (muối, tiêu, nước mắm) trong 10 phút.`, time: '10 phút' },
                { title: 'Nấu cơm/cháo', desc: 'Cho gạo và nước vào nồi theo tỉ lệ phù hợp. Nấu đến khi chín. Với cháo: khuấy đều và hầm thêm.', time: '20 phút', heat: 'Lửa vừa → nhỏ', hasTimer: true, timerMins: 20 },
                { title: 'Chế biến topping', desc: `Phi hành tỏi, xào ${main} chín. Nêm nếm vừa ăn. Múc ra và ăn kèm với cơm/cháo.`, time: '8 phút', heat: 'Lửa vừa', tip: 'Với cháo, thêm chút gừng thái sợi giúp dậy mùi thơm.' }
            ]
        };
    }

    // --- Default ---
    return {
        mainIngredient: main,
        prepTime: '15:00', totalTime: '45 phút',
        prepDesc: `Rửa sạch tất cả nguyên liệu. Thái ${main} và ${sub} vừa ăn. Chuẩn bị gia vị.`,
        steps: [
            { title: 'Sơ chế chi tiết', desc: `Rửa sạch ${main}, cắt miếng vừa ăn. Băm nhỏ tỏi, hành. Pha sẵn gia vị nêm nếm.`, time: '10 phút' },
            { title: 'Nấu chín nguyên liệu', desc: `Làm nóng chảo/nồi. Cho ${main} vào nấu cùng ${sub} và gia vị theo thứ tự phù hợp.`, time: '15 phút', heat: 'Lửa vừa', hasTimer: true, timerMins: 15 },
            { title: 'Nêm nếm và điều chỉnh', desc: 'Nếm thử, thêm muối, đường, nước mắm nếu cần. Đảo đều và nấu thêm 2-3 phút.', time: '3 phút', tip: 'Nêm nếm từ từ, có thể thêm nhưng không thể bớt.' }
        ]
    };
}

function renderCookingSteps(stepsData, food) {
    const container = document.getElementById('cooking-steps-container');
    if (!container) return;
    container.innerHTML = '';
    cookTimers = {};

    const steps = stepsData.steps;

    // Update step 1 prep
    const s1Desc = document.getElementById('step1-desc');
    if (s1Desc) s1Desc.innerText = stepsData.prepDesc;
    const s1Time = document.getElementById('step1-time');
    if (s1Time) s1Time.innerText = stepsData.prepTime;
    const hTime = document.getElementById('header-time');
    if (hTime) hTime.innerText = stepsData.totalTime;

    // Render each cooking step (starting from step #2)
    steps.forEach((step, idx) => {
        const num = idx + 2;
        let metaBadges = '';
        if (step.time) metaBadges += `<span class="inline-flex items-center gap-1.5 px-4 py-2 rounded-full bg-primary/10 text-primary font-bold text-sm"><span class="material-symbols-outlined text-base">schedule</span>${step.time}</span>`;
        if (step.heat) metaBadges += `<span class="inline-flex items-center gap-1.5 px-4 py-2 rounded-full bg-error/10 text-error font-bold text-sm"><span class="material-symbols-outlined text-base">local_fire_department</span>${step.heat}</span>`;

        let tipHTML = '';
        if (step.tip) {
            tipHTML = `<div class="bg-secondary-container/30 border-2 border-secondary-container p-5 rounded-2xl">
                <div class="flex items-center gap-3 mb-2"><div class="p-2 bg-secondary text-on-secondary rounded-full"><span class="material-symbols-outlined text-sm" style="font-variation-settings:'FILL' 1;">lightbulb</span></div><span class="text-sm font-black uppercase text-secondary tracking-widest">Mẹo nhỏ</span></div>
                <p class="text-base text-on-surface-variant font-medium">${step.tip}</p></div>`;
        }
        let warnHTML = '';
        if (step.warning) {
            warnHTML = `<div class="bg-error-container/20 border-2 border-error-container p-5 rounded-2xl">
                <div class="flex items-center gap-3 mb-2"><div class="p-2 bg-error text-on-error rounded-full"><span class="material-symbols-outlined text-sm">warning</span></div><span class="text-sm font-black uppercase text-error tracking-widest">Lưu ý</span></div>
                <p class="text-base text-on-surface-variant font-medium">${step.warning}</p></div>`;
        }
        let tipsGrid = (tipHTML || warnHTML) ? `<div class="grid grid-cols-1 ${tipHTML && warnHTML ? 'md:grid-cols-2' : ''} gap-4 my-2">${tipHTML}${warnHTML}</div>` : '';

        let timerHTML = '';
        if (step.hasTimer && step.timerMins) {
            const mins = step.timerMins;
            const timeStr = `${mins.toString().padStart(2,'0')}:00`;
            timerHTML = `<div class="bg-surface-container p-5 rounded-2xl flex flex-col md:flex-row items-center gap-5 border border-primary/20">
                <div class="flex items-center gap-3">
                    <div id="dyn-timer-icon-${idx}" class="h-14 w-14 rounded-full bg-primary flex items-center justify-center text-on-primary shadow-lg"><span class="material-symbols-outlined text-2xl">timer</span></div>
                    <div><p class="text-xs font-bold text-primary uppercase tracking-wider mb-1">Bộ đếm thời gian</p><span id="dyn-timer-time-${idx}" class="font-black text-3xl text-on-surface tracking-tighter">${timeStr}</span></div>
                </div>
                <div class="flex-grow w-full">
                    <div class="flex justify-between text-xs font-bold text-on-surface-variant mb-1"><span>Bắt đầu</span><span id="dyn-timer-ptext-${idx}">0% Hoàn thành</span></div>
                    <div class="h-3 bg-white/50 rounded-full overflow-hidden w-full"><div id="dyn-timer-pbar-${idx}" class="bg-gradient-to-r from-primary to-primary-container h-full w-0 rounded-full relative transition-all duration-1000 ease-linear"><div class="absolute right-0 top-0 bottom-0 w-2 bg-white/50 animate-pulse"></div></div></div>
                </div>
                <button id="dyn-timer-btn-${idx}" onclick="toggleDynTimer(${idx})" class="w-full md:w-auto px-6 py-3 rounded-full bg-primary-container text-on-primary-container font-black shadow-md hover:bg-primary hover:text-on-primary transition-colors whitespace-nowrap">Bắt đầu</button>
            </div>`;
        }

        container.innerHTML += `
            <div class="bg-surface-container-lowest rounded-[2.5rem] p-10 shadow-[0_20px_40px_-15px_rgba(0,100,121,0.1)] relative overflow-hidden group">
                <div class="absolute -top-10 -right-10 text-[200px] font-black text-surface-container-low opacity-50 select-none z-0">${num}</div>
                <div class="relative z-10 flex flex-col gap-5">
                    <div class="flex items-center gap-6 mb-2">
                        <div class="h-16 w-16 rounded-full bg-gradient-to-br from-primary to-primary-container text-on-primary flex items-center justify-center font-black text-3xl shadow-lg">${num}</div>
                        <h4 class="text-3xl font-black text-on-surface tracking-tight">${step.title}</h4>
                    </div>
                    <p class="text-lg text-on-surface-variant leading-relaxed font-medium">${step.desc}</p>
                    ${metaBadges ? `<div class="flex flex-wrap gap-3">${metaBadges}</div>` : ''}
                    ${tipsGrid}
                    ${timerHTML}
                </div>
            </div>`;
    });

    // Final step: Trình bày — phân biệt ngọt / mặn
    const finalNum = steps.length + 2;
    const isSweet = stepsData.isSweet === true;
    const finalDesc = isSweet
        ? `Cho ${food.name || 'món tráng miệng'} ra ly/tô/đĩa xinh xắn. Trang trí với hoa quả tươi, lá bạc hà hoặc topping yêu thích. Thưởng thức ngay khi mát lạnh!`
        : `Tắt bếp, cho ${food.name || 'món ăn'} ra đĩa/tô đẹp. Trang trí với rau thơm tươi, hành phi hoặc ớt thái lát. Dọn kèm nước chấm phù hợp và thưởng thức ngay khi còn nóng!`;
    const finalTip = isSweet
        ? 'Dùng ly hoặc tô thủy tinh trong suốt giúp tôn lên màu sắc đẹp mắt của món tráng miệng.'
        : 'Dùng đĩa trắng để tôn lên màu sắc món ăn. Rắc thêm mè rang hoặc hành phi giúp tăng hương vị và tính thẩm mỹ.';
    container.innerHTML += `
        <div class="bg-surface-container-lowest rounded-[2.5rem] p-10 shadow-[0_20px_40px_-15px_rgba(0,100,121,0.1)] relative overflow-hidden group">
            <div class="absolute -top-10 -right-10 text-[200px] font-black text-surface-container-low opacity-50 select-none z-0">${finalNum}</div>
            <div class="relative z-10 flex flex-col gap-5">
                <div class="flex items-center gap-6 mb-2">
                    <div class="h-16 w-16 rounded-full bg-gradient-to-br from-primary to-primary-container text-on-primary flex items-center justify-center font-black text-3xl shadow-lg">${finalNum}</div>
                    <h4 class="text-3xl font-black text-on-surface tracking-tight">Trình bày và thưởng thức</h4>
                </div>
                <p class="text-lg text-on-surface-variant leading-relaxed font-medium">${finalDesc}</p>
                <div class="bg-secondary-container/30 border-2 border-secondary-container p-5 rounded-2xl">
                    <div class="flex items-center gap-3 mb-2"><div class="p-2 bg-secondary text-on-secondary rounded-full"><span class="material-symbols-outlined text-sm" style="font-variation-settings:'FILL' 1;">lightbulb</span></div><span class="text-sm font-black uppercase text-secondary tracking-widest">Mẹo trình bày</span></div>
                    <p class="text-base text-on-surface-variant font-medium">${finalTip}</p>
                </div>
            </div>
        </div>`;

    // Create timer instances after DOM is updated
    steps.forEach((step, idx) => {
        if (step.hasTimer && step.timerMins) {
            cookTimers[idx] = new CookingTimer(
                `dyn-timer-time-${idx}`, `dyn-timer-btn-${idx}`,
                `dyn-timer-pbar-${idx}`, `dyn-timer-ptext-${idx}`,
                `dyn-timer-icon-${idx}`
            );
        }
    });
}
