# 23 · Hung thú xuất thế, liên minh trảm yêu, ma thú đa dạng

**Ngày:** 2026-10-07

## Lý do
- Người chơi:
  - *"lâu lâu sẽ có ma thú mạnh xuất hiện, gọi là sự kiện thế giới, ma thú chia thành nhiều cấp bậc, đi tàn sát khắp nơi, các tu sĩ phải cùng nhau tiêu diệt"*.
  - *"ma thú đa dạng tí nhé bro"*.
- Phản hồi về art, qua ba vòng:
  - *"ma thú xấu quá bruh, vẽ lại đi"*, kèm ảnh tham khảo: tứ hung (Thao Thiết, Đào Ngột, Cùng Kỳ, Hỗn Độn), long có cánh, huyền quy.
  - *"vẽ pixel đơn giản thôi… dáng tương tự na ná là được… cho nó to xíu"*.
  - *"kích thước phải to hơn tu sĩ"* và *"làm nó dài dài trông hiền quá, làm nó to cao í"*.

## Đã làm

### Ma thú đa dạng (`BeastKind`, `BeastSystem.KindFor`)

| Loài | Nơi sinh | Ghi chú |
|---|---|---|
| Yêu lang | đồng cỏ, xa van, lãnh nguyên, Mộ Lan | |
| Yêu hổ (Cùng Kỳ) | rừng phương nam, rừng rậm | |
| Yêu hùng | rừng phương bắc, tuyết, lãnh nguyên | |
| Cự mãng | đầm lầy, rừng rậm | |
| Độc hạt | sa mạc, hoàng thổ | |
| Kim sí đại bằng | đồi núi | biết bay |
| Cửu vĩ hồ | đồng cỏ, rừng | |
| Ma ngưu (Thao Thiết) | Ma Đạo, đất tro | |
| Hung viên (Đào Ngột) | đồi núi | |
| Phi long | ven biển | biết bay |
| Hỏa kỳ lân | núi lửa | chỉ xuất hiện làm hung thú |
| Huyết bức | Ma Đạo | biết bay |
| Huyền quy | ven biển | |
| Hỗn Độn | — | chỉ là hung thú bát–cửu giai; biết bay |

- **Tên theo loài** (`beasts.json` → `kinds`), ví dụ Cùng Kỳ, Thao Thiết, Đào Ngột, Bá Hạ, Ứng Long. Hung thú mang thêm hung danh (`greatTitles`), ví dụ "Thái Cổ Lôi Văn Hổ", "Cửu U Ma Ngưu".
- **Loài biết bay** bay thẳng qua mọi địa hình, tốc độ 6 + cấp. Loài đi bộ đi theo `NavSystem`, cấp cao bước nhanh hơn.

### Art vẽ tay (`Render/BeastArt.cs`)
- Mỗi loài là một bản đồ ký tự, mỗi ký tự một màu, rộng khoảng 22–27 px; viền tự thêm.
- Dáng cao và dựng đứng:
  - Yêu lang ưỡn ngực ngẩng đầu, Cùng Kỳ chồm lên dang cánh, gấu đứng hai chân há miệng gầm, mãng xà ngóc như rắn hổ mang, bọ cạp dựng đuôi.
  - Thao Thiết nhìn thẳng, mõm há đầy nanh, hai sừng đỏ.
  - Đào Ngột là khối bờm dựng gai.
  - Phi long đứng dang cánh, kỳ lân chồm, huyền quy ngẩng cổ, Hỗn Độn là khối trắng không mặt có cánh.
- Loài bay có 2 khung vỗ cánh. Loài đi bộ nhún lên 1 px ở khung thứ hai.
- **Kích cỡ** (`SpriteLibrary.BeastScale`):
  - Ma thú luôn to hơn tu sĩ: bản gốc đã rộng gấp 2–3 lần một tu sĩ.
  - Từ lục giai trở lên và mọi hung thú vẽ ×2, cửu giai vẽ ×3. Phóng theo bội nguyên nên pixel vẫn vuông.
- Atlas đơn vị dùng ô 32 px. Hào quang đỏ máu cho hung thú, đặt theo cỡ con thú.
- Cảnh đánh nhau vẽ đúng loài (`BeastSystem.LookNear` tìm con thú tại chỗ, kể cả con vừa chết).
- Thẻ yêu thú hiện loài, có biết bay không, số nơi đã tàn sát, và liên minh đang truy sát nó.

### Hung thú xuất thế (`Sim/BeastHorde.cs`)
- **Sinh ra:**
  - Mỗi năm có xác suất 3% (nhân với luật Thiên tai), tối đa 3 hung thú cùng lúc.
  - Nguồn gốc theo thứ tự ưu tiên: từ núi lửa (Hỏa kỳ lân), từ chiến trường cổ có oán khí từ 15 người trở lên, hoặc từ thâm sơn.
  - Yêu thú tự tu lên thất giai cũng có thể "sát tính bộc phát" mà thành hung thú.
