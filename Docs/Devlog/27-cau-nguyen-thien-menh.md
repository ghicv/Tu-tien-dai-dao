# 27 · Cầu nguyện, tín ngưỡng và Thiên mệnh

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"oke làm tiếp cầu nguyện với thiên mệnh đi bro"*.
- Hai trụ cột còn yếu (GDD §0.0):
  - **Soft Goals:** người chơi chưa có mục tiêu mở nào để theo.
  - **Meaningful Consequences:** việc Thiên Đạo đáp hay không đáp lời chúng sinh chưa để lại dấu vết gì.

## Đã làm

### Tín ngưỡng (`Settlement.Faith`, 0–100, khởi đầu 30)
- **Tăng khi:**
  - Thiên Đạo đáp lời cầu nguyện: +25. Các làng trong vòng 40 ô cũng +5, vì tiếng lành đồn xa.
  - Ban phúc cho làng khi làng không cầu gì: +5.
- **Giảm khi:**
  - Lời cầu nguyện quá hạn mà không được đáp: −15.
  - Bị giáng thiên phạt: −10, vì sợ hãi và oán trời.
- **Trôi dần về 30** mỗi năm; làng có miếu thì trôi về 45.
- **Hậu quả (mỗi năm):**

| Tín ngưỡng | Điều xảy ra |
|---|---|
| ≥ 60 | Dựng **miếu thờ Thiên Đạo**, kể cả xóm nhỏ chưa đủ cỡ để có miếu |
| ≥ 80 | 3% mỗi năm: trời giáng phúc, một người trong làng **thức tỉnh linh căn** (khí vận cao, bái nhập tông môn gần nhất) |
| < 15 | **Bỏ hoang miếu**, và không dựng lại khi làng lớn lên |
| ≤ 8 | 1,5% mỗi năm (nếu luật cho phép ma đạo): dân thờ tà thần, một người **theo tà giáo thành ma tu** |

### Cầu nguyện (`Sim/FaithSystem.cs`)
- **Ba điều kiện làng gặp nạn sẽ cầu nguyện** (lấy từ các hệ có sẵn):
  - Hạn hán còn từ 2 tháng trở lên: **cầu mưa**.
  - Đang có ôn dịch: **cầu trừ ôn dịch**.
  - Lương thực không đủ một tháng: **cầu mùa màng**.
- **Hung thú đang kéo tới** (con mồi kế tiếp của nó): **cầu trừ hung thú**.
- **Ai cầu và khi nào:**
  - Tín ngưỡng càng cao càng dễ cầu: xác suất 10% + 50% × tín ngưỡng.
  - Mỗi làng tối đa một lời mỗi hai năm. Cả thế giới có tối đa 8 lời đang chờ.
  - Tông môn không cầu.
- **Hạn chờ:** 6 tháng, riêng cầu trừ hung thú 4 tháng.
- **Nhiều cách đáp lời** (Player Agency):
  - **Mưa** của Thiên Đạo (tab Thời tiết) đáp lời cầu mưa và cầu mùa trong vùng mưa.
  - **Ban cơ duyên cho làng:** kho đầy lại, dịch bệnh tiêu tan (`DisasterSystem.Cure`), đáp mọi lời cầu của làng, ngoại trừ lời cầu diệt hung thú.
  - **Thiên phạt** giết được hung thú thì đáp lời các làng đã cầu trừ nó. Tính cả khi nó chết vì sét lan, thiên tai do Thiên Đạo gây ra, hay bất cứ thứ gì chết trong một lệnh của Thiên Đạo (`FaithSystem.Heaven`).
- **Nạn tự qua thì không ai nợ ai:** hạn hán hết, dịch tàn, hung thú bỏ đi thì lời cầu tự đóng, tín ngưỡng không đổi.
- **Tu sĩ giết hung thú trước:** dân cảm ơn tu sĩ đó ("cho là Thiên Đạo phái người tới"), tín ngưỡng chỉ +3.

### Thiên mệnh (`Sim/DestinySystem.cs`)
- **Luôn có 3 mục tiêu mềm** do thế giới tự gợi ý từ tình hình hiện tại:

