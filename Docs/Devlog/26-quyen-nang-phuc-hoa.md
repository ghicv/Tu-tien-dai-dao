# 26 · Quyền năng còn thiếu: thả tu sĩ, hung thú, phúc họa, thiên tài địa bảo, bí cảnh

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"làm tiếp phần quan trọng tiếp theo"*.
- Theo GDD §14, tab Sinh linh, Linh khí và Phúc/Họa còn thiếu một nửa quyền năng.
- Đây là phần ảnh hưởng trực tiếp nhất tới Player Agency: có thêm nhiều cách để "nếu… thì sao?".
- Mỗi quyền năng đều đi vào các hệ có sẵn chứ không đứng riêng (GDD §0.0).

## Đã làm

| Quyền năng | Tab | Đọc / đổi hệ nào | Hậu quả thấy được |
|---|---|---|---|
| **Thả tu sĩ** (chọn Luyện Khí … Hóa Thần) | Sinh linh | Tạo một tán tu thật (`CultivationSystem.Descend`). Chỗ bấm thành động phủ; linh căn, ngộ tính, dã tâm bốc ngẫu nhiên | Tự tu luyện, du ngoạn, kết thù, thu đồ đệ, dã tâm lớn thì lập tông môn. Hóa Thần xé rách hư không ngay |
| **Đánh thức hung thú** (ngũ đến cửu giai) | Sinh linh | `BeastHorde.RaiseHungThu` | Tàn sát từ thành này sang thành khác, các tông môn lập liên minh trảm yêu hoặc đóng chặt sơn môn |
| **Thả yêu thú** (giờ chọn được nhất đến cửu giai) | Sinh linh | `BeastSystem.Spawn` | Chiếm lãnh địa, săn thú, tập kích làng; tu sĩ tới săn yêu đan |
| **Ban pháp bảo** | Phúc/Họa | `Treasures`, `TreasureName` | Sức chiến đấu +12%, dễ vượt thiên kiếp hơn. Ma tu "giết người đoạt bảo" thì cướp được pháp bảo |
| **Giáng tâm ma** | Phúc/Họa | `DaoHeart` −0,45, tu vi đang tích lũy mất một nửa | Đạo tâm càng yếu càng dễ bị tâm ma thắng ngay. Lúc đó: chết (25%), tụt một cảnh giới (35%) hoặc sa vào ma đạo (40%). Không sao thì vẫn yếu hơn, đột phá khó hơn, dễ tẩu hỏa về sau |
| **Phế tu vi** | Phúc/Họa | Tụt trọn một đại cảnh giới (Luyện Khí thì mất hết các tầng) | Sức, máu, thọ nguyên giảm theo. Người đã quá thọ của cảnh giới dưới thì sắp tọa hóa. Rớt xuống Luyện Khí thì không ngự kiếm được, phải đi bộ về |
| **Thiên tài địa bảo** | Linh khí | `RelicSystem.PlaceTreasure`. Linh khí chỗ bấm càng đậm thì phẩm càng cao (2–5) | Bảo quang xung thiên, các tông môn kéo đến đấu pháp tranh đoạt. Bảo vật có ích cho tông môn hoặc tu sĩ giành được |
| **Mở bí cảnh** | Linh khí | `RelicSystem.OpenSecretRealm`: thượng cổ di tích ba tầng, có pháp bảo, linh thạch, đan dược | Tranh đoạt, rồi các tu sĩ vào thám hiểm từng tầng (có thể chết trong đó) |

### Chọn cấp
- Các công cụ thả sinh linh dùng ô **+ / −** (hoặc phím `[` `]`) để chọn cảnh giới hoặc giai, thay cho cỡ cọ.
- Ô hiển thị tên cấp ("Kết Đan", "lục giai"); con trỏ hiện gợi ý "Thả tu sĩ · Kết Đan".

### Thẻ thông tin
- Các nút quyền năng chuyển xuống một hàng riêng, gồm 8 nút: phúc trước, họa sau.
- **Thẻ tu sĩ** có thêm Ban pháp bảo, Giáng tâm ma, Phế tu vi.
- Khi rê chuột, gợi ý ghi rõ đối tượng, kèm đạo tâm hiện tại khi chọn Giáng tâm ma.

### Hình ảnh (pixel art)
- **Biểu tượng mới:** trái tim tím có vết nứt (tâm ma), viên kim đan nứt đôi (phế tu vi).
- **Dùng lại:** kiếm (pháp bảo), sprite thiên tài địa bảo và thượng cổ di tích.

### Kỹ thuật
- Những gì Thiên Đạo đặt xuống luôn đứng được, không bị giới hạn 120 bí cảnh mở (`RelicSystem.Add(divine)`).
- Mọi quyền năng đi qua `IWorldCommand`, ghi vào nhật ký lệnh như cũ. RNG tách theo vị trí và chỉ số, nên lịch sử khi không dùng quyền năng không đổi.

## Kết quả
- **Test EditMode:** 56/56. Test mới `ThienDaoPhucHoaPowersChangeTheWorld` kiểm cả 8 quyền năng: thả tu sĩ, ban pháp bảo, tâm ma, phế tu vi, hung thú, thiên tài địa bảo, bí cảnh; rồi cho thế giới chạy 2 năm và kiểm bất biến.
- Không đổi mô phỏng khi người chơi không dùng quyền năng, nên cân bằng và hiệu năng giữ như devlog 25.

## Còn lại (đề xuất tiếp)
- Cầu nguyện và tín ngưỡng: làng gặp nạn cầu Thiên Đạo, đáp hay không đáp đều có hậu quả.
- Thiên mệnh: mục tiêu mềm do thế giới tự đặt ra.
- Đổi mùa, ban thần thông (cần hệ công pháp, thần thông trước).
