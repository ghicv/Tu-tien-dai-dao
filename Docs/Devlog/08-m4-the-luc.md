# 08 · M4 — Thế lực

**Ngày:** 2026-10-06

## Mục tiêu (GDD §6, roadmap M4)
"Faction chung, làng → thành, tông môn lập/tuyển đệ tử, ngoại giao opinion." Kiểm chứng: **tông môn tự sinh và tranh linh mạch**.

## Đã làm

### Mô hình thế lực (`Sim/FactionSystem.cs`)
- **`Faction`:**
  - Hiện mỗi thế lực là một tông môn. Id của thế lực trùng id settlement của sơn môn, nên `Cultivator.SectId` không phải đổi gì.
  - Các trường: chính/ma đạo, ngày lập, tông mẹ (nếu là phân tông ly khai), người khai sơn, kho **linh thạch**, thực lực, số thành viên, lãnh thổ, số trận thắng/thua, số tử trận, màu cờ.
  - Về sau quốc gia phàm nhân hay gia tộc có thể dùng chung mô hình này.
- **Lãnh thổ** chia theo ô 16×16 cell (lưới 64×64):
  - Mỗi ô lưu lượng linh khí trung bình, tỉ lệ đất liền và có linh mạch hay không. Ô được tính lại khi địa hình hoặc linh khí đổi; ô bị chìm xuống biển thì thành vô chủ.
  - Mỗi năm, tông môn chiếm thêm tối đa 2 ô biên giới tốt nhất. Ô được chấm điểm theo linh khí, linh mạch (+2) và khoảng cách tới sơn môn.
  - Kích thước lãnh thổ mong muốn = 2 + √thực lực × 0,9, tối đa 48 ô. Tông yếu đi thì mất dần các ô xa và nghèo.
  - Ô có sơn môn luôn thuộc về tông đó.
  - Nếu ô tốt nhất đang nằm trong tay tông khác (không phải đồng minh) thì tính là **tranh đoạt**: thiện cảm giảm 8. Tranh linh mạch còn được ghi thành sự kiện.
- **Thực lực** cộng theo cảnh giới của thành viên: Luyện Khí 1, Trúc Cơ 5, Kết Đan 30, Nguyên Anh 200, Hóa Thần 1500. Mỗi tiểu cảnh giới cộng thêm 15%.
- **Linh thạch:**
  - Thu mỗi năm: 4 + 6 cho mỗi ô linh mạch + cống nạp 0,03 cho mỗi dân của các làng phàm nhân nằm trong lãnh thổ.
  - Chi mỗi năm: phí duy trì 0,12 × thực lực (đan dược, pháp khí, trận pháp).
  - Trúc Cơ Đan (30 linh thạch) giờ phải **mua bằng kho của tông**; trước đây là ngẫu nhiên 50%. Tông nghèo thì đệ tử khó Trúc Cơ hơn.
  - Mỗi trận đánh tốn 25 linh thạch cho bên tấn công và 15 cho bên phòng thủ.
  - Tông có trên 800 linh thạch có thể **chiêu mộ khách khanh**: tán tu mạnh nhất cùng chính/ma trong bán kính 200, giá 100–850 tùy cảnh giới.
- **Che chở:** làng nằm trong lãnh thổ tông nào thì nộp cống cho tông đó, và trẻ có linh căn ở làng đó được tông này thu nhận trước.

### Ngoại giao
- **Thiện cảm** (−100…100) mỗi năm trôi dần về một mức "tự nhiên" (12%/năm), cộng thêm nhiễu ngẫu nhiên. Mức tự nhiên được tính từ:
  - Chính/ma: chính–chính +12, ma–ma −12, chính–ma −30.
  - Biên giới chung: −1,5 cho mỗi đoạn tiếp giáp, tối đa 15.
  - Phân tông với tông mẹ: −20.
  - Tông nghèo giáp tông đang giữ từ 3 linh mạch trở lên: −15.
  - Có chung kẻ thù đang giao chiến: +40; cùng thù địch với một tông khác: +20 (tối đa +60).
