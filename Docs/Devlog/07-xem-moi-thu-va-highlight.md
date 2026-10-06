# 07 · Xem mọi thứ + highlight khi rê chuột

**Ngày:** 2026-10-06

## Lý do
Người chơi báo:
- *"tính năng click vào nhân vật để xem thông tin bị lỗi, k click được"*
- *"xem toàn bộ thông tin của tất cả các game object, từng cell cũng được"*
- *"chuột hover vào object nào thì cho nó cái highlight nhìn cho rõ ràng"*

## Nguyên nhân lỗi click
- **Hình trang trí:** tu sĩ đứng ở tông môn (đệ tử, trưởng lão) chỉ là hình trang trí và không gắn với tu sĩ thật. Lúc mới vào game có **0/184** tu sĩ đang hiển thị dạng thực thể (`IsShownOnMap`), nên mọi hình người thấy trên map đều click không ra gì.
- **Bán kính chọn sai:** phần chọn tìm theo vị trí chân, bán kính 1,5 ô, trong khi sprite cao khoảng 2 ô và tu sĩ ngự kiếm được vẽ nâng lên 0,7 ô. Bấm vào thân người hay đang bay thường bị trượt.
- UI không chặn chuột; đã kiểm tra bằng `EventSystem.RaycastAll`.

## Đã làm
- **Hình ở tông môn gắn với người thật** (`UnitRenderer`):
  - Mỗi hình đệ tử Luyện Khí hoặc trưởng lão ở tông môn giữ tham chiếu `Who` tới một thành viên thật đang ở nhà.
  - Người đó ra ngoài hoặc chết thì hình biến mất. Đột phá hoặc nhập ma thì skin đổi theo.
- **Hit-test theo đúng khung hình đã vẽ:** mỗi frame `UnitRenderer` ghi lại khung của từng thứ đã vẽ: tu sĩ (có tính độ nâng khi bay), dân làng, đoàn di dân, thú.
  - `Pick(điểm, độ lệch)` chọn theo thứ tự ưu tiên tu sĩ, rồi người hoặc di dân, rồi thú; trong cùng nhóm lấy cái gần tâm nhất.
  - Có thêm biên khoảng 6 px màn hình để sprite nhỏ khi thu nhỏ map vẫn bấm trúng.
- **Công cụ Xem chọn được mọi thứ** (`InspectTarget` trong `WorldBootstrap`), theo thứ tự:
  1. Sprite dưới chuột.
  2. Tu sĩ đang ra ngoài, khi map thu nhỏ đến mức không vẽ sprite.
  3. Nhà hoặc tông môn → mở thẻ làng.
  4. Vật thể khác: cây, đá, nhà bỏ hoang.
  5. Ô đất.
- **Các thẻ mới** (`GameUI`):
  - **Đàn thú:** số con trong vùng 64×64, ghi chú "chỉ vẽ tượng trưng", thức ăn và kẻ săn, các loài khác trong vùng, % cỏ, tổng toàn thế giới.
  - **Đoàn di dân:** số người, lương thực mang theo, làng đã rời đi, số tháng đã đi. Có nút Theo dõi.
  - **Vật thể:** tên, kích thước, vị trí, cộng thêm thông tin ô bên dưới.
  - **Ô đất:**
    - Địa hình, độ cao, nhiệt độ theo mùa, độ ẩm, độ màu mỡ.
    - Linh khí hiện tại so với trần, linh mạch.
    - Cỏ của khu 8×8; cảnh báo không đi bộ qua được hoặc mặt nước chết đuối.
    - Ruộng hoặc đất của làng nào; số thú trong vùng.
- **Highlight** (`HighlightRenderer`):
  - Khung chữ nhật có viền tối, nét luôn dày 2–3 px màn hình ở mọi mức zoom.
  - **Trắng** cho thứ đang rê chuột. Ô vuông brush cũ bị ẩn khi đang dùng công cụ Xem.
  - **Vàng nhấp nháy** cho thứ đang chọn, và bám theo khi thứ đó di chuyển.
  - Phạm vi khung theo loại: làng thì bao quanh cả làng; đàn thú thì bao cả vùng 64×64 mà nó sống; tu sĩ đang bế quan không thấy hình thì khung đặt ở tông môn.
- **Gợi ý dưới con trỏ** dùng chung kết quả chọn, nên chữ khớp với khung đang sáng.
- **Theo dõi** chạy cho mọi thứ có vị trí, không chỉ tu sĩ.

## Kiểm chứng
- **Play mode, đầu game:** chọn trúng 8/8 hình tu sĩ ở Quỷ Linh Môn, mở được thẻ Kết Đan trưởng lão. Khung vàng quanh nhân vật, khung trắng khi rê lên ô đất.
- **Kiểm tra từng thẻ:** thú, ô đất, vật thể, làng, tu sĩ đều ra đúng. Thẻ di dân kiểm tra sau 20 năm mô phỏng.
- **EditMode:** 17/17 test vẫn pass.
