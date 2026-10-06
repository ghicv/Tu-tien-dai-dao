# 05 · M3.1 — Trường hợp biên, bất biến thế giới, hiệu ứng

**Ngày:** 2026-10-06

## Lý do
Người chơi vẽ nước đè lên Âm La Tông thì dân và đệ tử vẫn đứng trên mặt nước (ảnh người chơi gửi). Yêu cầu: *"cần phải xử lý hết các trường hợp có thể xảy ra"*, rồi chốt luật: *"nếu biến thành nước thì nhân vật rơi xuống nước chết luôn"*. Thêm yêu cầu: *"thiên phạt các thứ chưa có hiệu ứng cháy nổ, sét đánh"*.

## Luật mới khi đất biến thành nước (xử lý ngay lúc vẽ)
- **Làng hoặc tông môn có tâm bị nhấn chìm:** toàn bộ phàm nhân chết đuối, làng bị xóa sổ. Tông môn tan rã và người sống sót thành tán tu.
- **Tu sĩ đứng trong vùng ngập:** Luyện Khí chết đuối. Từ Trúc Cơ trở lên ngự kiếm bay lên bờ gần nhất. Người đang bay qua thì không sao. Động phủ bị ngập thì dời lên bờ.
- **Đoàn di dân trong vùng ngập:** chết đuối cả đoàn.
- **Thú hoang:** mỗi vùng mất số thú đúng bằng tỉ lệ đất bị ngập.
- **Biến thành núi đá hoặc đỉnh tuyết** (không phải nước): làng dời tới chỗ đất ở được gần nhất. Tông môn chọn chỗ linh khí cao nhất và xây lại đại điện. Không còn chỗ thì dân ly tán sang làng gần nhất.

## Các lỗ hổng khác đã vá
- Tông môn chết vì đói hoặc dịch nhưng tu sĩ vẫn mang `SectId` cũ. Đã chuyển việc giải tán vào `Abandon`, mọi con đường đều đi qua đó.
- Người đi bộ đang đứng trên nước được phép lội ra (trước đây bị kẹt mãi).
- Đang ở điểm du ngoạn mà chỗ đó bị ngập thì về nhà ngay. Trở về mà kẹt đường thì vẫn về tới nhà.
- Nhân vật trang trí (dân, đệ tử, thú): chỉ chọn điểm trên đất liền và chỉ đi đường không cắt qua nước. Đất dưới chân bị đổi thì con đó biến mất rồi xuất hiện lại chỗ hợp lệ.
- Phía hiển thị không bao giờ vẽ người đứng (không bay) trên nước.
- Thả thú xuống biển thì thú mất luôn.

## Bất biến thế giới (`Sim/WorldInvariants.cs`)
Các điều luôn phải đúng:
- Không làng nào nằm trên đất không ở được.
- Nhà và ruộng khớp với làng sở hữu, không ô nào thuộc về làng đã chết.
- Lương thực và các nhóm tuổi không âm.
- Tu sĩ liên kết đúng với entity của mình; không ai ở nhà trên nước; không ai thuộc tông môn đã chết.
- Số đếm theo cảnh giới khớp với thực tế; đoàn di dân không lang thang mãi; thú hoang không âm hay NaN.

## Hiệu ứng (`Render/FxRenderer.cs`)
- Sự kiện mang thêm vị trí và loại hiệu ứng (`Fx`). FX renderer đọc các sự kiện mới và phát theo thời gian thực, vẫn chạy khi đang tạm dừng.
- Sprite sinh bằng code:
  - Tia sét lởm chởm có nhánh, 2 hình nhấp nháy xen kẽ.
  - Chớp sáng, vòng xung kích, tia lửa, khói, lửa 3 frame.
  - Mây giông, cột sáng, giọt nước.
- Gắn hiệu ứng vào sự kiện:
  - Thiên phạt: sét kèm chớp, tia lửa, khói và lửa cháy khoảng 2 giây.
  - Thiên kiếp: mây đen và 7 tia sét liên tiếp. Chết dưới kiếp: sét.
  - Đột phá: cột sáng vàng. Tẩu hỏa nhập ma: nổ tím.
  - Chết đuối, làng bị nhấn chìm: bọt nước. Ban cơ duyên: hạt sáng vàng bay lên.
- Thiên phạt luôn có sét đánh tại chỗ click: thiêu cây quanh đó, làng gần đó có người chết; trúng tu sĩ thì người đó hồn phi phách tán.

## Kiểm chứng
- **Chaos test:** 60 đợt phá ngẫu nhiên, gồm biến thành nước nông, biển sâu, núi, sa mạc; xóa nhà; thiên phạt; thả sói; lập làng; vẽ và phá linh mạch. Sau mỗi đợt chạy thêm 60 ngày, cuối cùng mọi bất biến đều đúng.
- Chaos chạy 2 lần với cùng seed cho cùng hash (vẫn deterministic).
- `FloodDrownsTheSectButFlyersEscape`: Luyện Khí chết, Trúc Cơ trở lên sống và đứng trên đất khô.
- `VillageUnderARaisedMountainMovesAway`: làng dời đi.
- 17/17 test pass. Ảnh: `Docs/m3_1_fx.png`.

## Tiếp theo
Làm lại UI theo kiểu WorldBox (người chơi yêu cầu): thanh công cụ ở đáy chia tab và có icon, thanh trên gọn, các bảng chỉ mở khi cần.