- **Lệch khỏi mức tự nhiên** vì các biến cố: mỗi lần tranh đoạt −8, mỗi tu sĩ tử trận −8.
- **Trạng thái** (có độ trễ để không bật/tắt liên tục):
  - Đồng minh: lên khi trên 40, tan khi xuống dưới 15.
  - Thù địch: lên khi dưới −30, hết khi lên trên −15.
  - Còn lại là Trung lập.
  - Chiến tranh và Đình chiến 20 năm có luật riêng, xem phần dưới.
- **Tuyên chiến:** đang thù địch, thiện cảm dưới −50, là hàng xóm, mỗi bên đang có ít hơn 2 cuộc chiến, xác suất 25%/năm. Bên mạnh hơn là bên tuyên chiến.
- **Giảng hòa:** sau ít nhất 1 năm, xác suất tăng dần theo số năm chiến, số trận đã đánh và độ chênh lực. Sau đó đình chiến 20 năm, thiện cảm đặt về −25.
- Thế giới tự chia thành hai khối **chính đạo** và **ma đạo**, đúng tinh thần Thiên Nam trong truyện.

### Chiến tranh (trận trừu tượng; M5 sẽ làm sâu hơn)
- Mỗi tháng, mỗi cuộc chiến chưa có trận nào đang diễn ra có 1/3 khả năng mở trận mới.
- **Chọn bên và mục tiêu:**
  - Bên tấn công được chọn theo tỉ lệ thực lực.
  - Mục tiêu là ô biên giới tốt nhất của địch, ưu tiên linh mạch; không giáp nhau thì đánh xa.
  - Chỉ vây công sơn môn khi không còn ô nào khác để đánh.
- **Xuất quân:**
  - Mỗi bên cử tối đa 3 tu sĩ từ Trúc Cơ trở lên đang ở nhà, vây sơn môn thì 4. Tông chủ chỉ ra trận khi vây/giữ sơn môn, hoặc 20% các lần khác.
  - Tu sĩ **thật sự ngự kiếm bay tới chiến trường** (`Trip.Battle`). Trên map có nhãn đỏ "Chiến trường: A – B".
- **Phân thắng bại** sau 30 ngày:
  - Lực mỗi bên = tổng trọng số của tu sĩ có mặt (tâm cảnh tốt thì mạnh hơn; ma tu ×1,15) + Luyện Khí ở nhà làm quân số.
  - Bên phòng thủ ×1,1, khi giữ sơn môn ×1,35 (hộ sơn đại trận). Kết quả tung ngẫu nhiên ×0,7–1,3.
- **Thương vong:** xác suất chết = cơ sở × 2^(cảnh giới cao nhất của địch − cảnh giới bản thân). Cơ sở là 0,3 cho bên thua và 0,06 cho bên thắng. Tử trận có hiệu ứng nổ.
- **Kết quả:**
  - Thắng thì chiếm ô.
  - Thắng trận vây sơn môn thì **diệt môn**:
    - Hall bị phá.
    - Đệ tử từ Trúc Cơ trở xuống cùng chính/ma có 35% quy hàng bên thắng; còn lại thành tán tu.
    - Phàm nhân ở lại thành làng thường với tên mới.
    - Bên thắng nhận các ô nằm trong tầm với.

### Tông môn tự sinh
- **Khai tông lập phái:** tán tu đang ở nhà tự lập tông mỗi năm, xác suất nhân với (0,4 + dã tâm):
  - Nguyên Anh: 12%/năm; Kết Đan: 5%; Trúc Cơ đại viên mãn: 1%.
  - Địa điểm: chỗ đất trống vô chủ, linh khí ≥ 2000, đủ chỗ cho sơn môn 5×5 và cách các khu dân cư khác.
  - Mấy chục người hầu được tách từ làng gần nhất; tán tu cùng đạo quanh đó 35% theo về.
- **Ly khai (phản xuất):** mỗi năm, mỗi tông có ít nhất 6 thành viên. Áp dụng cho trưởng lão Kết Đan+ (không phải tông chủ):
  - Xác suất: 0,4% × (0,3 + dã tâm) × quy mô tông.
  - Cộng 8% nếu vừa đổi tông chủ trong 2 năm qua và vị trưởng lão này mạnh gần bằng tông chủ (tranh ngôi).
  - Trưởng lão **khác chính/ma với tông** thì 25%/năm bị trục xuất hoặc tự phản xuất.
  - Người ly khai dẫn theo tối đa 35% đệ tử và lập tông cách ít nhất 40 ô. Quan hệ với tông cũ bắt đầu ở −70 (−90 nếu khác chính/ma).
  - Không tìm được chỗ thì thành tán tu.
