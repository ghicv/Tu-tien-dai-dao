# 30 · Chính trị nước phàm nhân và gia tộc

**Ngày:** 2026-10-08

## Lý do
- Người chơi: *"oke làm chính trị nước phàm nhân với gia tộc đi bro"*.
- Đây là luật N4 và N5 trong `Docs/WorldRuleSystem.md`. `WorldRules.md` yêu cầu:
  - §7: Family, Clan, Nation.
  - §10: Clan conflict, Rebellion, Civil war.
  - §13: Civilization rise and fall.
- Trước đây nước phàm nhân chỉ là vùng đất có tên và một kinh thành.

## Đã làm

### Vua và triều đại (`Sim/PoliticsSystem.cs`)
- **Mỗi nước có:**
  - Một vị vua thuộc một dòng họ (triều đại), với tuổi và tính cách nhân từ, bình thường hay bạo ngược.
  - Số năm của triều đại, và độ ổn định: 100 trừ bất mãn trung bình của dân, tính theo số dân từng thành.
- **Vua băng hà:** già thì chết. Vua bạo ngược ở nước rối ren có thể bị ám sát.
  - Thái tử đã trưởng thành thì nối ngôi, tính cách có thể khác cha.
  - Thái tử nối ngôi êm 85% số lần. Không có người nối dõi thì ấu chúa lên ngôi, quyền thần nhiếp chính, độ ổn định −20. Lúc đó hoàng thúc ở thành lớn nhất có 35% khả năng không phục, gây **nội chiến tranh ngôi**.

### Bất mãn của từng thành (`Settlement.Unrest`, 0–100)
- **Mỗi năm, bất mãn trôi dần về mức hợp với năm vừa qua của thành:**

| Làm tăng | Làm giảm |
|---|---|
| Có người chết đói (+35) | Có tông môn bảo hộ (−8) |
| Chết nhiều hơn sinh (+10) | Tín ngưỡng ≥ 60 (−5) |
| Hạn hán hoặc ôn dịch (+15) | Gia tộc tu tiên trên ngai vàng (−8) |
| Vua bạo ngược (tới +25; vua nhân từ −10) | Là kinh thành (−30) |
| Ở xa kinh đô trên 180 ô (+10) | |
| Nước đang nội chiến mà thành không theo phe nào (+8) | |

### Khởi nghĩa và nội chiến
- **Nổ ra khi:** một thành từ 120 dân trở lên, không phải kinh thành, có bất mãn ≥ 78. Mỗi năm 4% khả năng thành đó khởi nghĩa.
- **Ai cầm đầu:** nếu thành là đất tổ của một gia tộc tu tiên, chính gia tộc đó khởi binh; không thì là một thủ lĩnh nghĩa quân người phàm.
- **Ai theo:** các thành trong vòng 160 ô có bất mãn ≥ 55.
- **Nội chiến kéo dài 2–5 năm:** mỗi năm hai bên mất 0,5–1,5% dân; kinh thành và thành theo nghĩa quân mất nhiều hơn.
- **Hai bên mạnh đến đâu:**
  - **Nghĩa quân:** dân của các thành theo × (0,8 + bất mãn), cộng sức gia tộc cầm đầu.
  - **Triều đình:** dân các thành trung thành × (1 − bất mãn của thành đó) × (0,6 + 0,6 × nhân từ). Thành trung thành mà dân đang bất mãn thì đánh hời hợt, và vua bạo ngược thì ít người liều chết vì vua. Cộng một phần sức tông môn bảo hộ kinh thành, vì tu sĩ chỉ ra tay giúp chứ không thành đại quân.
  - **Sức triều đình giảm theo khoảng cách:** ở thành nổi dậy chỉ còn 1 − khoảng cách/400 (ít nhất 30%). Xa kinh đô thì triều đình giữ được ngai vàng nhưng không giữ nổi đất.
- **Ba kết cục:**
  1. **Dẹp yên:** các thành nổi dậy bị thanh trừng (mất thêm 5% dân), bất mãn về 35.
  2. **Đổi triều:** nghĩa quân mạnh hơn cả triều đình và có từ 35% dân trở lên (hoặc là tranh ngôi), nên chiếm được kinh đô. Người cầm đầu lên ngôi, lập triều mới; nếu là gia tộc tu tiên thì gia tộc đó thành **hoàng tộc**.
  3. **Cát cứ:** nghĩa quân không lật được ngai vàng nhưng mạnh hơn sức triều đình vươn tới được, nên giữ đất của mình và tách ra lập **nước mới**.
     - Biên giới được vẽ lại trên map, mỗi ô thuộc kinh đô nào gần hơn, theo cùng kiểu nhiễu như lúc sinh map.
     - Nước mới lấy tên còn trống của đại vực, hoặc thêm hướng vào tên nước cũ ("Tây Lương Quốc").
- **Tông môn chọn phe:** nếu tông bảo hộ kinh thành khác tông bảo hộ thành nổi dậy, thì sau khi nghĩa quân thắng, hai tông thêm thù (−25).
- **Thống nhất:** nước cát cứ đang rối ren (ổn định ≤ 45) có thể bị nước mẹ đang ổn định (≥ 60) thu phục, 5% mỗi năm: "giang sơn về một mối".
- **Phục quốc:** nước đã diệt vong mà được di dân dựng lại thì có triều đại mới.

