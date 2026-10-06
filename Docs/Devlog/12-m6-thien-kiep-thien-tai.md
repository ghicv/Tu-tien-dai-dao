# 12 · M6 (phần 1): Thiên kiếp & thiên tai

**Ngày:** 2026-10-06

## Lý do
Mốc M6 trong GDD là "đủ các tab quyền năng, thiên kiếp, thiên tai", để vòng CREATE → INTERVENE → CONSEQUENCE chạy hoàn chỉnh. Người chơi chọn làm **thiên kiếp và thiên tai trước**.

## Đã làm

### Thiên kiếp (`CultivationSystem.Tribulation`)
- **Một cơ chế chung** cho thiên kiếp tự nhiên (khi đột phá Nguyên Anh / Hóa Thần) và thiên kiếp do Thiên Đạo giáng.
- **Tỉ lệ sống sót:**
  - Gốc như cũ: 0,5 (đại thiên kiếp 0,3) + 0,3 × tâm cảnh + 0,2 × khí vận.
  - Mỗi pháp bảo +4% (tối đa 3 món).
  - Bế quan tại tông môn được hộ sơn đại trận che chắn: +5%.
  - Thiên kiếp do Thiên Đạo giáng tính theo thân thể người chịu: mỗi đại cảnh giới dưới Kết Đan −6%, trên Kết Đan +6%. Luyện Khí hiếm khi sống.
- **Thiên Đạo giáng thiên kiếp** (`DivineAct.Tribulation`), chỉ lên tu sĩ được chọn; bấm vào chỗ trống thì không có gì xảy ra:
  - Đang ở bình cảnh mà sống sót: phá bình cảnh, lên đại cảnh giới kế tiếp.
  - Chưa tới bình cảnh mà sống sót: được lôi kiếp tôi luyện, +50% tu vi tiểu cảnh giới và tâm cảnh +0,15.
  - Không qua được: vẫn lạc.
- **Sét đánh cả vùng xung quanh** (`DisasterSystem.TribulationStrikes`), bán kính 4 + 2 × cảnh giới (6–16 ô):
  - Cây bị đốt.
  - Nhà có thể sập và phàm nhân trong vùng có người chết. Hai thiệt hại này không xảy ra khi độ kiếp tại tông môn, vì đại trận che chắn.
  - Tu sĩ dưới Kết Đan đang ở gần (đứng xem, hoặc định đánh lén) có thể bị đánh tan xác.

### Lôi địa (lớp `WorldData.Zone`, cờ `ZoneFlags.Thunder`)
- **Nguồn gốc:** mọi lần thiên kiếp, dù sống hay chết, đều để lại một vùng lôi địa có tên riêng (Lôi Cốc, Cửu Tiêu Lôi Đài…), lấy từ `Lore.ThunderPlaces`.
- **Tác dụng:**
  - Trần linh khí trong vùng +25% MaxQi (lôi khí).
  - Phàm nhân không khai hoang và không lập làng ở đó.
  - Đất có ánh tím trên bản đồ, kèm nhãn tên.
- **Tồn tại:** 200–400 năm với thiên kiếp tự nhiên, 300–600 năm với thiên kiếp do Thiên Đạo giáng; sau đó lôi khí tan, có dòng tin báo. Thiên kiếp mới rơi vào lôi địa cũ thì gộp làm một và kéo dài thêm.
- **Nhân quả xuyên thời đại:** tu sĩ từ Trúc Cơ trở lên dời động phủ vào lôi địa hoặc chân núi lửa thì tiểu sử ghi lại, ví dụ:
  - "… lập động phủ ở Lôi Cốc, nơi lôi khí còn sót lại từ thiên kiếp của Lạc Dao năm 177."

### Thiên tai (`Sim/DisasterSystem.cs`, tab mới "Thiên tai")
Mỗi loại là một `CalamityCommand`, đi qua hàng đợi lệnh và nằm trong log replay. Bán kính theo cỡ cọ (`DisasterSystem.Radius`); con trỏ hiện đúng vùng ảnh hưởng.