- **Tông chủ nhập ma:** 20%/năm cả tông sa vào ma đạo.
- **Suy tàn:** tông không còn tu sĩ nào thì thành làng thường.
- **Dã tâm (`Ambition`):** chỉ số mới của tu sĩ.
- **Tên tông:** lấy theo truyện (`Lore.DemonicSects`, `Lore.RighteousSects`), không trùng tên cũ. Hết tên thì ghép âm tiết kèm hậu tố Tông/Môn/Cốc/Phái/Cung/Các/Sơn.
- Thế giới tối đa 24 thế lực.

### Hiển thị
- **Lớp phủ Lãnh thổ:** tô màu theo cờ của từng thế lực (ma đạo tông tím đỏ, chính đạo các màu khác), viền đậm ở biên giới, ruộng làng nhạt hơn.
- **Cửa sổ Thế lực** (icon cờ, góc trên phải): 7 thế lực mạnh nhất, kèm số tu sĩ, cảnh giới tông chủ, lãnh thổ, linh mạch, linh thạch, đang chiến / đồng minh / thù địch với ai.
- **Thẻ thông tin:**
  - Tông môn có thêm khối Thế lực: chính/ma, tách từ đâu, người khai sơn, lãnh thổ, thu nhập, thực lực, chiến tích, quan hệ.
  - Làng: "Dưới sự che chở của …".
  - Ô đất: thuộc lãnh thổ ai, có linh mạch chưa ai chiếm.
- **Nhãn:** tên tông môn mang màu cờ; nhãn đỏ ở chiến trường đang diễn ra.
- Click vào sơn môn (hall) mở thẻ tông môn. Trước đây hall không thuộc về ai nên chỉ ra thẻ "vật thể".

## Cân chỉnh
- **Bản đầu, thiện cảm cộng dồn hằng năm:** tông nào cũng rơi xuống −100, 266 lần tuyên chiến trong 200 năm. Đổi sang trôi về mức tự nhiên.
- **Sau đó quá hòa bình:** chỉ 6 cuộc chiến, liên minh bật/tắt liên tục. Thêm độ trễ trạng thái và yếu tố "chung mối đe dọa", hạ ngưỡng tuyên chiến xuống −50.
- **Linh thạch chạm trần ở mọi tông:** thêm phí duy trì, phí chiến tranh và chiêu mộ khách khanh. Giờ tông có linh mạch thì giàu, tông không có thì cạn, nên linh mạch thành thứ đáng để đánh nhau.
- **Kết quả sau 150 năm** (seed ThienDao): 24 thế lực sống / 32 từng tồn tại, 17 lần lập tông, 42 lần ly khai hoặc trục xuất, 225 trận, 20 lần giảng hòa, 2 lần diệt môn, 28 lần chiêu mộ. Hiệu năng vẫn khoảng 0,16 ms/tick.

## Bất biến và test
- **`WorldInvariants`** có thêm các kiểm tra:
  - Tông còn sống thì thế lực còn sống, và ngược lại.
  - Lãnh thổ và quan hệ chỉ thuộc về thế lực còn sống; thiện cảm nằm trong khoảng hợp lệ; linh thạch không âm.
  - Không có tu sĩ nào thuộc thế lực đã chết.
- **Test mới:**
  - `SectsStartAsFactionsHoldingLand`
  - `NewSectsAreFoundedAndBreakAway` (150 năm)
  - `WarIsFoughtOverLandAndCostsLives`
  - `DestroyedSectLeavesNoDanglingState`: dìm cả tông môn xuống biển.
- Chaos test và test tất định (hash có thêm trạng thái thế lực) vẫn chạy.

## Còn để sau
- Đồng minh nhảy vào trợ chiến; chiến đấu cá nhân và đấu pháp (M5).
- Quốc gia phàm nhân: thành trì, vua chúa, tông môn đỡ đầu một nước (kiểu Việt Quốc thất phái).
- Thiên Đạo gieo thù hận hoặc hóa giải (M6).
- Chợ và giá linh thạch (M7).
