# 02 · M2 — Sinh mệnh cơ bản

**Ngày:** 2026-10-05 · **Commit:** `d808dc5`

## Mục tiêu
Phàm nhân và động vật ăn, di chuyển, sinh con, chết; dân số làng tự tăng giảm theo tài nguyên.

## Đã làm
- `Sim/SettlementSystem`: làng là quần thể gồm 17 nhóm tuổi, lương thực, nhà và ruộng (địa hình `Farmland`, có chủ sở hữu theo ô).
  - Mỗi tháng: thu hoạch theo độ màu mỡ và mùa, săn bắn, chết đói, mở hoặc bỏ ruộng.
  - Mỗi năm: già đi, chết, sinh con, xây hoặc bỏ nhà, tách đoàn di dân lập làng mới.
- Đoàn di dân là cá thể di chuyển thật trên map.
- Động vật ban đầu là cá thể (sau đó đã đổi sang quần thể, xem devlog 03).
- `Render/UnitRenderer`: mọi sinh vật vẽ trong 1 mesh động, có nội suy giữa các tick; dân làng đi lại chỉ để trang trí.
- Tên theo Phàm Nhân Tu Tiên (`Sim/Lore.cs`): tông môn dùng tên trong truyện; địa danh có hậu tố Thôn / Trấn / Thành theo dân số.

## Lỗi gặp và cách sửa
- Thỏ ăn trụi cỏ khiến hệ sinh thái sụp → ăn công bằng, cỏ ×3, sinh sản giới hạn theo mật độ.
- Sói không bao giờ sinh sản được: khi đếm mật độ, nó tính luôn chính mình.
- Truy vấn tìm mồi tăng theo bình phương khi thú dồn đàn, làm Unity treo vài phút → giới hạn 48 ứng viên mỗi truy vấn.
- Săn bắn quá hời, làng không cần ruộng vẫn đủ ăn → tách riêng lượng lương thực cho người săn.

## Kiểm chứng
- Làng bị biến thành sa mạc sụt từ 32 xuống 22 người, làng đối chứng tăng lên 56.
- Có di dân lập làng mới: từ 39 lên 47 làng. 10/10 test pass.
