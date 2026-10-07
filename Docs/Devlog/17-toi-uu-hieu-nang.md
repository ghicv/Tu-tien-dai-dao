# 17 · Tối ưu hiệu năng

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"oke tối ưu trước rồi làm tiếp đi bro"*.
- Lấy mốc từ lần đo ở [16](16-luu-tai-va-hieu-nang.md): năm 1000 tốn 148 ms cho mỗi năm mô phỏng. Ba hệ nặng nhất là Di chuyển (44,6), Làng (41,8) và Tu sĩ (29).
- Mỗi texture chunk địa hình giữ thêm một bản sao trên CPU.

## Đã làm
- **Lưới tra cứu vị trí** (`CreatureSystem`):
  - Trước đây dựng lại mỗi ngày. Giờ chỉ dựng ở lần tra cứu đầu tiên trong ngày, dùng `PrevX/PrevY` (chỗ mọi người đứng lúc đầu ngày) để kết quả y như cũ.
  - Phần lớn các ngày không ai tra cứu, nên gần như không phải dựng lại.
- **Ruộng của làng** (`SettlementSystem`):
  - Trước đây, tháng nào cũng kiểm tra lại mọi ô ruộng (khoảng 55 nghìn ô cuối game) và cộng lại độ màu mỡ.
  - Giờ chỉ kiểm tra lại khi địa hình trong bán kính 40 ô quanh làng thay đổi (`HandleTerrainChanged` đặt cờ `FarmsCheck`).
  - Tổng độ màu mỡ được cache, chỉ tính lại khi ruộng hoặc số ô được canh tác thay đổi. `LoseField` (thiên tai) cũng làm mới cache.
  - Cache cho kết quả chính xác: cùng thứ tự cộng, cùng kết quả.
- **Texture chunk chỉ nằm trên GPU** (`WorldRenderer`):
  - Chunk được vẽ vào một texture tạm dùng chung, rồi `Graphics.CopyTexture` sang chunk; chunk bỏ bản sao CPU (`Apply(false, true)`).
  - Mỗi chunk giảm từ khoảng 680 KB xuống 340 KB; với bộ đệm 420 chunk là từ khoảng 290 MB xuống 143 MB.
  - GPU không hỗ trợ sao chép texture thì giữ cách cũ.
  - Đã chạy đúng trên máy người chơi (GeForce GT 730).
- **Job nền trong Editor** (`Editor/EditorJob.cs`):
  - Menu đo hiệu năng và xuất biên niên giờ chạy từng lát trên `EditorApplication.update`, nên menu trả về ngay.
  - Trước đây MCP chờ quá lâu, timeout rồi gửi lại lệnh, khiến một lần bấm đo chạy tới 7 lượt.

## Kết quả (`Docs/Perf_ThienDao.md`, seed ThienDao, 1000 năm)

| Năm | Trước (ms/năm) | Sau (ms/năm) |
|---|---|---|
| 100 | 42,6 | 31,9 |
| 500 | 119 | 80,6 |
| 1000 | 148 | **101** (giảm 32%) |

- **Theo từng hệ ở năm 1000:**

  | Hệ thống | Trước (ms/năm) | Sau (ms/năm) |
  |---|---|---|
  | Di chuyển | 44,6 | 24,3 |
  | Làng | 41,8 | 14,3 |

- **Lịch sử y hệt bản trước:** mọi thế kỷ cùng số tu sĩ, cùng số phàm nhân, cùng số dòng sử sách. Tối ưu không làm thay đổi mô phỏng.
- **Tua:** cuối game giờ đạt khoảng 4,7 năm/giây, đầu game khoảng 15 năm/giây (ngân sách 8 ms mỗi khung hình).
- **Còn nặng nhất:** Tu sĩ (29 ms/năm, vì mỗi tu sĩ hút linh khí trên vùng 9×9 ô mỗi tháng), Di chuyển, Làng, Thế lực. Nếu cần nhanh hơn nữa: cập nhật so le theo nhóm (staggered update), hoặc chạy mô phỏng trên luồng riêng.

## Kiểm chứng
- 50/50 test pass (có cả test tất định, replay, lưu/tải).
- Play mode: địa hình vẽ đúng với texture chỉ trên GPU.
