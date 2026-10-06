# 13 · M6 (phần 2): Quy luật, sinh tử, đại kiếp, thời tiết

**Ngày:** 2026-10-06

## Lý do
Hoàn thành phần còn lại của tab quyền năng Thiên Đạo trong GDD §14: Quy luật, Sinh/Tử, Thời tiết và đại kiếp toàn cầu. Kèm theo là việc còn để lại từ devlog 12: kẻ thù đánh lén người đang độ kiếp, và lớp phủ thiên tai trên bản đồ.

## Đã làm

### Quy luật (`Sim/WorldRules.cs`, tab "Quy luật")
- **Cách hoạt động:** 7 luật toàn thế giới. Mỗi lần đổi là một `SetRuleCommand`, đi qua log replay và được hash. Giá trị được làm tròn theo bước của từng luật, nên cùng thao tác bấm luôn ra cùng một giá trị.

| Luật | Khoảng | Tác dụng |
|---|---|---|
| Linh khí thiên địa | ×0,2 – ×2 | Linh khí cả thế giới hồi về mức này nhân với trần tự nhiên (`QiSystem.MonthlyStep(regen, scale)`); hình dạng linh mạch giữ nguyên. |
| Tỉ lệ linh căn | ×0 – ×5 | Số trẻ thức tỉnh mỗi năm; ×0 thì không còn ai bước lên tiên lộ. |
| Tỉ lệ đột phá | ×0,25 – ×4 | Nhân với tỉ lệ đột phá đại cảnh giới. |
| Thiên kiếp khắc nghiệt | ×0 – ×3 | Nhân với khả năng vẫn lạc dưới thiên kiếp. |
| Sinh sản | ×0,25 – ×3 | Tốc độ sinh của phàm nhân và thú hoang. |
| Thiên tai | ×0 – ×5 | Tần suất thiên tai tự nhiên và ôn dịch; ×0 là thiên hạ thái bình. |
| Ma đạo | bật / tắt | Tắt thì tu sĩ tẩu hỏa chỉ tụt tu vi, không sa vào ma đạo; tông môn cũng không hóa ma. |

- **Giao diện:** mỗi luật là một ô có nút − / + (Ma đạo là công tắc). Giá trị khác mặc định hiện màu đỏ cam, có nút "Mặc định" để trả hết về như cũ, và cửa sổ Thống kê liệt kê các luật đã đổi.
- Ở giá trị mặc định mọi hệ số đều là ×1, nên mô phỏng cho kết quả y hệt trước khi có tab này.

### Sinh / Tử
- **Hồi sinh** (`DivineAct.Revive`, `CultivationSystem.Revive`), nút "✚ Hồi sinh" trên thẻ người đã chết:
  - Người đó sống lại tại động phủ (hoặc chỗ đất liền gần nhất). Tông môn đã mất thì thành tán tu.
  - Thọ nguyên còn lại ít nhất 100 năm, tâm cảnh +0,2.
  - **Kẻ đã giết họ, nếu còn sống, trở thành huyết thù.** Đủ mạnh thì người được hồi sinh lên đường "đòi lại món nợ máu đã lấy mạng mình". Giết được thì StoryDetector ghi truyện mới **"Trùng sinh báo thù"**.
  - Bấm vào người đã chết trong danh sách theo dõi giờ mở thẻ của họ, để có thể hồi sinh.
- **Diệt môn** (`DivineAct.Annihilate`, `FactionSystem.Annihilate`), có trong tab Thiên Đạo và trên thẻ tông môn:
  - Mọi tu sĩ đang ở trong sơn môn vẫn lạc; người đang ở ngoài sống sót thành tán tu.
  - Đại điện bị đốt, lãnh thổ được giải phóng, thị trấn tông môn thành làng thường.
  - Nơi đó hóa lôi địa 300–600 năm.