### Gia tộc (`Sim/ClanSystem.cs`)
- **Lập gia tộc:** tu sĩ Kết Đan trở lên có 4% mỗi năm trở về quê và dựng **tu tiên gia tộc** mang họ mình ("Hàn gia"). Gia tộc đủ mạnh gọi là "thế gia"; lên ngôi thì là "hoàng tộc".
- **Con cháu:**
  - Trẻ thức tỉnh linh căn ở đất tổ, 70% được gia tộc thu nhận và **đổi sang họ của gia tộc**.
  - Áp dụng cho cả người được Thiên Đạo ban linh căn và người thức tỉnh nhờ tín ngưỡng.
  - Thành viên gia tộc vẫn vào tông môn như thường; gia tộc là huyết thống, không thay cho tông môn.
- **Báo thù:** giết một người của gia tộc thì hai người mạnh nhất trong tộc (chưa có kẻ thù nào khác) thề truy sát kẻ giết. Phần truy sát dùng hệ huyết thù có sẵn.
- **Thế thù:** kẻ giết cũng thuộc một gia tộc thì hai tộc thành thế thù. Người hai tộc gặp nhau ngoài đường thì đánh, bất kể thuộc tông nào.
- **Gia tộc và ngai vàng:**
  - Gia tộc ở thành nổi dậy có thể cầm đầu khởi nghĩa và lên ngôi.
  - Gia tộc tu tiên trên ngai vàng làm dân cả nước bớt bất mãn; uy danh gia tộc ×1,5.
- **Tuyệt tự:** không còn ai mang họ trong 30 năm thì gia tộc mất. Đất tổ bị bỏ hoang thì gia tộc dời về làng gần nhất nơi con cháu còn sống.

**Chuỗi phản ứng mẫu:** hạn hán ba năm → dân đói, bất mãn → Hàn gia ở thành đó khởi binh → nội chiến → Hàn gia chiếm kinh đô, lên ngôi → dân bớt bất mãn → con cháu Hàn gia giết người của Lý gia → thế thù kéo dài nhiều đời.

### Hiển thị
- **Thẻ làng:**
  - Dòng triều đình: vua, tuổi, tính cách, triều đại và số năm, hoàng tộc (nếu có), độ ổn định.
  - Dòng nội chiến nếu nước đang có.
  - Chip **bất mãn** (biểu tượng ngọn đuốc pixel mới), chip **Khởi nghĩa / Tranh ngôi** nếu thành đang theo nghĩa quân.
  - Dòng "Đất tổ của … gia", kèm số tu sĩ cùng họ và các mối thế thù.
- **Thẻ tu sĩ:** chip gia tộc (vàng nếu là hoàng tộc, đỏ nếu đang thế thù), tooltip ghi đất tổ, uy danh, các mối thù.
- **Map:** nhãn đỏ "Khởi nghĩa · …" hoặc "… tranh ngôi" phía trên thành bắt đầu cuộc nổi dậy. Biên giới nước tự vẽ lại khi cát cứ hoặc thống nhất.
- **Sử sách:** vua băng hà hoặc bị ám sát, nối ngôi, khởi nghĩa, tranh ngôi, dẹp yên, đổi triều, cát cứ, thống nhất, lập gia tộc, thế thù, tuyệt tự.

## Kết quả
- **Test EditMode:** 61/61. Hai test mới:
  - `KingdomsHaveKingsAndARisingEndsInOneOfThreeWays`: nước nào cũng có vua. Dưới vua bạo ngược, dân bất mãn thì khởi nghĩa, và nội chiến kết thúc bằng một trong ba kết cục.
  - `ClansRiseOnAncestralLandAndAvengeTheirOwn`: dựng gia tộc; trẻ thức tỉnh ở đất tổ vào tộc và đổi họ; giết người của tộc thì tộc trưởng truy sát kẻ giết.
- **Cân bằng:** chỉnh qua 4 lần đo 1000 năm (seed ThienDao).

| Lần đo | Nội chiến | Dẹp yên / đổi triều / cát cứ | Phàm nhân năm 1000 |
|---|---|---|---|
| 1. Bản đầu | 202 | 196 / 5 / 0 | 29,0 nghìn |
| 2. Khởi nghĩa thưa hơn, triều đình theo lòng nhân từ | 190 | 176 / 13 / 0 | 27,3 nghìn |
| 3. Tranh ngôi ít hơn, bớt đẫm máu | 132 | 120 / 11 / 0 | 29,4 nghìn |
| 4. Sức triều đình giảm theo khoảng cách (bản cuối) | **117** | **102 / 9 / 5** | **31,9 nghìn** |

- **Bản cuối, sau 1000 năm:**
  - Khoảng 1 cuộc nội chiến mỗi 8,5 năm trên toàn thế giới. Phần lớn bị dẹp yên; thỉnh thoảng đổi triều; năm lần cát cứ lập nước mới (17 nước thành 22).
  - Tu sĩ còn sống 791. Có 59 gia tộc tu tiên, giữa chúng có 145 mối thế thù.
- **Hiệu năng:**
  - Hệ Chính trị và Gia tộc tốn 0,3–0,8 ms/năm, vì chỉ chạy theo năm.
  - Vẽ lại biên giới khi cát cứ hay thống nhất chỉ xảy ra vài lần mỗi nghìn năm.
  - Tổng năm 1000 là 112 ms/năm.
- **Còn để ý:**
  - Chưa có lần thống nhất nào trong 1000 năm, vì điều kiện (nước mẹ ổn định ≥ 60, nước con ≤ 45) khá chặt.
  - Thế thù giữa các gia tộc nhiều dần theo thời gian; về sau có thể cần luật giảng hòa hoặc liên hôn.