| Thiên tai | Hiệu ứng |
|---|---|
| **Động đất** | Bán kính 2 × cọ (10–60 ô). Nhà sập theo khoảng cách (tới 70% ở tâm). Mỗi làng mất tới 13% dân. **Linh mạch đứt gãy** (Thiên Đạo 50%, tự nhiên 30% ở tâm), nên trần linh khí tụt và có thể kéo theo tranh linh mạch. Đá lở trên đồi núi. |
| **Núi lửa** | Nón núi bán kính 5, miệng dung nham, 4–7 dòng dung nham dài 10–26 ô. Một **mạch địa hỏa** (linh mạch) mở ra dưới miệng núi. Rừng quanh đó cháy. Tro bụi trong 40 ô giết tới 15% dân và hủy nửa kho lương. Dòng dung nham nguội thành đồi sau 3–12 năm, miệng núi thành núi đá sau 40–90 năm. Núi lửa có tên (`Lore.Volcanoes`) và là địa danh vĩnh viễn. |
| **Lũ lụt** | Vùng trũng (độ cao ≤ 0,6) ngập thành nước nông 2–4 tháng, mép nước lởm chởm. **Ruộng ngập mất luôn**, nước rút thành cỏ và làng phải khai hoang lại. Nhà và quảng trường làng không ngập. Dân chết đuối 1–6%, kho lương ướt mất 30%. Thú và tu sĩ Luyện Khí đứng trong vùng nước bị xử lý như vẽ nước. |
| **Hạn hán** | Bán kính 6 × cọ (40–160 ô), kéo dài 12–30 tháng. Thu hoạch chỉ còn ¼, cỏ héo (×0,6 mỗi tháng) nên thú ăn cỏ rồi tới sói đói theo. Khi hết hạn, tin báo dân trong vùng giảm từ bao nhiêu còn bao nhiêu. |
| **Ôn dịch** | Bấm vào làng. Dịch kéo dài 3–8 tháng, mỗi tháng chết 4–6% dân. **Lây sang làng trong 70 ô** theo đường buôn bán và người chạy nạn. Làng đã qua dịch miễn nhiễm 10 năm. Ôn dịch tự nhiên (1%/làng/năm, trước là 1,5% chết một lần) giờ đi qua cơ chế này. |
| **Thú triều** | 150–300 sói được thả vào vùng. Các làng trong 50 ô mất tới 14% dân; có tông môn che chở thì thiệt hại chỉ còn 35% và tin ghi tông môn "xuất thủ trấn áp". Luyện Khí đang ở ngoài đường có 30% bị xé xác. |

**Thế giới tự sinh thiên tai** (mỗi năm, toàn thế giới):
- Động đất 4%, núi lửa 0,8% (ở đồi núi).
- Lũ ở làng ven sông 5%, hạn hán 5%.
- Thú triều 3%: khi một vùng có trên 60 con sói, bầy sói tràn xuống làng gần nhất rồi bị đánh lui (còn lại 60%).

### Địa hình mới: Dung nham (`Terrain.Lava`)
- Không đi được và không đặt vật thể được.
- Ai rơi vào thì chết cháy: Luyện Khí chết, Trúc Cơ trở lên ngự kiếm thoát ra.
- Làng hoặc đoàn di dân bị dung nham phủ thì bị nuốt chửng.

### Giao diện
- **Tab Thiên Đạo:** thêm nút **Thiên kiếp**. Thẻ tu sĩ cũng có nút này; thẻ làng thì không.
- **Tab mới "Thiên tai":** Động đất, Núi lửa, Lũ lụt, Hạn hán, Ôn dịch, Thú triều, mỗi loại có icon pixel riêng.
- Gợi ý khi rê chuột:
  - Thiên kiếp: tên người sắp chịu kiếp và họ có đang ở bình cảnh không.
  - Thiên tai: bán kính ảnh hưởng.
