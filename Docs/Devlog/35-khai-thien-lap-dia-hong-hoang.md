# 35 · Khai thiên lập địa: thế giới mở màn ở thời hồng hoang

**Ngày:** 2026-10-09

## Lý do
- Người chơi:
  - *"game nên bắt đầu từ thửa hồng hoang khai thiên lập địa, theo timeline tu tiên"*
  - *"chưa có con người và tông môn nào, toàn là ma thú, thần thú, hung thú"*
- Đã chốt trong [`Docs/Vision.md`](../Vision.md):
  - Nhân tộc xuất hiện theo **cả hai cách**: tự tới khi đến thời kỳ, và Thiên Đạo có thể tạo ra.
  - Tên các thời kỳ theo kiểu Hồng Hoang; tên sinh vật và địa danh theo PNTT.

## Thế giới mới
- **Ván mới nào cũng mở màn ở khai thiên lập địa** (`MapGenerator.Generate(seed, primordial: true)`):
  - Có đủ địa hình, linh khí, linh mạch.
  - Không có làng, tông môn, tu sĩ, tên nước, thượng cổ di tích.
- **Tùy chọn cũ vẫn còn:** khởi đầu "đã có người" chỉ còn dùng cho test và công cụ editor.
- **File lưu** ghi lại thế giới được mở theo kiểu nào, để lúc tải dựng lại map đúng như lần đầu. Phiên bản file lưu tăng lên 2.

### Thú tộc làm chủ
- **150 yêu thú** rải theo linh khí: linh khí càng dày, giai càng cao. Lúc đầu tối đa lục giai.
- **5 chân linh cửu giai**, mỗi loài đúng một con, mỗi con đứng đầu một yêu tộc:

| Chân linh | Yêu tộc | Lãnh địa |
|---|---|---|
| Thái Cổ Chân Long | Long tộc | linh mạch gần biển |
| Cửu Thiên Thiên Phượng | Phượng tộc | linh mạch trên cao |
| Hỗn Nguyên Kỳ Lân | Kỳ Lân tộc | linh mạch trong núi rừng |
| Bắc Minh Côn Bằng | Côn Bằng tộc | linh mạch gần biển |
| Huyền Vũ Thánh Quy | Huyền Vũ tộc | linh mạch gần biển |

  - Mỗi con chiếm một linh mạch giàu nhất, cách nhau ít nhất 180 ô.
  - Yêu thú sinh ra trong vòng 200 ô quanh lãnh địa thì quy phục; hằng năm có thêm con mới quy phục.
  - Chân linh sống 30.000 năm và không bao giờ hóa hung thú.
- **Hai loài mới vẽ bằng pixel art:**
  - Thiên Phượng: cánh đỏ thẫm, đầu và mào vàng, đuôi lửa dài, phun lửa khi đánh.
  - Côn Bằng: cá biển bắc hóa chim, xanh thẫm, đuôi cá và cánh lớn, đánh bằng gió.
  - Cả hai biết bay, vỗ cánh có 2 khung hình.
- **Chân linh chết thì để lại bí cảnh thượng cổ** 3 tầng, giấu kín cho tới khi có người tìm ra: Long Huyệt, Phượng Sào, Kỳ Lân Động, Côn Bằng Mộ, Huyền Vũ Quy Khư. Bên trong có "huyết mạch" của chân linh, 3.000 linh thạch và đan dược. Đó là truyền thừa từ hồng hoang cho nhân tộc đời sau.
- **Chưa có làng thì không có hung thú nào nổi lên**, vì hung thú là thứ đi từ thành này sang thành khác tàn sát.
- **Bảo vật thiên địa** vẫn xuất thế; nhưng khi chưa có tu sĩ thì chỉ thành sự kiện nhỏ, không phải "cả thiên hạ chấn động".

### Nhân tộc xuất hiện
- **Tự nhiên:**
  - Sau 50–100 năm, một bộ lạc 24–40 người xuất hiện trên đồng cỏ màu mỡ gần nước, cách lãnh địa chân linh ít nhất 120 ô và cách làng khác ít nhất 60 ô.
  - Sau đó cứ 20–60 năm lại thêm một bộ lạc, tổng cộng 6 bộ lạc; từ đó người tự sinh sôi, di cư, lập làng như cũ.
  - Nếu bị giết sạch, 50–100 năm sau sẽ có người khác tới.
- **Thiên Đạo tạo:** công cụ "Lập làng" đổi thành **"Tạo hóa nhân tộc"**. Bấm vào đâu thì 24 phàm nhân xuất hiện ở đó, kèm hiệu ứng ban phúc, kể cả ngay năm đầu.
- Bộ lạc đầu tiên, tự đến hay do Thiên Đạo tạo, là sự kiện lớn: **"Nhân tộc xuất hiện!"**.

### Thời kỳ (`EraSystem.EpochName`)
| Thời kỳ | Khi nào |
|---|---|
| Hồng hoang | chưa có người |
| Nhân tộc mông muội | có người, chưa ai tu luyện |
| Thủy tổ tu tiên | có người đầu tiên tu luyện: sự kiện lớn, người đó "tự ngộ ra Dẫn Khí Quyết, bộ công pháp đầu tiên" |
| Thượng cổ | có tông môn đầu tiên: sự kiện lớn "tông môn đầu tiên của nhân tộc, Thượng cổ tu tiên bắt đầu" |
| Trung cổ, Cận cổ, Kim cổ | lần lượt sau mỗi đại kiếp; đại kiếp thời chỉ có thú thì không tính |

- Trước khi có tông môn, tên thời đại đi theo thời kỳ: "Hồng Hoang", "Nhân Tộc Mông Muội", "Thủy Tổ Tu Tiên". Sau đó các thời đại cũ (Thái Bình, Đại Chiến Loạn Thế, Mạt Pháp…) chạy như trước.
- Đồng hồ góc trái ghi thời kỳ: *"Hồng hoang · năm 1 · tháng 2"*.
- Camera mở ngay lãnh địa của một chân linh.

## Chạy thử (seed ThienDao, 500 năm, trước khi rút ngắn thời gian chờ nhân tộc)
- Năm 0: 155 yêu thú; Long tộc 40 con, Phượng tộc 41, Kỳ Lân tộc 23, Côn Bằng tộc 20, Huyền Vũ tộc 18.
- Năm 214: nhân tộc xuất hiện ở Cửu Khúc Thôn.
- Năm 296: thủy tổ tu tiên Âu Dương Kinh Hồng.
- Năm 445: Thiên Sơn Phái, tông môn đầu tiên, mở ra thượng cổ.
- Năm 500: 20 làng, 4.372 người, 62 tu sĩ.
- Hung thú tàn sát các làng đầu tiên nhiều lần.
- Sau lần chạy này, thời gian chờ bộ lạc đầu tiên được rút từ 150–300 xuống còn 50–100 năm, và giai khởi đầu của yêu thú thường hạ từ thất giai xuống lục giai.

## Kết quả
- **Test EditMode:** 61/61. Ba test mới:
  - `PrimordialWorldOpensWithBeastsAndTheFiveChanLinh`
  - `MenComeOfThemselvesOrAtHeavensHand`
  - `AFallenChanLinhLeavesItsLairToTheAgesAfter`
- **Chưa làm:**
  - các yêu tộc tranh lãnh địa, đánh nhau trên map;
  - biome tu tiên giới;
  - sơn môn sống;
  - thiên kiêu bảng;
  - sợi nhân quả.
