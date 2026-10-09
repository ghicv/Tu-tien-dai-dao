# 32 · Thế giới hiện hình: đuổi bắt, cơ duyên trên đầu, chiêu thức theo công pháp, động phủ

**Ngày:** 2026-10-09

## Lý do
- Người chơi:
  - *"các simulation của bro toàn kể chuyện, chả thấy thể hiện ra cho người chơi thấy gì cả, rất nhảm"*
  - *"tu sĩ A phát hiện bí bảo, tìm được cơ duyên thì phải thể hiện trên thế giới là nó đang ở chỗ bí bảo, và cầm được cơ duyên trên đầu với hiệu ứng phát sáng"*
  - *"ma thú phải tấn công các động vật, tu sĩ ở gần nó, đi qua nó. nếu là động vật thì chạy, tu sĩ thì nếu mạnh hoặc ngang thì ở lại đánh nhau, yếu hơn thì chạy, ma thú đuổi theo"*
  - *"click vào bí cảnh hoặc đồ gì xịn, có thể thêm chức năng thu hút toàn bộ tu sĩ đến tranh giành"*
  - *"tu sĩ bế quan, 1 là kiếm hang động hoặc động phủ nào đó gần, hoặc thành trấn… chứ k đứng yên tại chỗ bế quan được"*
  - *"các trận combat đấu pháp phải có tí công pháp, hiệu ứng bắn chưởng tung tóe đặc thù của từng tông môn, từng công pháp… combat như thật luôn"*
- Hướng mới (đã lưu vào bộ nhớ): **tạm dừng tính năng mới**. Mọi việc dồn vào làm cho thế giới **thay đổi thấy được trên map**, và hành động của người chơi kéo theo một chuỗi thay đổi nhìn thấy được.

## Đuổi bắt (`Sim/BeastChase.cs`, mới)
Trước đây yêu thú gặp người qua đường được giải quyết bằng một lần tung xúc xắc trong tháng, rồi hiện một dòng chữ. Nay nó diễn ra từng ngày, ngay trên map:
- **Yêu thú nhìn quanh** 3 ngày một lần, tầm nhìn 5 + giai ô. Ai đang đi đường trong tầm có 35% bị để ý: tu sĩ (dưới Hóa Thần, không đang đánh trận hay truy sát, 3 tháng gần đây chưa đánh nhau), đoàn di dân, thương đội (hai loại sau chỉ bị yêu thú từ nhị giai trở lên để ý).
- **So sức:**
  - tu sĩ mạnh gấp 1,5 lần trở lên: tu sĩ **săn yêu đan**, yêu thú bỏ chạy;
  - ngang sức (0,7–1,5): hai bên **lao vào nhau**;
  - yếu hơn: tu sĩ **bỏ chạy** ra xa 30 ô, yêu thú đuổi theo. Người ngự kiếm thường thoát; người đi bộ hiếm khi thoát.
- **Bắt kịp** (cách 1,6 ô): đánh nhau thật (`Fight`), cảnh đấu diễn ngay tại chỗ. Thương đội bị bắt thì mất hàng; đoàn di dân mất 20–50% người.
- **Bỏ cuộc:** đuổi quá 15 ngày, hoặc con mồi ra khỏi 3 lần tầm nhìn.
- **Rình mồi:** ngày không nhìn quanh, yêu thú thỉnh thoảng (1/12 mỗi ngày) vồ về một chỗ cách 3–6 ô nếu vùng có thú hoang. Con mồi do renderer diễn (xem dưới).
- Bỏ hẳn kiểu gặp gỡ "tung xúc xắc theo tháng" cũ.

## Cơ duyên
- **Thám hiểm là một chuyến đi thật** (`Trip.Explore`): tu sĩ bay tới bí cảnh, vào trong. Ra thì đứng lại ở cửa 20–45 ngày rồi mới về.
- **Giơ cơ duyên trên đầu** (`Cultivator.Loot`, `LootUntil`): ai vừa được gì thì vật đó lơ lửng trên đầu 45–60 ngày, nhấp nhô một pixel, có quầng sáng đập theo nhịp và hạt sáng bay lên. Lúc vừa có được thì một cột sáng dựng lên.
  - Có 6 kiểu, đều là pixel art vẽ tay: pháp bảo (kiếm), ngọc giản công pháp, hồ lô đan dược, linh thạch, thiên địa linh vật (ngọc), yêu đan (đỏ).
  - Nguồn: thám hiểm bí cảnh, thắng tranh đoạt, Thiên Đạo ban pháp bảo hay công pháp, giết người đoạt công pháp, chém yêu thú lấy yêu đan.
  - Người đang ở nhà mà được cơ duyên thì bước ra đứng giơ vật lên cho người chơi thấy.