- **Bảng thông tin:**
  - Ô đất: tên lôi địa / núi lửa và nguồn gốc, số năm lôi khí còn lại, dung nham, số tháng hạn còn lại.
  - Thẻ làng: đang có dịch, đang hạn.
  - Cửa sổ Thống kê: số vùng hạn, ổ dịch, ô ngập, ô dung nham, lôi địa, núi lửa.
- **Hiệu ứng mới:** sóng chấn động và bụi (động đất), cột lửa và tro (núi lửa), khói xanh (ôn dịch), bụi và máu (thú triều).

## Kiểm chứng
- **Test mới** (35/35 pass):
  - `DivineTribulationOpensTheGateOrKillsAndLeavesThunderLand`:
    - Bấm vào chỗ trống thì không có thiên kiếp.
    - Trúc Cơ ở bình cảnh thì lên Kết Đan hoặc chết.
    - Ô đó thành lôi địa, trần linh khí không thấp hơn thế giới đối chứng.
    - Hết hạn thì lôi khí tan.
  - `WorldBringsCalamitiesOnItself`: 150 năm có thiên tai tự nhiên, dân không tuyệt diệt.
  - `EruptionRaisesAVolcanoWhoseLavaCools`: miệng núi là dung nham; sau 14 năm các dòng dung nham đã nguội, miệng núi vẫn còn.
  - `FloodDrownsFieldsThenRecedes`: ruộng mất, làng vẫn còn, 5 tháng sau nước rút hết.
  - `DroughtStarvesTheVillage`, `PlagueRunsItsCourse`, `EarthquakeBreaksLeyLinesAndHouses`, `BeastTideFallsOnTheVillages`.
  - Chaos test và replay test có thêm đủ 6 thiên tai và thiên kiếp; mọi bất biến vẫn giữ.
- **Chạy 200 năm, seed ThienDao, không can thiệp:**
  - Năm 200: 29.327 dân, 105 làng, 843 tu sĩ, 21 thế lực.
  - Một thiên kiếp tự nhiên (Lạc Dao, năm 177) đã để lại lôi địa "Cửu Tiêu Lôi Đài".
  - Ví dụ tin: "Hạn hán gần Thái Nam Thôn chấm dứt sau 16 tháng; dân trong vùng từ 107 còn 81 người."
  - Lần chạy đầu ôn dịch lây quá nhiều (sử sách toàn tin dịch), nên đã giảm tỉ lệ lây từ 10% xuống 5% mỗi tháng và tỉ lệ dịch tự phát từ 1,5% xuống 1%.
- **Play mode:**
  - Gọi núi lửa cạnh Thanh Khâu Thôn: nón núi, dòng dung nham chảy vào rừng, nhãn tên núi (ảnh: "Hỏa Diễm Sơn"). Ảnh: `Docs/m6_nui_lua.png`.
  - Giáng thiên kiếp lên một Kết Đan đại viên mãn của Quỷ Linh Môn: vượt kiếp, đột phá Nguyên Anh; quanh tông môn thành lôi địa "Lôi Cốc" ánh tím. Ảnh: `Docs/m6_thien_kiep.png`.
  - Sửa sau khi xem: sườn núi lửa ban đầu dùng "Đỉnh tuyết" nên trông như núi tuyết, nay đổi thành đá núi. Giáng thiên kiếp cũng không còn đánh dấu người đó "được Thiên Đạo điểm hóa", vì trước đó dấu này làm sinh nhầm truyện "Thiên mệnh chi tử".

## Còn để sau (M6 phần 2)
- Đại kiếp toàn cầu, thời tiết (mưa, bão, tuyết, đổi mùa), tab Quy luật (slider toàn cầu), hồi sinh và diệt một thế lực.
- Đánh lén người đang độ kiếp thành một hành vi chủ động của AI. Hiện mới chỉ có rủi ro bị sét đánh khi đứng gần.
- Lôi địa và núi lửa thành bí cảnh hoặc cấm địa thật (M7). Hạn hán chưa có lớp phủ riêng trên bản đồ.