### Đại kiếp toàn cầu (`Calamity.GreatCalamity`)
- Kéo dài 8–15 năm. Linh khí cả thế giới chỉ còn một nửa (`Simulation.QiScale` = luật × `DisasterSystem.QiFactor`).
- Mỗi năm có 2–5 thiên tai ngẫu nhiên: động đất, hạn, lũ, dịch, thú triều, núi lửa, rét.
- Khi kết thúc, tin báo tổng kết: phàm nhân từ bao nhiêu còn bao nhiêu, tu sĩ từ bao nhiêu còn bao nhiêu, "thiên địa bước vào thời đại mới".
- Tự xảy ra khoảng 0,15% mỗi năm (khoảng 7 thế kỷ một lần), nhân theo luật Thiên tai.

### Thời tiết (tab "Thời tiết")
| | Hiệu ứng |
|---|---|
| **Mưa** | Bán kính 6 × cọ, kéo dài 4–8 tháng: thu hoạch ×1,3, cỏ xanh lại. **Mưa rơi vào vùng hạn thì hạn hán chấm dứt** (có tin tổng kết thiệt hại). |
| **Bão** | Cuồng phong đi theo một đường dài 60–120 ô, rộng theo cỡ cọ: cây đổ, 30% số nhà trên đường bão tốc mái, mỗi làng bị quét mất 3% dân. |
| **Rét** | Bán kính 5 × cọ, kéo dài 3–5 tháng: thu hoạch ×0,1, cỏ héo, mỗi tháng 1,5% dân chết cóng. Hết rét có tin báo tổng số người chết cóng. |

- Thế giới tự sinh: bão 6% mỗi năm, rét 4% mỗi năm; mỗi vùng đang hạn có 30% mỗi năm được mưa cắt hạn.

### Đánh lén lúc độ kiếp (`CultivationSystem.Ambush`)
- **Ai có thể ra tay:** người mang huyết thù với người đang độ kiếp (60%), hoặc một ma tu khác phe ham túi trữ vật (15%). Phải ở trong vòng 250 ô và cảnh giới không thấp hơn quá một bậc. Kẻ mạnh nhất sẽ ra tay; người mang huyết thù được ưu tiên gấp đôi.
- **Kết quả:** người đang độ kiếp chỉ chống trả bằng một nửa sức.
  - Kẻ đánh lén thắng: người độ kiếp "thân tử đạo tiêu", bị cướp túi trữ vật, và đệ tử của họ thề báo thù.
  - Kẻ đánh lén thua: bị phản sát "thân xác tan dưới lôi kiếp".
  - Thắng hay thua, sét vẫn đánh và vẫn để lại lôi địa.

### Lớp phủ "Thiên tai" (phím Tab hoặc tab Lớp phủ)
Dung nham màu cam, ổ dịch màu xanh lá, vùng rét màu trắng, vùng hạn màu cam nhạt, vùng mưa màu xanh dương, lôi địa màu tím.

## Kiểm chứng
- **Test mới** (42/42 pass):
  - `RulesRewriteTheWorld`: linh căn ×0 thì không ai thức tỉnh trong 10 năm; linh khí ×0,3 thì tổng linh khí dưới 60% thế giới đối chứng; giá trị vượt khoảng bị kẹp lại.
  - `RevivedCultivatorReturnsWithAGrudge`: sống lại, huyết thù với kẻ giết, còn ít nhất 99 năm thọ; hồi sinh người đang sống không có tác dụng.
  - `AnnihilatedSectLeavesThunderLand`, `GreatCalamityDimsTheWorldThenPasses`, `RainBreaksDroughtAndColdKills`, `StormTearsDownTrees`.
  - `EnemiesStrikeDuringTribulation`: 10 người độ kiếp, mỗi người có kẻ thù ở gần; phải có ít nhất một vụ đánh lén.
  - Chaos test có thêm bão, rét, mưa, đại kiếp, hồi sinh, diệt môn và đổi quy luật ngẫu nhiên; replay test có thêm đổi quy luật, bão, rét, mưa, đại kiếp và hồi sinh.

## Còn để sau
- Đổi mùa thủ công: chưa làm, vì nhảy lịch sẽ phá nhịp tick.
- Bí cảnh và cấm địa thật trên lôi địa, núi lửa, cổ chiến trường (M7).