| Thiên mệnh | Điều kiện sinh | Thành | Không thành | Thiên uy |
|---|---|---|---|---|
| Trừ khử hung thú X trước khi nó tàn sát thêm 3 thành | Có hung thú đang tàn sát (luôn được ưu tiên) | Nó chết | Nó tàn sát thêm 3 thành, hoặc hết 30 năm | 3 |
| Đáp lời 3 lời cầu nguyện | Luôn có thể sinh | Đáp đủ 3 lời | Hết 40 năm | 2 |
| Đưa tu sĩ X lên cảnh giới kế | Tu sĩ Trúc Cơ đến Nguyên Anh có tư chất tốt nhất | Người đó lên cảnh giới kế | Người đó chết, hoặc hết 150 năm | 3 / 4 / 6 |
| Giữ tông môn X trụ vững 50 năm | Tông môn yếu nhất đang có chiến tranh | Còn đứng sau 50 năm | Bị diệt | 3 |
| Phàm nhân thiên hạ đạt N | Luôn có thể sinh (N = dân số hiện tại + 25%) | Đạt N | Hết 100 năm | 2 |
| N nơi thành tâm thờ phụng (tín ngưỡng ≥ 60) | Luôn có thể sinh | Đạt N | Hết 60 năm | 2 |

- **Không bắt buộc:** người chơi theo hay không, theo cách nào cũng được (Soft Goals).
- **Sử sách ghi lại** khi Thiên mệnh mới xuất hiện, khi thành, và khi không thành.
- **Thiên uy** cộng dồn qua mỗi lần thành, là con số tiến trình của người chơi (Progression).

### Hiển thị
- **Cửa sổ Thiên mệnh** (nút ngôi sao, góc trên bên phải):
  - Dòng đầu: Thiên uy, số Thiên mệnh đã thành và không thành, hai kết quả gần nhất.
  - Mỗi Thiên mệnh có tiến độ ("nó đã tàn sát thêm 1/3 thành"), số năm còn lại và phần thưởng.
  - Danh sách lời cầu nguyện đang chờ, kèm gợi ý cách đáp.
  - Bấm một dòng thì camera lia tới nơi đó.
- **Trên map:**
  - Làng đang cầu có cột khói hương pixel bốc lên và đốm vàng lấp lánh.
  - Nhãn vàng phía trên làng, ví dụ "Cầu mưa · còn 4 tháng".
- **Thẻ làng:**
  - Chip tín ngưỡng: vàng khi ≥ 60, tím khi < 15. Tooltip giải thích các ngưỡng.
  - Chip lời cầu đang chờ, kèm cách đáp.
- **Sử sách:** hai loại sự kiện mới, Tín ngưỡng (biểu tượng lư hương pixel) và Thiên mệnh (ngôi sao). Bấm vào sự kiện thì camera lia tới.

## Kết quả
- **Test EditMode:** 57/57. Test mới `PrayersAreAnsweredOrIgnoredAndHeavenOffersDestinies`:
  - Có Thiên mệnh ngay từ đầu.
  - Hai làng gặp hạn hán đều cầu mưa. Mưa đáp lời một làng; làng còn lại bị làm ngơ thì lời cầu quá hạn và tín ngưỡng giảm.
- **Cân bằng (1000 năm, seed ThienDao, người chơi không can thiệp):**
  - Phàm nhân 30,1 nghìn, tu sĩ còn sống 816, yêu thú 51, ma tu 34.
  - Thế giới tự cầu nguyện 1.176 lần. 334 lời quá hạn, số còn lại tự qua vì nạn hết trước hạn.
  - Không làng nào rơi xuống ≤ 8 (tín ngưỡng trôi dần về 30), nên không có tà giáo nào khi người chơi chỉ đứng nhìn. Tà giáo chỉ sinh ra khi Thiên Đạo nhiều lần làm ngơ hoặc giáng phạt một vùng.
- **Hiệu năng:**
  - Hệ Tín ngưỡng và Thiên mệnh tốn 0,3–0,6 ms/năm. Kiểm tra làng được chia làm ba, mỗi tháng chỉ xét một phần ba.
  - Tổng năm 1000 là 124 ms/năm (96 ms ở devlog 25), vì lần chạy này thế giới đông hơn: nhiều tu sĩ, yêu thú, thế lực hơn.
  - Hệ Di chuyển (50 ms) là điểm nóng lớn nhất, cần tối ưu tiếp: lái theo đường chỉ khi đổi ô, hoặc Burst.