- **Cấp theo thời đại:**
  - Thiên hạ mạnh nhất mới đến Kết Đan thì hung thú lục–thất giai; có Nguyên Anh thì thất–cửu giai; có Hóa Thần thì cửu giai.
  - Như vậy hung thú luôn đáng sợ nhưng không phải bất khả chiến bại.
- **Tàn sát:**
  - Đi từ thành này sang thành khác, không quay lại hai nơi vừa tàn sát.
  - Đến nơi thì giết 8–20% dân (tường thành giảm 60%, miếu thổ địa giảm 10%), phá nhà, để lại đất bị giày xéo (kỳ lân để lại đất cháy). Sau đó nghỉ 20–50 ngày.
- **Liên minh trảm yêu:**
  - Sau mỗi lần tàn sát, các tông môn trong 320 ô (lần sau xa hơn) cử tới 3 cao thủ mỗi nơi. Cảnh giới tối thiểu theo cấp hung thú: lục giai cần Trúc Cơ, thất giai Kết Đan, cửu giai Nguyên Anh. Ma môn thường đứng ngoài.
  - Liên minh quá yếu thì các tông môn "đóng chặt sơn môn".
  - Người trong liên minh bay đuổi theo con thú thật trên map, rồi đánh nhiều hiệp: cả liên minh cùng đánh vào da thịt nó, mỗi hiệp nó xé một người. Chết quá nửa thì những người còn lại tan vỡ đội hình.
  - **Thắng:** người tung đòn kết liễu được yêu đan (thành pháp bảo), linh thạch, đan dược, tiến độ tu luyện và danh tiếng; cả liên minh cùng được thưởng. Các tông môn cùng chiến đấu thân nhau hơn (+12 quan hệ), nên dễ kết minh.
  - **Thua:** hung thú mạnh thêm. Liên minh sau được gọi từ xa hơn.
  - Chỗ đánh nhau thành chiến trường cổ (vết tích và oán khí).
- **Test mới `HungThuRavagesTownsAndTheSectsBandTogether`:** sau 6 năm, hung thú tàn sát nhiều nơi khác nhau và liên minh đã hình thành.

### Sửa lỗi
- **Hung thú chết vì "mất hang ổ"** khi chỗ đứng thành nước, trong khi nó vốn không có hang. Hung thú và loài biết bay giờ không bị kiểm tra hang ổ.
- **Hung thú tàn sát mãi một chỗ:** nó xóa mục tiêu trước khi chọn mục tiêu mới. Giờ nó nhớ hai nơi vừa tàn sát.

### Cân bằng (đo 1000 năm, seed ThienDao)
- **Bản đầu quá chết chóc:**
  - Tới 3 hung thú cùng lúc, mỗi tháng tàn sát 8–20% dân một thành, không bao giờ dừng.
  - Liên minh lao vào đánh dù quá yếu.
  - Kết quả: phàm nhân tụt từ khoảng 34 nghìn xuống 15,7 nghìn, tu sĩ còn sống từ 879 xuống 467, yêu thú tăng lên khoảng 50 vì thiếu người săn.
- **Đã chỉnh:**
  - Tối đa 2 hung thú, xuất thế 2%/năm.
  - Mỗi lần tàn sát giết 6–15% dân, nghỉ 60–150 ngày.
  - Liên minh chỉ xuất quân khi đủ 80% sức hung thú; chết 1/3 là tan vỡ đội hình.
  - **Ngủ say:** sau 8 lần tàn sát hoặc 15 năm, hung thú về núi ngủ 50 năm. Lúc ngủ nó không lảng vảng, không tập kích, không gây sự, không lập yêu tộc.
- **Sau khi chỉnh:** phàm nhân 30–31 nghìn và vẫn tăng; tu sĩ còn sống 770–830; yêu thú khoảng 39, tính cả hung thú đang ngủ.

## Kết quả
- **Test EditMode:** 53/53 (mới: `HungThuRavagesTownsAndTheSectsBandTogether`).
- **Hiệu năng:** năm 1000 tốn 117 ms/năm (Editor, chế độ Debug), với thế giới đông tu sĩ hơn.
  - Hệ Yêu thú tăng dần lên 11 ms/năm dù chỉ có khoảng 40 con. Cần đo chi tiết ở bước sau.

## Tiếp theo
- Burst cho các vòng lặp nóng (A*, di chuyển, linh khí, vẽ chunk).
- Mùa và ngày/đêm có tác động lên mô phỏng.
