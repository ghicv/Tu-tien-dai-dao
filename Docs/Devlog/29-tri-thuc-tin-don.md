# 29 · Tri thức và tin đồn

**Ngày:** 2026-10-08

## Lý do
- Người chơi chọn mục tiếp theo trong `Docs/WorldRuleSystem.md`: luật N7, tri thức và tin đồn.
- `WorldRules.md` §6 yêu cầu NPC hành động theo **Knowledge**. Trước đây ai cũng "biết" mọi bí cảnh đã lộ và mọi hung thú, ở bất cứ đâu trên thế giới.

## Đã làm (`Sim/KnowledgeSystem.cs`)

### Tin đồn
- **Những gì có tin đồn:** bí cảnh hay linh vật, hung thú, tông môn suy yếu.
- **Hình dạng:** mỗi tin đồn là vài vòng tròn trên bản đồ, tối đa 8 vòng.
- **Bắt đầu từ nơi sự việc xảy ra:**

| Sự việc | Biết ngay trong bán kính |
|---|---|
| Linh vật xuất thế, Thiên Đạo giáng thiên tài địa bảo (bảo quang xung thiên) | 100 ô |
| Thiên Đạo mở bí cảnh | 70 ô |
| Có người tình cờ phát hiện bí cảnh | 12 ô quanh bí cảnh, và 10 ô quanh nhà người đó |
| Hung thú xuất thế | 50 ô; mỗi thành bị tàn sát thêm một vòng 40 ô (dân chạy loạn mang tin đi) |
| Tông môn thua trận đẫm máu, hoặc tông chủ chết | 20 ô quanh sơn môn, nhớ 10 năm |

- **Loang dần mỗi tháng:** 10 ô/tháng ở nơi hoang vắng, tới 34 ô/tháng ở vùng đông làng. Tin đi theo người.
- **Nhảy theo đường:**
  - Thương đội tới nơi thì mang tin của thành mình đi tới thành kia.
  - Đoàn di dân lập làng mới cũng mang tin của vùng cũ theo.
  - Mỗi lần được kể lại, tin **bị thổi phồng** ×1,15, tối đa gấp 2,5 lần.
- **Bị quên:** tin bí cảnh nhớ 100 năm, hung thú 50 năm; hết hạn, hoặc bí cảnh cạn và hung thú chết, thì tin mất.

### Chỉ ai nghe tin mới hành động
- **Tranh đoạt cơ duyên:**
  - Chỉ tu sĩ mà tin đã tới nơi họ ở mới được tính khi tranh đoạt bắt đầu.
  - **Tông nghe muộn thì kéo tới muộn:** tin lan tới sơn môn khi cuộc tranh đoạt chưa kết thúc (giờ kéo dài 60 ngày) thì tông đó cử tối đa 3 người tới. Họ đến từ xa hơn bình thường (gấp 1,5 lần tầm).
  - **Chưa ai tranh mà đủ người nghe:** cuộc tranh đoạt bắt đầu ngay. Tin bị thổi phồng có thể biến một bí cảnh tầm thường thành thứ đáng tranh: phẩm cấp × mức thổi phồng ≥ 3.
- **Thám hiểm bí cảnh:** người được chọn đi thám hiểm phải đã nghe tin, kể cả nhân vật chính.
- **Liên minh trảm yêu:** chỉ những tông đã nghe về các vụ tàn sát mới được gọi vào. Hung thú càng tàn sát nhiều nơi, tin dữ càng lan xa, liên minh càng đông.
- **Tông môn suy yếu:** tông mạnh hơn ở gần (trong vòng 300 ô, không phải đồng minh) nghe tin thì **bắt đầu dòm ngó**. Thiện cảm giảm 12 × mức thổi phồng, nên dễ dẫn tới chiến tranh: kẻ mạnh đánh lúc đối phương yếu.

**Chuỗi phản ứng mẫu:** tông A thua trận đẫm máu → tin "A suy yếu" lan ra → tông B ở gần nghe được, dòm ngó → B tuyên chiến → A mất thêm người → tin lan xa hơn → tông C nghe được, cũng nhảy vào.

### Hiển thị
- **Vòng tin đồn trên map:** khi thẻ đang hiện một bí cảnh hoặc một hung thú, mép của mọi vùng tin đã tới được vẽ thành vòng chấm pixel màu vàng nhạt, chạy chậm quanh vòng. Nhìn vào là thấy ai đã biết.
- **Thẻ bí cảnh:** "Tin đồn đã lan tới N tông môn: A, B, C…", kèm "đồn thổi gấp ×1,6 lần" nếu có.
- **Thẻ hung thú:** chip "N tông", tooltip ghi các tông đã nghe tin dữ.
- **Thẻ tông môn:** dòng "Nghe đồn: …" liệt kê bí cảnh, hung thú hay tông suy yếu mà sơn môn đã nghe.
- **Sử sách:**
  - "Tin X truyền tới tông Y, bị đồn thổi gấp 1,6 lần; 3 người kéo tới tranh đoạt muộn."
  - "Tông Y nghe tin X suy yếu, bắt đầu dòm ngó."

### Hiệu năng
- **Không lưu theo từng người:** tối đa 48 tin đồn, mỗi tin tối đa 8 vòng.
- **Mỗi tháng:** nới bán kính các vòng, rồi xét sơn môn nào vừa nằm trong vòng.
- **Hỏi "ai đã biết?"** chỉ tốn vài phép tính khoảng cách.

## Kết quả
- **Test EditMode:** 59/59. Test mới `WordSpreadsOutwardAndAlongTheRoadsGrowingInTheTelling`:
  - Linh vật vừa xuất thế thì gần biết, xa chưa biết.
  - Tin loang ra theo tháng.
  - Thương đội mang tin tới thành xa, và tin bị thổi phồng.
- **Cân bằng (1000 năm, seed ThienDao):**
  - Phàm nhân 34,3 nghìn, tu sĩ còn sống 932 (Trúc Cơ 389, Kết Đan 30, Nguyên Anh 1), không lệch so với devlog 28.
  - 15 lần một tông nghe tin muộn rồi kéo tới tranh đoạt.
  - 277 lần một tông nghe tin láng giềng suy yếu và bắt đầu dòm ngó, khoảng một lần mỗi 4 năm: đủ làm mầm chiến tranh mà không thành spam.
  - Lúc nào cũng có 0–5 tin đồn đang lan.
- **Hiệu năng:**
  - Hệ Tin đồn tốn 0,1–0,3 ms/năm. Tổng năm 1000 là 127 ms/năm.
  - Hệ Di chuyển (50 ms) vẫn là điểm nóng lớn nhất.
- **Ghi chú kỹ thuật:** sự kiện mức quan trọng 1 không gắn tu sĩ thì không vào sử sách (`EventLog`), nên "dòm ngó" để ở mức 2. Đây là mầm của chiến tranh, cần hiện trong sử tông môn.
