# 15 · M7: Chiều sâu thế giới, hiệu ứng đánh nhau pixel

**Ngày:** 2026-10-07

## Lý do
M7 trong GDD: kinh tế/chợ/thương lộ, yêu thú tiến hóa & yêu tộc, bí cảnh từ lịch sử, thời đại & mạt pháp, với kiểm chứng "hai seed khác nhau → lịch sử khác hẳn".

Trong lúc làm, người chơi yêu cầu thêm:
- *"tu sĩ đánh nhau thì phải có hiệu ứng tấn công… toàn thân tu sĩ nháy trắng flash để biết là đang dính damage"*
- *"dùng cho toàn bộ sinh vật… khi chết thì xuất hiện hiệu ứng khói trắng xong biến mất"*
- *"Hiệu ứng k matching với art style của game, cho pixel thôi"*

## Đã làm

### Yêu thú & yêu tộc (`Sim/BeastSystem.cs`)
- **Khai linh trí:** mỗi năm, ở vùng hoang (cách làng trên 40 ô) có linh khí từ 2.500 trở lên, thú hoang có thể khai linh trí thành yêu thú nhất giai. Tên ghép từ `Lore`, ví dụ "Xích Mục Yêu Lang", "Bạch Mao Linh Lộc". Thế giới khởi đầu với khoảng 14 con.
- **Lên giai:** yêu thú hút linh khí nơi hang ổ để lên giai, từ nhất giai tới cửu giai. Hai giai tương đương một đại cảnh giới của tu sĩ. Lên thất giai thì hóa hình. Thọ nguyên từ 80 tới 3.000 năm.
- **Hằng tháng:**
  - Săn hươu, thỏ trong vùng.
  - Đi lại quanh lãnh địa.
  - Từ nhị giai trở lên thì tập kích làng gần nhất. Nếu tông môn che chở làng đó mạnh hơn nhiều thì thường bỏ qua; nếu có người ra tay thì hai bên giao chiến.
- **Gặp tu sĩ ngoài đường:** đánh nhau. Tu sĩ thắng được yêu đan: linh thạch, thêm đan nếu yêu thú từ tam giai trở lên, cùng một ít tu vi. Thua thì thường chết.
- **Yêu Vương:** yêu thú từ ngũ giai trở lên, có ít nhất 2 yêu thú yếu hơn quanh nó, sẽ xưng Yêu Vương và lập yêu tộc có tên riêng (Bách Thú Lĩnh, Huyết Nguyệt Sơn…). Yêu tộc kéo thú triều xuống làng; tông môn gần đó cử người mạnh nhất đi chinh phạt. Yêu Vương chết thì yêu tộc tan rã.
- **Thả yêu thú:** có nút trong tab Sinh linh (`SpawnBeastCommand`).

### Bí cảnh từ lịch sử (`Sim/RelicSystem.cs`)
- **Nguồn gốc:**
  - Tu sĩ từ Kết Đan trở lên khi chết có thể để lại "Động phủ của X": 35% với Kết Đan, 70% với Nguyên Anh, chắc chắn với Hóa Thần. Bên trong là pháp bảo và linh thạch mà kẻ giết họ chưa lấy.
  - Tông môn bị diệt để lại "Di tích X".
  - Núi lửa và lôi địa có thể ẩn thiên địa linh vật.
- **Phát hiện:** bí cảnh ẩn cho tới khi có tu sĩ đi ngang qua trong vòng 25 ô; cơ hội tùy khí vận và ngộ tính. Người phát hiện vào ngay nếu dám, không thì tin đồn lan ra và người mạnh ở gần sẽ tìm tới. Nhân vật chính cũng chủ động đi thám hiểm.
- **Thám hiểm:** có thể vẫn lạc trong cấm chế. Nếu sống sót thì lấy được pháp bảo (đúng tên của người xưa), linh thạch, đan, truyền thừa (tu vi, ngộ tính), hoặc luyện hóa linh vật để tăng thọ.
- **Truyện mới:**
  - "Kế thừa y bát": đệ tử đời sau về động phủ của sư tổ.
  - "Di bảo truyền kỳ": vào động phủ của một danh nhân.
  - "Tìm về cố địa": phân tông tìm lại di tích của tông môn gốc.
  - "Bí cảnh ngàn năm": bí cảnh ngủ yên từ 300 năm trở lên mới được khai mở.

