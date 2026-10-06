# 03 · M2.1 — Thú hoang thành quần thể, hiển thị tượng trưng

**Ngày:** 2026-10-06 · **Commit:** `ee83767`

## Lý do
Người chơi: *"map nhỏ mà quá nhiều sinh vật thì nó k đúng"*, rồi làm rõ: *"số lượng có thể nhiều, nhưng hiển thị trên map thì ít thôi"*. Khoảng 12.000 cá thể vừa trông sai vừa tốn khoảng 2,8 ms/tick.

## Đã làm
- `Sim/WildlifeSystem`: hươu, thỏ, sói là con số trên 16×16 vùng (mỗi vùng 64×64 ô). Mỗi tháng:
  - Ăn cỏ theo hốc sinh thái: hươu ăn lá rừng, thỏ gặm cỏ đồng.
  - Thiếu cỏ thì chết đói; sinh sản vào Xuân và Hạ.
  - Sói săn theo mô hình bão hòa; 4% mỗi quần thể lan sang vùng bên; làng săn một ít.
- `CreatureSystem` chỉ còn lo cá thể thật (di dân, sau này tu sĩ).
- `UnitRenderer`: mỗi vùng trong khung nhìn vẽ tối đa 3 hươu, 3 thỏ, 2 sói đi lang thang.

## Lỗi gặp và cách sửa
- Hươu tuyệt chủng vì thỏ lấn át → chia phần thức ăn theo độ phù hợp môi trường của vùng.
- Hươu vẫn giảm dần vì gần 50 làng săn → giảm mức săn hươu, tăng tỉ lệ sinh.

## Kiểm chứng
- 3 seed × 40 năm: cả 3 loài cùng tồn tại; hươu khoảng 300–850, thỏ 6–12 nghìn, sói 100–250.
- 0,05 ms/tick, nhanh hơn khoảng 60 lần. Bộ test từ 130 giây xuống 31 giây.