- **Tranh đoạt:** người thắng vào bí cảnh rồi đứng giơ cơ duyên 30 ngày. Trong lúc tranh, người của các phe quanh bí cảnh (cách nhau trong 12 ô) bắn chiêu vào nhau liên tục.

## Thiên cơ hiển lộ (quyền năng mới)
- Bấm vào một bí cảnh hay thiên tài địa bảo: một cột sáng dựng lên, tin đồn bị thổi phồng lan ra 2.000 ô.
- Mọi tông môn đã nghe tin trong vòng 5.000 ô kéo người tới, mỗi tông tối đa 5 người, tối đa 12 phe, tranh nhau 90 ngày. Đang có tranh đoạt thì kéo dài thêm và gọi thêm phe.
- Có trong thanh công cụ, cạnh "Mở bí cảnh".

## Đấu pháp như thật
- **Chiêu thức theo công pháp:** công pháp hệ gì thì đánh ra chiêu hệ đó. Đệ tử một tông dùng chung trấn phái công pháp, nên mỗi tông có lối đánh riêng.

| Hệ | Bay đi | Trúng đích |
|---|---|---|
| Kim | phi kiếm ánh vàng, vệt lấp lánh | tia lửa, chớp sáng |
| Mộc | lốc lá xoay | lá văng tung |
| Thủy | cầu nước nhỏ giọt | nước bắn tóe |
| Hỏa | cầu lửa 3 khung, tàn lửa, khói | nổ lửa, ngọn lửa bùng |
| Thổ | tảng đá ném vòng cung | đá vụn, bụi đất |
| Lôi | tia sét gấp khúc nhấp nháy mỗi khung | chớp trắng, tia lửa tung khắp |
| Phong | trăng lưỡi liềm gió, bóng mờ đuổi theo | vòng xung kích, gió cắt ngang |
| Băng | băng thương rụng sương | vỡ thành mảnh băng |
| Ma công | cầu huyết khí, khói đen | nổ tối, khói đen |
| Vạn năng | kiếm khí màu theo cảnh giới | tia lửa |

- **Yêu thú tam giai trở lên có chiêu riêng:** rồng, kỳ lân, hồ ly phun lửa (một dòng cầu lửa); rắn, bọ cạp phun độc; huyền quy phun nước; ưng tạo gió; dơi phát sóng âm; hỗn độn dùng ma khí. Thú nhỏ hơn thì cào cắn: lao hẳn vào đối thủ, vệt cào đỏ, máu văng.
- **Chuyển động:**
  - Trước mỗi chiêu là **tụ khí**: hạt sáng hút vào tay, rồi lóe lên.
  - Hai bên lượn lên xuống trong lúc đánh.
  - Một phần tư số đòn đầu bị **né**: người bị nhắm bước tránh, chiêu bay qua cắm xuống đất tung bụi.
  - Trúng đòn thì bị **đánh bật lùi** mạnh hơn.
  - Số chiêu mỗi trận tăng lên: tu sĩ đấu nhau 9 chiêu, đấu yêu thú 7.
- **Đuổi bắt trên map:**
  - Yêu thú đang săn có dấu **"!" đỏ** nhấp nháy trên đầu; ai đang chạy thì tung bụi.
  - Khi tu sĩ săn yêu thú, hoặc hai bên ngang sức, chạm nhau trong 7 ô thì vừa chạy vừa bắn chiêu vào nhau.

## Thú hoang biết sợ, sói biết săn
- **Chạy trốn:** hươu, thỏ, sói thấy yêu thú trong 6 ô thì chạy thẳng ra xa, nhanh gấp 3, chân chạy nhanh, tung bụi. Hươu và thỏ còn chạy trốn sói.
- **Sói săn:** sói đi săn theo đợt, khoảng 1/3 thời gian, đuổi con hươu hay con thỏ gần nhất trong vùng. Bắt được thì có vệt cào và máu văng.
- **Yêu thú rình mồi:** đúng chỗ yêu thú đang vồ tới hiện một con hươu (hoặc thỏ). Con này chạy, nhưng chậm hơn, nên bị bắt.
- Số thú tượng trưng tối đa mỗi vùng tăng từ 3/3/2 lên 5/5/3.