### Kinh tế & thương lộ (`Sim/TradeSystem.cs`)
- **Chợ ở mỗi làng:**
  - Lương thực dư trên 6 tháng thì đem bán.
  - Làng ở nơi linh khí cao hái được linh thảo.
  - Tông môn luyện linh thảo thành đan; khi đệ tử đột phá Trúc Cơ, tông dùng đan trong kho trước rồi mới bỏ linh thạch ra mua.
- **Giá cả:** giá biến động theo căn bậc hai của tỉ lệ cầu/cung, giới hạn từ ×0,25 tới ×4. Nhân vật chính mua đan theo giá của chợ đó.
- **Thương đội:** chở hàng tới nơi bán được giá hơn trong vòng 200 ô (trừ đi phí đường xa), nhờ đó làng thừa lương nuôi được làng đói.
- **Thương lộ:** ô nào có thương đội đi qua đủ 10 lần thì mòn thành đường đất (`ZoneFlags.Road`) trên bản đồ. Tuyến nào chở đủ 5 chuyến thì có tin "Thương lộ A – B hình thành".

### Thời đại & mạt pháp (`Sim/EraSystem.cs`)
- **Chu kỳ linh khí:** linh khí cả thế giới lên xuống ±25% theo chu kỳ 1.500–3.500 năm, mỗi seed một chu kỳ và một pha riêng; không thế giới nào mở đầu trong mạt pháp. Hệ số này nhân vào `Simulation.QiScale`.
- **Nhận diện thời đại:** mỗi 50 năm, và ngay khi đại kiếp bắt đầu hoặc kết thúc, thế giới được xếp vào một thời đại: Đại Kiếp, Tân Thời Đại, Mạt Pháp, Đại Chiến Loạn Thế, Hóa Thần Hiện Thế, Quần Hùng Tranh Bá, Linh Khí Phục Tô, "X Xưng Bá", Suy Tàn, Tiên Đạo Điêu Linh, Bách Tông Tề Phóng, hoặc Thái Bình.
- **Hiển thị:** tên thời đại nằm cạnh ngày tháng trên HUD và trong danh sách "Các thời đại" của Biên niên sử. Linh khí chu kỳ dưới 85% hoặc trên 115% thì hiện chip cảnh báo.

### Hiệu ứng đánh nhau & sát thương (`Render/FightScenes.cs`)
- **Cảnh đánh:** mô phỏng vẫn phân thắng bại trong một tick, nhưng mỗi trận đấu pháp hay đánh yêu thú được dựng thành cảnh khoảng 2,5 giây:
  - Hai bên đứng đối mặt và lao lên khi ra đòn.
  - Tu sĩ bắn kiếm khí theo màu cảnh giới (ma tu màu đỏ); yêu thú vồ, để lại vết cào đỏ.
  - Người trúng đòn bị đẩy lùi và **toàn thân nháy trắng** (vẽ thêm bóng trắng của chính sprite đè lên).
  - Đòn cuối chớp sáng, kẻ thua **bốc khói trắng rồi biến mất**, hoặc bỏ chạy.
- **Mọi sinh vật:** thiên tai, tập kích hay sét đánh tạo ra một vùng sát thương. Mọi sinh vật vẽ trên map trong vùng đó (tu sĩ, yêu thú, dân, thú hoang, di dân, thương đội) nhấp nháy trắng; một phần dân và thú tượng trưng gục xuống thành khói trắng. Mọi cái chết khác có vị trí trên map cũng bốc khói trắng.
- **Pixel art:**
  - Toàn bộ sprite hiệu ứng vẽ lại thành pixel viền cứng, 2–3 sắc phẳng: sét, cột sáng, chớp, vòng chấn động, mây, khói, lửa, và tia kiếm khí mới.
  - Sprite chỉ phóng theo bội số nguyên và khớp lưới 8 px/ô; độ mờ giảm theo từng nấc.
  - Khói là cụm đặc màu, nở theo nấc rồi biến mất.
  - Nhân vật trong cảnh đánh cũng khớp lưới và mờ theo nấc.
