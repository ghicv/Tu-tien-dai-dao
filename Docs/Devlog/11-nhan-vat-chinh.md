# 11 · Nhân vật chính: danh sách theo dõi

**Ngày:** 2026-10-06

## Lý do
Người chơi:
- *"người chơi có thể thêm 1 nhân vật nào đó vào danh sách theo dõi, khi đó nhân vật đó sẽ có hành vi như một con người thật trong truyện tu tiên, chứ k đơn thuần là chạy quanh quanh nhà như những NPC khác … tự tạo cho mình 1 nhân vật chính"*
- *"có thể thêm nhiều nhân vật vào để theo dõi, khi ở trong danh sách theo dõi, có thể click vào để follow"*

## Đã làm

### Theo dõi là một lệnh của Thiên Đạo
- `WatchCommand(index, on)`:
  - Đi qua hàng đợi lệnh và nằm trong log như mọi quyền năng khác, vì người được theo dõi sống theo AI riêng nên mô phỏng thay đổi. Replay vẫn ra đúng kết quả: test `WatchingIsPartOfTheReplayableLog`.
  - Theo dõi bao nhiêu người cũng được. Người đã chết vẫn bỏ khỏi danh sách được.
- **Người được theo dõi:**
  - Không còn đi chơi ngẫu nhiên hay tự dời động phủ như NPC thường; mọi quyết định do `ProtagonistAI` đưa ra.
  - Bế quan khổ tu thì tu vi tăng ×1,3.

### ProtagonistAI (`Sim/ProtagonistAI.cs`)
- **Nhịp sống:** mỗi tháng, khi đang rảnh, nhân vật cân nhắc theo thứ tự:
  1. **Bị kẻ mạnh hơn 1,3 lần truy sát:** lặng lẽ bỏ động phủ, trốn xa trên 200 ô, sang vùng vô chủ.
  2. **Có huyết thù và đã đủ sức** (≥ 0,9 lần kẻ thù): lên đường báo thù, dùng `CombatSystem.StartHunt`.
  3. **Tán tu từ Trúc Cơ trở xuống:** tìm đến tông môn gần nhất cùng chính/ma để **bái sư** và được giao sư phụ.
  4. **Tán tu Kết Đan+ có dã tâm:** **khai tông lập phái**, dùng chung luật với thế lực (`FactionSystem.TryFound`).
  5. **Trưởng lão Kết Đan+:** khoảng 20 năm một lần **thu đệ tử chân truyền** từ Luyện Khí của tông, ưu tiên người chưa có thầy.
  6. **Kẹt bình cảnh mà chưa có đan:**
     - Đủ linh thạch thì vào **phường thị** (khu dân cư lớn nhất trong 300 ô) mua đan cho cửa ải kế tiếp.
     - Giá đan: Trúc Cơ Đan 40, Kết Kim Đan 120, Bổ Thiên Đan 360, Hoàng Long Đan 1.080 linh thạch.
     - Không đủ thì đi **tìm cơ duyên**.
  7. **Dư linh thạch:** ra phường thị mua **pháp bảo**. Giá 250 × cảnh giới × (số pháp bảo đang có + 1), tối đa 3 món.
  8. **Linh khí nơi ở quá loãng** (tán tu): đi tìm động phủ mới giàu linh khí hơn ít nhất 30%.
  9. **Còn lại:** bế quan 1–3 năm (45%), lịch luyện (30%) hoặc tìm cơ duyên (25%).
- **Kết quả khi tới nơi:**
  - **Lịch luyện:** ngộ tính +0,03, tâm cảnh +0,02, được linh thạch, có một chuyện nhỏ (chém yêu lang, luận đạo, ngộ kiếm quyết…). 12% gặp yêu thú cường đại.
  - **Tìm cơ duyên** (vùng hoang nhiều linh khí, không thuộc tông nào):
    - 8%: động phủ cổ tu sĩ, được **pháp bảo** có tên (Thanh Trúc Phong Vân Kiếm, Phong Lôi Sí…).
    - 12%: tìm được đan.
    - 30%: hái **linh thảo** (+25% tu vi tiểu cảnh giới, phần còn lại bán lấy linh thạch).
    - 12%: chạm **cấm chế cổ**.
    - Còn lại: về tay trắng.
  - **Nguy hiểm** (yêu thú, cấm chế): Luyện Khí 15% vẫn lạc, Trúc Cơ 6%, Kết Đan+ 2%. Sống sót thì mất 25% tu vi và phải về tĩnh dưỡng.
  - **Về đến nhà:** luôn **tĩnh tu** 6–18 tháng trước khi đi tiếp.
  - **Xuất quan:** ghi "tu vi tinh tiến tới …", hoặc "tu vi vẫn dừng ở …, bình cảnh khó phá".
