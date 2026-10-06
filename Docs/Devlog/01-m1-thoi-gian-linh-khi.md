# 01 · M1 — Thời gian & linh khí

**Ngày:** 2026-10-05 · **Commit:** `a9d21c4`

## Mục tiêu
Thế giới bắt đầu "sống": có thời gian, mùa, linh khí lan, cạn, hồi; mọi quyền năng đi qua hàng đợi lệnh, deterministic.

## Đã làm
- `Core/SimClock`: 1 tick là 1 ngày; tháng 30 ngày, năm 360 ngày, 4 mùa; 5 mức tốc độ (tới 10 năm/giây); ngân sách 8 ms/frame.
- `Sim/QiSystem`: linh khí hiện tại trên lưới thô 128×128.
  - Mỗi tháng khuếch tán 18% và hồi 6% khoảng cách tới trần; hệ số hồi theo mùa.
  - Linh khí từng ô bằng trần của ô cộng phần thừa/thiếu nội suy từ các khối xung quanh.
- `World/QiCap`: trần linh khí bằng nền cộng ảnh hưởng linh mạch; chỉ tính lại vùng bị sửa.
- `Sim/WorldCommands` + `Simulation`: mọi brush thành lệnh, áp dụng giữa các tick và ghi log để replay.
- Renderer chỉ nghe sự kiện từ `WorldData`. Màu map đổi theo mùa.
- Tách assembly `ThienDao` (+ Editor, Tests); thêm 7 EditMode test.

## Lỗi gặp
- `OnTerrainChanged` trùng tên message có sẵn của MonoBehaviour, gây "Script error"; đổi thành `Handle*`.

## Kiểm chứng
- Test: cùng seed thì cùng hash; cùng chuỗi lệnh thì cùng trạng thái; replay từ log khớp.
- Vùng bị hút từ 0 hồi về đủ mức sau khoảng 3 năm game.
- 3,5 năm game chạy mất 24 ms.