- **Sprite mới:** yêu thú, xe thương đội, và bản bóng trắng của mọi unit (dùng cho hiệu ứng nháy).

### Giao diện
- **Thẻ:**
  - Yêu thú: giai, tuổi, số người đã giết, linh khí hang ổ, thuộc hạ nếu là Yêu Vương.
  - Thương đội: đi từ đâu tới đâu, chở gì.
  - Làng: thêm giá và kho lương thực, linh thảo, đan, cùng số thương đội gửi đi và nhận về.
- **Thống kê:** thêm yêu thú, Yêu Vương, bí cảnh, thương đội, thương lộ, linh khí chu kỳ.
- **Bản đồ:** nhãn yêu tộc và yêu thú từ ngũ giai, nhãn bí cảnh đã lộ, đường đất màu nâu.

## Kiểm chứng
- **Test mới** (48/48 pass):
  - `BeastsAwakenAndGrow`
  - `CultivatorSlaysBeastForItsCore`
  - `YeuVuongGathersAClan`
  - `DeadExpertsLeaveCavesThatCanBeExplored`: động phủ giữ đúng pháp bảo của người chết.
  - `CaravansCarryGoodsAndWearRoads`
  - `EachWorldHasItsOwnAges`: hai seed có chu kỳ linh khí khác nhau; hệ số luôn trong khoảng 0,74–1,26; năm đầu không rơi vào mạt pháp.
  - `WorldInvariants` kiểm thêm liên kết thực thể của yêu thú, số đếm theo giai, và thuộc hạ chỉ theo Yêu Vương còn sống.
  - Chaos test và replay test có thêm lệnh thả yêu thú.
- **Chạy 200 năm (seed ThienDao):**
  - 28.170 dân, 838 tu sĩ.
  - 63 yêu thú đã sinh ra, 18 còn sống; 3 yêu tộc. Ví dụ năm 41, Thanh Lân Nguyệt Thố lên ngũ giai, xưng Yêu Vương, lập Bách Thú Lĩnh.
  - 12 bí cảnh, 9 đã lộ. Ví dụ năm 28: "【Kế thừa y bát】 … Lý Lãnh tìm về động phủ cũ".
  - 1.751 chuyến thương đội, 1.689 ô thương lộ.
  - Các thời đại nối tiếp: Thái Bình → Bách Tông Tề Phóng (năm 51) → Đại Kiếp (năm 158) → Tân Thời Đại (năm 170) → Bách Tông Tề Phóng (năm 201).
- **Cân chỉnh sau lần chạy đầu:**
  - Yêu thú sinh ra sát tông môn nên bị diệt gần hết: chuyển nơi sinh ra vùng hoang, tăng tỉ lệ khai linh trí, giảm lượng tu vi cần để lên giai, và yêu thú né làng có người bảo vệ mạnh.
  - Thời đại "Đại Chiến" bật quá dễ vì giao tranh nhỏ cũng tính là một trận: nâng ngưỡng.
  - Tên yêu tộc bị trùng: mỗi yêu tộc giờ có tên riêng.
- **Play mode:** ảnh `Docs/m7_danh_nhau.png`. Bên trái Kết Đan đánh ma tu, bên phải yêu thú đánh Trúc Cơ; người trúng đòn hiện thành bóng trắng, có tia lửa và khói pixel.

## Còn để sau
- Thương hội như một loại thế lực riêng; cướp thương đội (ma tu, yêu thú).
- Bí cảnh có bản đồ riêng (GDD để ngoài MVP).
- Yêu tộc như một thế lực đầy đủ trong `FactionSystem` (ngoại giao, lãnh thổ).
