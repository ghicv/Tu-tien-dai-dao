# 22 · Bay và đi bộ: tìm đường né địa hình

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"các tu sĩ có thể bay thì di chuyển nhanh hơn và vượt địa hình, các tu sĩ chưa đủ tu vi phải đi bộ thì đi chậm hơn, phải tự tìm đường né địa hình chứ k được xuyên qua nhé"*.
- Cách cũ:
  - Người đi bộ cứ đi thẳng tới đích.
  - Vướng nước hoặc núi thì bỏ đích. Tu sĩ đi bộ về nhà còn dịch chuyển tức thời qua chỗ bị chặn.
  - Dân di cư và thương đội hễ đứng yên một ngày là bị coi như kẹt.

## Đã làm

### Bay (`CreatureSystem.Fly`)
- Từ Trúc Cơ, tu sĩ ngự kiếm phi hành, bay thẳng qua biển, đỉnh núi và dung nham.
- Tốc độ theo cảnh giới (`FlightByRealm`): Trúc Cơ 10 ô/ngày, Kết Đan 14, Nguyên Anh 20, Hóa Thần 30.

### Đi bộ (`CreatureSystem.Walk`, `Sim/NavSystem.cs`)
- **Ai đi bộ:** Luyện Khí, dân di cư, thương đội, yêu thú.
- **Tốc độ theo mặt đất** (`TerrainInfo.WalkSpeed`):

  | Mặt đất | Tốc độ (cỏ = 1) |
  |---|---|
  | Cỏ, xa van, ruộng | 1 |
  | Bãi cát | 0,9 |
  | Đất tro | 0,85 |
  | Sa mạc, lãnh nguyên | 0,75 |
  | Rừng | 0,65 |
  | Đồi, hoàng thổ | 0,6 |
  | Tuyết | 0,55 |
  | Rừng rậm | 0,5 |
  | Đầm lầy | 0,4 |
  | Núi, sông (lội qua) | 0,35 |

  - Đường buôn (thương lộ) nhân thêm ×1,6.
  - Biển, đỉnh núi và dung nham không đi qua được.
- **Lội sông:** sông giờ đi qua được nhưng rất chậm, nên đất liền không còn bị sông chia cắt.
- **Tìm đường:**
  - Lưới thô 4×4 ô (256×256 nút). Mỗi nút lưu thời gian đi qua (đường nhanh, đầm lầy và núi chậm). Nút phần lớn là nước hay đá thì bị chặn.
  - Tìm bằng A* tám hướng, không cắt góc giữa hai nút bị chặn, phá hòa theo chỉ số nút nên luôn ra cùng một đường.
  - Làm mượt đường (string pulling): bỏ những mốc có thể đi thẳng tới, nên người đi băng thẳng qua đồng trống và chỉ vòng khi cần.
  - Chuyến ngắn (dưới 40 ô) và nhìn thẳng thấy đích thì đi thẳng, không cần tìm đường.
- **Đi theo đường:**
  - Bước tới mà vướng (đất vừa đổi) thì thử đi chệch sang bên (±30°, ±60°, ±90°). Không được thì lần sau tìm đường lại.
  - Đứng trên nước (lũ vừa tràn tới) thì lội về ô khô gần nhất, không đi thẳng qua biển.
- **Không có đường** (bị kẹt trên đảo, thung lũng bị bít): bỏ đích.
  - Dân di cư dừng lại lập làng tại chỗ.
  - Thương đội mất hàng.
  - Riêng tu sĩ đi bộ không về được nhà thì được đặt về nhà. Đây là ngoại lệ duy nhất còn dịch chuyển tức thời.
- **Hiệu năng:**
  - Tối đa 32 lần tìm đường mỗi ngày, mỗi lần mở tối đa 6.000 nút. Ai chưa tới lượt thì chờ đến hôm sau.
  - Lưới thô chỉ tính lại ở vùng địa hình vừa đổi.
  - Bộ nhớ tạm của A* không ghi vào file lưu.

## Kết quả
- **Test mới `WalkersGoRoundWaterAndFlyersOverIt`** (3 năm, seed ThienDao): 0 lần người đi bộ bước từ đất xuống nước, lên đỉnh núi hay vào dung nham.
- **Test EditMode:** 52/52.
- **Hiệu năng (1000 năm, Editor ở chế độ Debug).** Cần tới 5 vòng đo mới tìm ra đúng chỗ tốn:
  - Bản đầu: hệ Di chuyển 69 ms/năm (trước khi có tìm đường là 23 ms). Cả mô phỏng năm 1000 tốn 145 ms.
  - **Vùng liên thông** (`NumberZones`): đích không cùng vùng thì báo không có đường ngay. Đích dời ít (dưới 8 ô) thì giữ đường cũ. A* có trọng số.
  - **Lần thử làm mượt đường trên lưới thô bị bỏ:** đường đi xuyên ô nước bên trong một nút "mở", người đi vướng rồi tìm lại mãi, lên khoảng 2.000 lần mỗi năm.
  - **Sửa tận gốc:**
    - Mốc đường là ô khô gần tâm nút nhất (`_door`), không phải tâm nút.
    - Một chuyến bị vướng quá 3 lần thì coi như không có đường.
    - Nút toàn ô khô (`_full`) thì không cần kiểm tra từng ô.
  - **Kết quả:**
    - Khoảng 50 lần tìm đường mỗi năm, khoảng 22 nghìn nút mở. Không phải chờ lượt.
    - Hệ Di chuyển khoảng 41 ms/năm, trong đó A* khoảng 20 ms. Đánh số vùng dưới 1 ms.
    - Năm 1000 tốn khoảng 111 ms/năm (86,7 ở devlog 21), với 34 nghìn dân và 880 tu sĩ.
  - **Phần còn lại là chi phí thật của vòng lặp:** khoảng 0,9 µs mỗi nút A* trong Mono chế độ Debug. Đây là việc hợp với Burst (xem đề xuất trong phần Tiếp theo).
  - `PerfProbe` có thêm bảng "Trong hệ Di chuyển": thời gian A*, kiểm tra đi thẳng, đánh số vùng, số lần tìm đường, không có đường, số nút mở, chờ lượt.

## Tiếp theo
- Sự kiện thế giới: hung thú nhiều loài, nhiều cấp xuất thế, đi tàn sát khắp nơi; các tông môn liên minh trảm yêu (người chơi yêu cầu).