## Bế quan trong động phủ
- **Không ai đứng giữa đồng bế quan nữa:** tu sĩ ở nhà thì ở trong nhà, kể cả nhân vật chính.
  - Tán tu sống ngoài hoang có một **động phủ** trên map: gò đá xám, rêu trên đỉnh, cửa hang hình vòm.
    - Chủ đi vắng thì cửa hang tối.
    - Chủ đang bế quan thì cửa hang sáng đèn. Từ Kết Đan trở lên có thêm quầng sáng màu cảnh giới phía trên.
    - Bấm vào động phủ là chọn người bên trong.
  - Đệ tử tông môn ở trong tông môn, người sống trong làng trấn thì ở trong làng.
- Nhân vật chính ở trong tông môn hay làng trấn thì có vòng vàng ngay trên cửa, để người chơi tìm và bấm được.

## Kết quả
- **Test EditMode:** 63/63. Test mới `BeastsChaseThoseWhoPassAndTheWeakRun`:
  - Yêu thú lục giai thấy tu sĩ Luyện Khí đi ngang thì đuổi, tu sĩ bỏ chạy.
  - Yêu thú nhất giai thấy tu sĩ Kết Đan thì tu sĩ quay sang săn nó.
- **Cân bằng, đo 1000 năm (seed ThienDao):**
  - Bản đầu: yêu thú để ý người qua đường 35% mỗi lần nhìn (10 lần một tháng), nên gần như ai đi ngang cũng dính. Tu sĩ săn sạch yêu thú: năm 1000 chỉ còn 11 con, trước là 45. Hệ Yêu thú tốn 30 ms/năm.
  - Đã sửa:
    - Hạ xuống 4% mỗi lần nhìn, tức khoảng 2/5 mỗi tháng, gấp chừng 2,5 lần lối gặp gỡ theo tháng cũ, nên đuổi bắt hay xảy ra mà không diệt chủng.
    - Mỗi tu sĩ 6 tháng mới dính lại một lần.
    - Yêu thú bị săn chạy được 8 ngày là thoát.
    - Lăn xúc xắc trước rồi mới tìm con mồi.
    - Danh sách "đang bị đuổi" dựng một lần mỗi ngày, không quét lại cả bầy.
    - Vòng hằng ngày chỉ duyệt yêu thú còn sống: danh sách `All` giữ cả con đã chết để làm sử sách.

| Năm 1000 | Trước (devlog 31) | Bản đầu | Bản cuối |
|---|---|---|---|
| Yêu thú còn sống | 45 | 11 | **43** |
| Tu sĩ còn sống | 816 | 1.064 | **755** |
| Phàm nhân | 31,1 nghìn | 38,7 nghìn | **30,2 nghìn** |
| Hệ Yêu thú (ms/năm) | 14,0 | 30,1 | **16,9** |
| Tổng mô phỏng (ms/năm) | 112 | 141 | **103,9** |

- **Hình ảnh chỉ chạy ở renderer, theo thời gian thật, và chỉ trong khung nhìn:**
  - Cơ duyên, đuổi bắt, chiêu tranh đoạt và động phủ chỉ đọc trạng thái mô phỏng, không ghi gì vào.
  - Thú hoang nhìn quanh mỗi 8 khung hình, lệch nhịp giữa các con. Lúc đang chạy thì nhìn mỗi khung.
  - Tối đa 96 yêu thú được xét làm kẻ săn, 48 chiêu bay cùng lúc.
- **Số lượng mô phỏng có thể lớn, trên map chỉ vẽ tượng trưng.**
- Mục tiêu: **một thế giới sống động nhất có thể.**
- **Toàn bộ hình ảnh là pixel art**, kể cả mọi hiệu ứng.
- **Hiệu năng đặt lên đầu** cho mọi tính năng, mà vẫn giữ chất sandbox.
- Tu tiên **cực khó**: Kết Đan là trưởng lão, Nguyên Anh là bá chủ một phương, Hóa Thần là truyền thuyết.