- **Túi trữ vật:**
  - **Linh thạch** đến từ: tông phát mỗi năm (1 + 2 × cảnh giới), lịch luyện, bán linh thảo, và đoạt của người mình giết.
  - **Đan cá nhân:** cộng 20% tỉ lệ đột phá; dòng tin ghi "đột phá … nhờ Kết Kim Đan".
  - **Pháp bảo:** mỗi món +12% sức đấu pháp, tối đa 3 món. Giết người thì đoạt cả linh thạch lẫn pháp bảo của người đó.

### Hiển thị và thao tác
- **Thẻ tu sĩ:**
  - Nút **☆ Theo dõi** / **★ Bỏ theo dõi**. Nút camera đổi tên thành "Camera" để không nhầm.
  - Có dòng **Túi trữ vật**: linh thạch, đan, pháp bảo.
- **Danh sách theo dõi** (góc dưới phải):
  - Mỗi người một dòng: tên, cảnh giới, đang làm gì, hoặc "đã vẫn lạc năm …".
  - **Bấm vào tên thì camera bay tới và bám theo**, kể cả khi người đó đang bế quan, vì nhân vật chính luôn được vẽ.
  - Nút × để bỏ theo dõi.
- **Trên map:**
  - Nhân vật chính **luôn được vẽ**, kể cả khi bế quan ở nhà, kèm vầng sáng vàng và nhãn "★ Tên · cảnh giới".
  - Họ không bị lặp trong đám hình tu sĩ đứng ở tông môn.
- **Dòng tin:** mọi chuyện liên quan nhân vật chính (là chủ thể hay đối phương) đều hiện, với dấu ★ màu vàng nhạt, dù quan trọng ít hay nhiều. Tất cả cũng vào **Tiểu sử** trong thẻ và Biên niên sử.
- **Bảng cường giả:** bấm vào một nhân vật chính đang bế quan thì camera vẫn theo, không báo "đang bế quan".

## Kiểm chứng
- **Một tán tu Luyện Khí được theo dõi 60 năm** (seed PhamNhan):
  - Năm 1: bái sư Linh Thú Sơn, đi tìm cơ duyên và hái được Ngọc Tủy Chi.
  - Năm 3–6: bế quan, lên Luyện Khí tầng 13.
  - Năm 8–9: mua Trúc Cơ Đan ở phường thị, nhờ đó **đột phá Trúc Cơ**.
  - Sau đó: nhiều lần lịch luyện, hai lần trọng thương vì yêu thú; năm 40 tìm được Kết Kim Đan trong di tích cổ.
  - Năm 59: lên **Trúc Cơ đại viên mãn**, túi có 983 linh thạch.
- **Một Trúc Cơ khác:** bị tông điều ra trận tranh linh mạch năm 11 và tử trận. Nhân vật chính không có hào quang bất tử.
- **Test:**
  - `WatchedCultivatorLivesByGoals`: tiểu sử sau 40 năm có ít nhất 6 sự tích.
  - `WatchingIsPartOfTheReplayableLog`.
  - 27/27 test pass.
- **Play mode:** theo dõi 3 người; danh sách, thẻ, dòng tin ★ và nhãn trên map hiển thị đúng. Ảnh: `Docs/watch_list.png`.

## Còn để sau
- Đạo lữ, kết nghĩa, kẻ thù không đội trời chung giữa hai nhân vật chính.
- Bí cảnh và cấm địa thật trên map để đi thám hiểm (M7).
- Cho nhân vật chính một "cốt truyện" có hồi: mục tiêu dài hạn như phi thăng hay phục hưng tông môn.
