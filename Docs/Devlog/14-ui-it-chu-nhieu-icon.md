# 14 · UI: ít chữ, nhiều icon

**Ngày:** 2026-10-06

## Lý do
Người chơi: *"sửa lại toàn bộ UI cho t, ít chữ thôi, nhiều icon vào"*, với mục tiêu *"dễ nhìn, dễ hiểu, dễ tương tác"*.

Nguyên tắc chung:
- Con số luôn đi cùng một icon.
- Tên đầy đủ và giải thích để trong tooltip (rê chuột vào là thấy).
- Chỉ hiện cái đang có ý nghĩa, ví dụ cảnh báo chỉ xuất hiện khi có chuyện.

## Đã làm

### Thành phần dùng chung
- **`Ui.Chip`:** icon + giá trị ngắn + tooltip. Chữ dài tự co nhỏ cho vừa ô, không tràn sang chip bên cạnh.
- **`Ui.Icon`:** một ảnh icon đứng riêng.
- **Icon pixel mới:**
  - Các mùa: hoa (Xuân), mặt trời (Hạ), lá (Thu), bông tuyết (Đông).
  - Chỉ số: đồng hồ cát, trái tim, kiếm, linh thạch, đan, bát cơm.
  - Thao tác: tâm ngắm (camera), dấu trang (theo dõi), xúc xắc, nhãn tên, mũi tên hoàn tác.
  - `Icons.ForEvent` chọn icon cho mỗi tin theo hiệu ứng (động đất, núi lửa, lũ, dịch…) rồi theo loại tin (đột phá, chết, chiến tranh, lập tông…).
  - `Icons.Cultivator` / `ForRealm` vẽ hình tu sĩ theo cảnh giới, tu sĩ ma đạo có hình riêng.

### Góc trên trái
- **Ngày tháng:** icon mùa + "Năm 12 · tháng 3". Ngày cụ thể để trong tooltip.
- **Dòng số liệu chữ thay bằng 6 chip:** dân, làng, tu sĩ, Nguyên Anh+, thế lực, chiến tranh. Hai chip cuối đổi màu khi có số đáng chú ý.
- **Hàng cảnh báo** chỉ hiện khi có chuyện: đại kiếp (số năm còn lại), vùng hạn, ổ dịch, mưa/rét, quy luật đã bị sửa.
- **Dòng tin:** mỗi tin có icon theo loại ở đầu dòng; tin về nhân vật đang theo dõi dùng icon ngôi sao.

### Thanh công cụ
- **Dòng nhỏ phía trên** cho biết đang ở đâu và đang cầm gì, ví dụ "THIÊN TAI › Núi lửa".
- **Ô cỡ cọ mờ đi** khi công cụ không dùng cọ (Xem, quyền năng nhắm vào một người, núi lửa, ôn dịch, đại kiếp). Ô vẫn giữ chỗ để thanh công cụ không nhảy kích thước.
- **Tab Thế giới:** ba nút chữ đổi thành icon (quả địa cầu: tạo lại thế giới, xúc xắc: seed ngẫu nhiên, nhãn: hiện/ẩn tên).
- **Tab Quy luật:** mỗi ô là icon + giá trị + nút −/+; giá trị khác mặc định hiện màu đỏ cam; nút hoàn tác để trả mọi luật về mặc định.

### Thẻ nhân vật / làng / tông môn
- **Đầu thẻ:** hình đại diện (tu sĩ theo cảnh giới, nhà dân, đại điện, thú, ô đất) + tên + một dòng phụ (chức vị · tông môn · đang làm gì).
- **Lưới chip 3 cột thay cho đoạn chữ dài:**
  - Tu sĩ: cảnh giới, linh căn, linh khí nơi ở (đỏ nếu không đủ), ngộ tính, tâm cảnh, khí vận, số mạng đã giết, linh thạch, đan, pháp bảo, số lần đột phá thất bại, truyền kỳ.
  - Người đã chết: năm vẫn lạc, kẻ đã giết.
  - Làng: dân, nhà, ruộng, lương thực (số tháng), sinh / mất năm qua, có dịch, đang hạn.
  - Tông môn: thêm số tu sĩ theo từng cảnh giới, lãnh thổ, linh mạch, linh thạch, thực lực.
- **Hai thanh có icon và số:**
  - Tu vi: tiểu cảnh giới + %, ví dụ "viên mãn 74%".
  - Tuổi: "390/500".
- **Tiểu sử rút còn 4 dòng gần nhất**, kèm "+N sự tích (H)" để mở Biên niên sử.
- **Nút toàn icon:** camera (tâm ngắm), theo dõi (dấu trang), 4–5 quyền năng, hồi sinh.

### Cửa sổ
- **Thống kê:** lưới 20 chip (dân, làng, đoàn di dân, tu sĩ theo 5 cảnh giới, thế lực, chiến tranh, diệt môn, hươu, thỏ, sói, hạn, dịch, mưa/rét, lôi địa, núi lửa, tổng số thiên tai). Phần chữ chỉ còn ô đất dưới con trỏ và thông số kỹ thuật.
- **Sự kiện:** 8 dòng, mỗi dòng có icon theo loại tin.
- **Bảng cường giả và danh sách theo dõi:** hình tu sĩ theo cảnh giới ở đầu mỗi dòng; người đã chết hiện đầu lâu.

## Kiểm chứng
- Xem trong Play mode, ảnh: `Docs/ui_moi.png` (thẻ tông môn, cửa sổ Sự kiện, tab Quy luật, HUD và hàng cảnh báo).
- **Các lỗi tìm thấy khi xem và đã sửa:**
  - Chữ cảnh giới dài tràn sang chip bên cạnh: chip giờ chỉ ghi tên đại cảnh giới, tiểu cảnh giới chuyển sang thanh tu vi, chữ tự co cho vừa ô.
  - Hai nút ngôi sao (theo dõi / ban cơ duyên) dễ nhầm: nút theo dõi đổi thành dấu trang.
  - Nút −/+ trong ô quy luật quá nhỏ: đổi thành chữ to.
  - Ký tự "↺", "⇄" không chắc có trong font: thay bằng icon pixel.
  - Các dòng trống trong cửa sổ Sự kiện hiện ô trắng: giờ ẩn cho tới khi có tin.
- 42/42 test vẫn pass.

## Còn để sau
- Biên niên sử và cửa sổ Thế lực vẫn là văn bản dài (bản chất là sử sách); có thể thêm icon theo loại sự kiện cho từng dòng.
- Chưa có phím tắt cho từng tab công cụ.
