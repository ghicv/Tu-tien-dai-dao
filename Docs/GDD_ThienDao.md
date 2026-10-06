# THIÊN ĐẠO — Xianxia World Simulation (GDD v0.1)

> Người chơi là Thiên Đạo. Không điều khiển ai. Tạo thế giới → quan sát → can thiệp → chứng kiến lịch sử tự sinh.
>
> Core loop: **CREATE → SIMULATE → OBSERVE → INTERVENE → CONSEQUENCE → HISTORY**

---

## 0. Ba nguyên tắc thiết kế xuyên suốt

1. **Simulation LOD (mức chi tiết theo tầm quan trọng).** Không mô phỏng mọi phàm nhân như một cá thể đầy đủ. Phàm nhân trong làng/thành là *dân số dạng số liệu*; chỉ cá thể "đáng chú ý" (tu sĩ, có linh căn, lãnh đạo, yêu thú mạnh, người vừa gặp kỳ ngộ) mới được "nâng cấp" thành entity đầy đủ. Đây là chìa khóa để chạy hàng triệu năm mà vẫn nhẹ.
2. **Mọi thứ là dữ liệu + quy luật, không phải script.** Câu chuyện sinh ra từ: nhu cầu khan hiếm + tính cách + quan hệ + xác suất. Không viết sự kiện cốt truyện.
3. **Simulation tách khỏi rendering.** Simulation chạy trên mảng dữ liệu thuần C#, deterministic theo seed. Rendering chỉ đọc state và vẽ.
4. **Toàn bộ tên gọi và lore dựa trên Phàm Nhân Tu Tiên:** tông môn, địa danh (Thôn / Trấn / Thành), đan dược, linh thạch, yêu thú, cảnh giới. Danh mục tên nằm ở `Assets/ThienDao/Sim/Lore.cs`; nội dung mới phải lấy tên từ đây hoặc bổ sung vào đây.

---

## 1. Kiến trúc tổng thể

```
WorldData (mảng thuần)        ← nguồn sự thật duy nhất
   ↓
SimulationClock (fixed tick)
   ↓
Systems: Climate · Qi · Ecology · Entity · Cultivation · Society · Economy · War · Realm
   ↓
EventBus → HistoryLog → StoryDetector
   ↓
Rendering (texture + sprite instancing)      Player Interaction (God Powers → Commands)
```

- **Player không sửa state trực tiếp.** Mỗi quyền năng tạo một `Command` đẩy vào hàng đợi, được áp dụng ở đầu tick tiếp theo → giữ deterministic, dễ replay/undo.
- **Deterministic:** RNG riêng theo hệ (xorshift/PCG, seed = worldSeed ^ systemId ^ tick). Không dùng `UnityEngine.Random` trong simulation. Thứ tự duyệt entity cố định (theo id).
- **Thư mục code đề xuất:**
  ```
  Assets/ThienDao/
    Core/        (Clock, Rng, Command, EventBus)
    World/       (WorldData, MapGen, Chunk)
    Systems/     (Qi, Climate, Ecology, Cultivation, Society, Economy, War, Realm)
    Entities/    (EntityStore, Components dạng struct array)
    History/     (HistoryLog, StoryDetector, Chronicle)
    Render/      (TerrainRenderer, OverlayRenderer, UnitRenderer, BuildingRenderer)
    Player/      (GodPowers, Brush, Inspector)
    Data/        (ScriptableObject: Realm, Root, Technique, Item, Biome…)
  ```

### 1.1 Thời gian & tick

| Đơn vị | Giá trị | Hệ chạy ở tần suất này |
|---|---|---|
| 1 tick | 1 ngày | di chuyển, chiến đấu, nhu cầu cá thể |
| 30 tick | 1 tháng | tu luyện, sản xuất, thương mại |
| 90 tick | 1 mùa | khí hậu, mùa màng, sinh sản động vật |
| 360 tick | 1 năm | dân số làng, ngoại giao thế lực, già/chết, sinh bí cảnh |

- Không phải hệ nào cũng chạy mỗi tick: **staggered update** (chia entity thành 30 nhóm, mỗi ngày cập nhật 1 nhóm cho tác vụ "tháng").
- Tốc độ: Pause / x1 / x5 / x20 / "Tua thời đại" (chỉ chạy hệ năm, bỏ qua render, để vượt hàng nghìn năm).

---

## 2. Thế giới (World Data)

### 2.1 Lớp dữ liệu theo cell (map W×H, mặc định 1024×1024)

| Lớp | Kiểu | Ý nghĩa |
|---|---|---|
| `height` | byte | độ cao → biển/đồng bằng/đồi/núi |
| `terrain` | byte | đất, cát, đá, tuyết, nước nông, nước sâu, dung nham, hang |
| `biome` | byte | đồng cỏ, rừng, rừng rậm, sa mạc, đầm lầy, tundra… |
| `moisture`, `temperature` | byte | sinh từ noise + vĩ độ, dao động theo mùa |
| `qi` (linh khí) | ushort | nồng độ linh khí hiện tại |
| `qiCap` | ushort | mức trần tự nhiên (do linh mạch quyết định) |
| `fertility` | byte | độ màu mỡ, cạn dần khi canh tác/khai thác |
| `vegetation` | byte | mật độ cây/linh thảo |
| `ore` | byte | loại khoáng + trữ lượng |
| `ownerFaction` | ushort | thế lực sở hữu cell (0 = vô chủ) |
| `zoneFlag` | byte | bit: phúc địa, tử địa, cấm địa, bí cảnh, ô nhiễm ma khí… (đã có: lôi địa, `WorldData.Zone`) |
| `building` | ushort | id công trình đang chiếm cell (một công trình chiếm nhiều cell theo footprint) |

Tất cả là **mảng phẳng** index `y*W + x`. Target **PC**: map mặc định 1024², tùy chọn 2048². 1024² × ~20 byte ≈ 20MB.

### 2.1b Cell và vật thể (quan trọng)

- **Cell** là đơn vị mô phỏng nhỏ nhất (địa hình, linh khí, sở hữu). Một cell **không phải** một ngôi nhà.
- **Vật thể** (nhà, cây, tông môn, đá, sinh vật) có **footprint nhiều cell** và hình vẽ pixel art chi tiết hơn cell:

| Vật thể | Footprint (cell) | Ghi chú |
|---|---|---|
| Cây nhỏ / bụi | 1×1 | sprite cao hơn footprint (tán cây che lên ô phía trên) |
| Cây lớn / linh mộc | 2×2 | |
| Nhà dân | 3×3 | |
| Nhà lớn, chợ, luyện đan phòng | 4×4 | |
| Tường thành / đường | 1×1 nối tile | autotile theo hàng xóm |
| Sơn môn / đại điện tông môn | 6×6 – 10×10 | |
| Người / thú nhỏ | điểm (không chiếm cell) | |
| Yêu thú lớn | 2×2 – 4×4 | |

- Logic chỉ dùng footprint (chặn đường, chiếm đất, cháy lan). Hình vẽ chỉ là trang trí phía trên.

### 2.2 Linh khí & linh mạch

- **Linh mạch** là các polyline/đồ thị nằm dưới địa hình (sinh lúc tạo map, thường theo dãy núi). Mỗi nút có cấp (1–9) và loại ngũ hành.
- Mỗi năm: `qiCap` của cell = tổng ảnh hưởng các nút linh mạch lân cận (giảm theo khoảng cách). `qi` hồi dần về `qiCap`, **khuếch tán** sang cell kề (diffusion đơn giản, chạy trên chunk thấp phân giải 1/8 để nhẹ).
- **Tu sĩ hấp thụ** làm giảm `qi` tại chỗ → vùng quá đông tu sĩ sẽ "cạn" → động lực tranh linh mạch tự nhiên.
- Khai thác linh thạch quá mức làm **suy linh mạch** (giảm cấp vĩnh viễn) → phúc địa có thể biến thành vùng chết sau hàng nghìn năm.
- **Động thiên / phúc địa:** vùng `qiCap` cao bất thường, hiếm.
- **Tử địa / cấm địa:** sinh ra *từ sự kiện* (đại chiến chết nhiều cường giả → ma khí; thiên kiếp lớn → vùng sét; tử thi cường giả → oán khí). Có thời gian hồi phục rất dài.

**Đã cài (M1):**
- `QiCap` từng ô = `QiBase` (noise + phúc địa) + ảnh hưởng linh mạch `(1 − d/60)² × 0.8`; nước nhân 0.4. Vẽ/phá linh mạch hoặc đổi địa hình chỉ tính lại vùng bị ảnh hưởng.
- Linh khí hiện tại sống trên lưới thô 128×128 (khối 8×8 ô). Mỗi tháng: khuếch tán 18% về trung bình 4 hàng xóm, rồi hồi 6% khoảng cách tới trần (thừa thì tản bớt). Hệ số hồi theo mùa: Xuân ×1.25, Hạ ×1, Thu ×0.85, Đông ×0.6.
- Linh khí một ô = trần của ô + phần thừa/thiếu nội suy từ các khối xung quanh, nên linh mạch vẫn sắc nét khi một vùng bị hút cạn.
- Hiện vùng bị hút cạn hồi về mức cũ sau khoảng 3 năm. Khi có tu sĩ (M3) sẽ chỉnh lại `RegenPerMonth` cho phù hợp.

### 2.3 Khí hậu, ngày/đêm, mùa

- Nhiệt độ = base theo vĩ độ + độ cao + offset mùa. Độ ẩm theo khoảng cách tới biển/sông + gió đơn giản.
- Thời tiết theo vùng chunk lớn (64×64): mưa, hạn, bão, tuyết. Hạn kéo dài → mất mùa → đói → di cư/chiến tranh.
- Ngày/đêm chỉ ảnh hưởng render (tint) và vài hành vi (ma vật mạnh ban đêm). Không tính trong tick ngày để giữ nhẹ.

### 2.4 Sinh map

1. Height: fractal noise + mask đảo/lục địa. 2. Sông: chảy theo gradient từ núi ra biển. 3. Nhiệt/ẩm → biome (bảng Whittaker). 4. Linh mạch dọc dãy núi + vài nút ngẫu nhiên. 5. Khoáng theo đá/núi. 6. Rải động vật, vài bộ lạc phàm nhân ban đầu.
Toàn bộ từ `worldSeed` → cùng seed ra cùng thế giới.

---

## 3. Sinh mệnh (Entities)

### 3.1 Phân tầng LOD

| Tầng | Đại diện | Lưu trữ |
|---|---|---|
| **Quần thể** | phàm nhân trong làng, động vật thường | số đếm theo độ tuổi/giới trong Settlement / theo chunk |
| **Cá thể** | tu sĩ, người có linh căn, lãnh đạo, yêu thú, linh thú, ma vật | entity đầy đủ (struct arrays) |
| **Huyền thoại** | cường giả, nhân vật lịch sử | entity + tiểu sử trong HistoryLog, không bị dọn khi chết |

- **Promotion:** mỗi năm, một làng sinh N trẻ; mỗi trẻ roll linh căn (~1–5% có linh căn tùy linh khí vùng). Có linh căn → tạo entity. Phàm nhân trở thành lãnh đạo, anh hùng, kẻ phản loạn → cũng được tạo entity.
- **Demotion:** entity tầm thường chết đi không có sự kiện đáng chú ý → chỉ để lại một dòng thống kê.
- Mục tiêu hiệu năng: **5.000–20.000 entity cá thể** cùng lúc, dân số quần thể không giới hạn thực tế.

### 3.2 Dữ liệu một entity cá thể

```csharp
// Struct-of-arrays trong EntityStore, đây là "khái niệm" một entity
id, species, sex, birthTick, posX, posY
hp, maxHp, hunger, fatigue
personality: courage, greed, loyalty, ambition, kindness, patience   // 0..100
talent: rootMask, rootQuality, comprehension (ngộ tính), luck (khí vận)
cultivation: realm, subStage, progress, techniqueId, daoHeart (tâm cảnh), lifespan
inventory: vài slot item id + count
factionId, rank, homeSettlementId
goalType, goalTarget
relations: danh sách ngắn (≤16) {otherId, type, value}   // thù, ân, sư đồ, phu thê, huyết thống
memoryRefs: id các sự kiện quan trọng trong HistoryLog (≤8)
```

### 3.3 AI: Utility AI theo mục tiêu

Mỗi lần "suy nghĩ" (không mỗi tick, chỉ khi xong mục tiêu hoặc mỗi tháng):
- Chấm điểm các mục tiêu: *Sống sót, Ăn, Tu luyện, Tìm tài nguyên, Đột phá, Báo thù, Bảo vệ tông môn, Tìm bạn đời, Khám phá, Mở rộng thế lực…*
- `score = nhu cầu × tính cách × cơ hội trong tầm nhìn`. Ví dụ tham vọng cao + gần đại hạn tuổi thọ → mục tiêu "tìm cơ duyên đột phá" vọt lên, kể cả mạo hiểm vào cấm địa.
- Hành động cụ thể là state machine nhỏ: Move → Do → Done. Pathfinding: A* trên grid thô (chunk 8×8) + flow field cho đám đông.

**Đây là nguồn emergent story chính:** tuổi thọ hữu hạn + linh khí khan hiếm + tính cách khác nhau → ép NPC hành động mạo hiểm.

### 3.4 Các loài

| Loài | Đặc điểm |
|---|---|
| Phàm nhân | sinh sản nhanh, tổ chức xã hội, có thể có linh căn |
| Tu sĩ | phàm nhân có linh căn + đã nhập môn |
| Động vật thường | quần thể, chuỗi thức ăn |
| Yêu thú | động vật hấp thụ linh khí đủ lâu → khai linh trí, có cảnh giới riêng (Nhất giai → Cửu giai), hóa hình ở cấp cao |
| Linh thú | yêu thú hiền/được thuần hóa, có thể khế ước với tu sĩ |
| Ma vật | sinh ra trong vùng ma khí/tử địa, ăn sinh khí, lan rộng nếu không bị diệt |
| Đặc biệt | thiên địa linh vật, quỷ hồn cường giả, khôi lỗi cổ… sinh từ sự kiện |

### 3.5 Đã cài (M2)

**Làng (quần thể):** 17 nhóm tuổi (5 năm một nhóm), kho lương, nhà (mỗi nhà 6 người), ruộng (loại địa hình `Farmland`, có chủ sở hữu theo ô).
- Mỗi tháng: thu hoạch = tổng độ màu mỡ của số ruộng người làm việc chăm được (mỗi lao động 2,5 ô) × 1,4 × hệ số mùa (Xuân 0,6 · Hạ 1 · Thu 1,5 · Đông 0,2), cộng thịt săn được quanh làng, trừ mỗi người 1 phần. Thiếu ăn thì người già và trẻ nhỏ chết trước. Thiếu ruộng thì mở thêm (chặt cây), thừa ruộng thì bỏ hoang.
- Mỗi năm: già đi, chết theo tỉ lệ từng nhóm tuổi, sinh con (phụ thuộc lương thực và độ chật chội), xây nhà khi đông, bỏ nhà khi vắng. Làng đủ 70 người mà chật hoặc thiếu ăn thì tách một đoàn di dân (18–30% dân số) đi lập làng mới cách 30–110 ô; đến nơi không còn chỗ thì nhập vào làng gần nhất.
- Tên gọi: tông môn dùng tên trong truyện; địa danh có hậu tố Thôn (dưới 150 người) / Trấn (150–400) / Thành (trên 400).

**Động vật thường (quần thể theo vùng):** hươu, thỏ, sói là con số trên từng vùng 64×64 ô (16×16 vùng), không phải cá thể. Mỗi tháng:
- Thú ăn cỏ trên lưới cỏ 8×8 ô. Hươu ăn lá rừng, thỏ gặm cỏ đồng; phần thức ăn của mỗi loài chia theo độ phù hợp môi trường của vùng, nên hai loài chỉ cạnh tranh một phần.
- Thiếu cỏ thì chết đói. Sinh sản vào Xuân và Hạ.
- Sói săn theo mô hình bão hòa: mồi càng nhiều thì mỗi con sói săn được càng nhiều, nhưng có giới hạn trên.
- 4% mỗi quần thể lan sang vùng bên cạnh. Làng săn một ít.
- Kết quả: cả 3 loài cùng tồn tại; thỏ và sói dao động theo chu kỳ săn mồi.

**Nguyên tắc hiển thị:** số lượng mô phỏng có thể lớn (hàng chục nghìn con), nhưng map chỉ vẽ vài con tượng trưng. Mỗi vùng trong khung nhìn có tối đa 3 hươu, 3 thỏ, 2 sói, mỗi làng tối đa 20 dân. Các con này chỉ để trang trí và đi lang thang. Chỉ những thứ đặc biệt (đoàn di dân; sau này là tu sĩ, yêu thú khai linh trí) mới là cá thể thật trong simulation.

**Hiển thị:** cá thể thật và con đại diện vẽ chung bằng 1 mesh động (1 draw call), có nội suy giữa các tick.

**Hiệu năng hiện tại:** khoảng 0,05 ms/tick trong Editor.

---

## 4. Tu tiên

### 4.1 Cảnh giới (tham khảo Phàm Nhân Tu Tiên — số liệu là giá trị cân bằng game)

| Cấp | Cảnh giới | Tiểu cảnh | Thọ nguyên (năm) | Điều kiện đột phá lên cấp này |
|---|---|---|---|---|
| 0 | Phàm nhân | – | 60–80 | có linh căn + công pháp nhập môn |
| 1 | Luyện Khí | tầng 1–13 | 120 | – |
| 2 | Trúc Cơ | Sơ/Trung/Hậu/Viên mãn | 220 | Luyện Khí 13 + (Trúc Cơ Đan tăng mạnh tỷ lệ) |
| 3 | Kết Đan | Sơ/Trung/Hậu/Viên mãn | 500 | Trúc Cơ viên mãn + Kết Kim Đan (linh vật/đan phụ trợ) |
| 4 | Nguyên Anh | Sơ/Trung/Hậu/Viên mãn | 1.000 | Kết Đan viên mãn + vượt **thiên kiếp nhỏ**, tâm cảnh đủ |
| 5 | Hóa Thần | Sơ/Trung/Hậu/Viên mãn | 2.000 | Nguyên Anh viên mãn + lĩnh ngộ pháp tắc + linh khí vùng rất cao |
| 6 | Luyện Hư | Sơ/Trung/Hậu/Viên mãn | 3.500 | thiên kiếp |
| 7 | Hợp Thể | Sơ/Trung/Hậu/Viên mãn | 6.000 | thiên kiếp |
| 8 | Đại Thừa | Sơ/Trung/Hậu/Viên mãn | 10.000 | đại thiên kiếp |
| 9 | Phi thăng | – | – | Độ Kiếp thành công → rời thế giới, để lại truyền thừa |

- Bản đầu (MVP) chỉ cần tới **Nguyên Anh**; từ Hóa Thần trở lên có thể để thế giới "nhân giới" có trần linh khí thấp khiến rất hiếm người đạt tới, giống truyện.
- **Đã cài (M3), thứ bậc theo truyện:**
  - Luyện Khí là đệ tử ngoại môn, Trúc Cơ là đệ tử nội môn, Kết Đan là trưởng lão hoặc tông chủ.
  - Nguyên Anh là bá chủ một phương. Hóa Thần là truyền thuyết: tỉ lệ đột phá 0,3% và cần linh khí từ 8.000.
  - Tỉ lệ đột phá cơ bản: Trúc Cơ 10% (+25% nếu có Trúc Cơ Đan), Kết Đan 4%, Nguyên Anh 1,5% kèm thiên kiếp.
  - Chi tiết và số liệu kiểm chứng: `Docs/Devlog/04-m3-tu-tien.md`.
- Mỗi cấp tăng sức mạnh theo cấp số (vd ×4–×6). Chênh 1 đại cảnh giới gần như không thể thắng, trừ pháp bảo/thần thông/hội đồng.

### 4.2 Linh căn

- 5 hệ cơ bản Kim/Mộc/Thủy/Hỏa/Thổ + biến dị Lôi/Phong/Băng. Lưu bằng bitmask.
- **Càng ít hệ càng quý:** Thiên linh căn (1 hệ) ×5 tốc độ, Chân linh căn (2) ×2, Ngụy linh căn (4–5) ×0.3.
- Linh căn hợp hệ với linh mạch/công pháp → thưởng tốc độ. Hàn Lập (ngụy linh căn) mạnh nhờ khí vận + bình (kỳ ngộ) → hệ thống phải cho phép kẻ thiên phú thấp vươn lên qua **kỳ ngộ**, không chỉ thiên phú.

### 4.3 Tốc độ tu luyện (mỗi tháng)

```
gain = baseRate[realm]
     × rootMultiplier × elementMatch(technique, root, localQiElement)
     × techniqueGrade
     × (localQi / qiRequired[realm])   // clamp 0..2
     × (1 + pillBonus) × daoHeartFactor
localQi -= gain × absorbFactor         // hút linh khí tại chỗ
```

### 4.4 Bình cảnh, đột phá, tẩu hỏa nhập ma

- Đạt viên mãn → **bình cảnh**: progress dừng, cần quyết định đột phá.
- `chance = base[realm] + pill + comprehension + daoHeart + tài nguyên phụ trợ − tuổi già`
- Thất bại: tổn thọ / tụt tiểu cảnh / tâm cảnh giảm. Thất bại nặng hoặc tâm cảnh thấp → **tẩu hỏa nhập ma**: chết, phế, hoặc thành **ma tu** (đổi phe, tính cách bạo lực, có thể tạo ma vật).
- **Tâm cảnh (daoHeart):** tăng khi bế quan, thắng tâm ma, sống lâu; giảm khi giết người thân, phản bội, thất bại liên tiếp.

### 4.5 Thiên kiếp & phi thăng

- Thiên kiếp là **sự kiện thế giới**: sét đánh vùng bán kính R quanh người độ kiếp, phá công trình, giết sinh vật yếu, có thể để lại vùng "lôi địa" (zoneFlag).
- Kết quả = sức mạnh + pháp bảo phòng ngự + trận pháp + vận khí. Người khác có thể **lợi dụng** (đánh lén lúc độ kiếp) → câu chuyện.
- Phi thăng: entity rời thế giới → để lại **Truyền thừa** (động phủ chứa công pháp + pháp bảo) tại nơi bế quan → hậu nhân phát hiện hàng nghìn năm sau.
- **Đã cài (M6):**
  - Thiên kiếp tự nhiên và thiên kiếp do Thiên Đạo giáng dùng chung một cơ chế: pháp bảo, hộ sơn đại trận, tâm cảnh và khí vận đều giúp sống sót.
  - Sét đánh cả vùng quanh người độ kiếp: cây cháy; nhà sập và phàm nhân chết nếu không có đại trận che chắn; tu sĩ yếu đứng gần có thể chết.
  - Nơi độ kiếp thành **lôi địa** có tên riêng, tồn tại 200–600 năm: trần linh khí +25%, phàm nhân tránh xa. Tu sĩ đến lập động phủ thì tiểu sử ghi rõ lôi địa đó do ai để lại.
  - Chi tiết: `Docs/Devlog/12-m6-thien-kiep-thien-tai.md`.

### 4.6 Công pháp, pháp thuật, thần thông, pháp bảo, đan dược

| Hệ | Thiết kế |
|---|---|
| **Công pháp** | ScriptableObject: hệ ngũ hành, phẩm cấp (Hoàng/Huyền/Địa/Thiên), cảnh giới tối đa, hệ số tốc độ. Lưu trong thư viện tông môn; tông môn mất công pháp cao cấp → suy tàn. Có thể **thất truyền** hoặc sinh mới (cường giả sáng tạo công pháp khi ngộ tính cao). |
| **Pháp thuật/võ kỹ** | Trừu tượng hóa thành chỉ số tấn công/phòng thủ/tầm xa + tag hệ. Không cần mô phỏng từng chiêu. |
| **Thần thông** | trait đặc biệt hiếm (bất tử thân, độn thuật, thiên nhãn…) — nhận từ truyền thừa, huyết mạch, kỳ ngộ. |
| **Pháp bảo** | item có phẩm cấp, chủ nhân, lịch sử (ai đã dùng). Pháp bảo nổi tiếng có tên riêng và được ghi trong lịch sử. |
| **Đan dược** | luyện bởi luyện đan sư từ linh thảo + yêu đan. Trúc Cơ Đan là "nút thắt" kinh tế lớn của tầng thấp → tranh đoạt. |

---

## 5. Tài nguyên

| Tài nguyên | Nguồn | Tái sinh? | Dùng cho |
|---|---|---|---|
| Thức ăn | nông nghiệp, săn bắn, hái lượm | có (theo fertility, mùa) | dân số |
| Nước | sông, hồ, giếng | có | định cư |
| Gỗ / Đá / Kim loại | rừng / núi / mỏ | gỗ có, đá-kim loại không | xây dựng, công cụ, vũ khí |
| Linh thạch | mỏ trên linh mạch | không (cạn, làm suy linh mạch) | tiền tệ tu tiên, trận pháp, tu luyện |
| Linh thảo | vùng linh khí cao | có, chậm (năm–trăm năm) | đan dược |
| Khoáng đặc biệt | hiếm, sâu trong núi/bí cảnh | không | luyện khí (pháp bảo) |
| Yêu đan | giết yêu thú | theo quần thể yêu thú | đan dược cao cấp, pháp bảo |
| Bí bảo | bí cảnh, mộ cổ | không | kỳ ngộ |

Khan hiếm là thiết kế có chủ đích: **tài nguyên quý luôn ít hơn nhu cầu** → xung đột.

---

## 6. Xã hội & thế lực

### 6.1 Một mô hình chung cho mọi tổ chức

Gia đình, bộ lạc, làng, thành, quốc gia, gia tộc tu tiên, tông môn, liên minh đều là **`Faction`** với `type` khác nhau → dùng chung code ngoại giao, chiến tranh, lịch sử.

```
Faction: id, type, name, leaderId, parentId (chư hầu/phân tông), memberIds | population
         treasury (tài nguyên), territory (danh sách cell/vùng), capitalSettlementId
         laws (bitmask: cấm ma tu, chế độ thế tập, thuế…), culture (giá trị: võ/văn/đạo/ma, ưa ngũ hành)
         goals, relations{factionId → opinion, treaty}, foundedTick, historyIds
```

### 6.2 Vòng đời tổ chức (rule-based)

- **Hình thành:** gia đình đủ lớn → bộ lạc; bộ lạc định cư gần nước + thức ăn → làng; làng đủ dân + có tường → thành; nhiều thành dưới một lãnh đạo → quốc gia.
- **Tông môn:** một tu sĩ đủ mạnh (≥ Trúc Cơ) + tham vọng + vùng linh khí cao chưa có chủ → lập tông môn.
- **Chia tách:** lãnh đạo chết không người kế vị rõ, nội bộ nhiều phe, lãnh thổ quá rộng, đệ tử mạnh hơn sư phụ + tham vọng cao → ly khai.
- **Hợp nhất:** bị đe dọa chung → liên minh; thua trận → sáp nhập/chư hầu.
- **Diệt vong:** hết thành viên, mất lãnh thổ, bị diệt môn.

### 6.3 Ngoại giao

Opinion giữa hai thế lực thay đổi theo: biên giới tiếp giáp, tranh cùng tài nguyên, văn hóa khác nhau, ân oán lịch sử (giết người của nhau), thương mại. Ngưỡng opinion → hiệp ước / thù địch / chiến tranh.

---

## 7. Kinh tế (nhu cầu → sản xuất → trao đổi)

- **Mỗi settlement có kho + chợ.** Mỗi tháng: tính cung (sản xuất của dân & nghề) và cầu (tiêu thụ + nhu cầu xây dựng + nhu cầu tu luyện của tu sĩ cư trú).
- **Giá động:** `price = basePrice × (cầu / max(cung,ε))^k` clamp. Không có giá cố định → giá Trúc Cơ Đan tăng vọt ở vùng hiếm linh thảo.
- **Thương nhân** (entity hoặc caravan trừu tượng) chọn tuyến có chênh lệch giá lớn nhất → tự hình thành **con đường giao thương**; tuyến được dùng nhiều thành "đường" trên map.
- **Thương hội** = Faction type đặc biệt, sở hữu caravan, có thể thuê tu sĩ hộ tống.
- **Tiền tệ:** phàm nhân dùng bạc/vàng; tu sĩ dùng **linh thạch**. Hai nền kinh tế quy đổi qua chợ, tỷ giá động.
- **Nghề nghiệp:** nông dân, thợ săn, thợ mỏ, thợ xây, thợ rèn (phàm), luyện đan sư, luyện khí sư, trận pháp sư, phù sư (tu sĩ). Nghề chọn theo nhu cầu chợ + thiên phú.

---

## 8. Tông môn

- **Cấu trúc:** Chưởng môn → Thái thượng trưởng lão → Trưởng lão → Nội môn → Ngoại môn → Tạp dịch. Rank theo cảnh giới + cống hiến.
- **Hành vi theo năm:** tuyển đệ tử (thu nhận trẻ có linh căn từ các làng xung quanh — tạo quan hệ với phàm nhân), phân phát tài nguyên theo rank, xây kiến trúc (sơn môn, luyện đan phòng, tàng kinh các, hộ sơn đại trận), chiếm linh mạch, mở khoáng, cử đệ tử vào bí cảnh.
- **Chỉ số:** sức mạnh (tổng sức chiến), uy danh, tài sản, thư viện công pháp, khí vận.
- **Nội bộ:** phe phái theo trưởng lão; tranh chức chưởng môn khi chưởng môn sắp hết thọ; đệ tử bị đối xử bất công + loyalty thấp → phản bội, mang công pháp đi lập tông mới.
- **Cấp bậc tông môn:** Tiểu → Trung → Đại → Thánh địa (dựa trên cường giả cao nhất + lãnh thổ + uy danh).

---

## 9. Chiến tranh & xung đột

Hai tầng giải quyết, theo LOD:

| Quy mô | Cách giải |
|---|---|
| Cá nhân / nhóm nhỏ | mô phỏng trên map: entity di chuyển, chọn mục tiêu, so chỉ số (realm, pháp bảo, thần thông, HP) + RNG |
| Quân đội / tông môn đại chiến | **trận đánh trừu tượng** tại một vùng: lực lượng = tổng sức chiến × địa hình × lãnh đạo × hậu cần (lương thực, linh thạch) × trận pháp. Kết quả trả về thương vong, cường giả tử trận (tạo sự kiện), lãnh thổ đổi chủ. Vẫn hiển thị vài unit đánh nhau tượng trưng trên map |

- **Casus belli tự sinh:** tranh linh mạch, báo thù (relation thù hận của lãnh đạo), đói kém, mở rộng lãnh thổ, tông môn chính – ma đối lập.
- **Hậu quả:** dân số giảm, nghề thất truyền, cell bị đốt (vegetation = 0), có thể thành **cổ chiến trường** (zoneFlag + oán khí → ma vật, nhưng cũng chôn pháp bảo → bí cảnh tương lai).
- **Diệt môn:** thư viện công pháp bị cướp hoặc thất truyền; người sống sót có thể ẩn danh và báo thù về sau (mục tiêu "Phục hưng tông môn").

---

## 10. Sinh thái & yêu thú

- Quần thể động vật theo chunk: cỏ → động vật ăn cỏ → thú săn mồi (Lotka–Volterra đơn giản, cập nhật theo mùa).
- **Khai linh trí:** động vật sống lâu trong vùng linh khí cao có xác suất nhỏ thành yêu thú (được promote thành entity).
- **Yêu thú** có cảnh giới riêng (Nhất → Cửu giai, tương ứng Luyện Khí → Hóa Thần), chiếm **lãnh địa**, ăn động vật/người, sinh con. Yêu thú bá chủ thành "Yêu Vương" → các yêu thú khác trong vùng thần phục → **yêu tộc** (Faction type yêu tộc).
- **Vòng đời một vùng:** Hoang dã → sinh thái phát triển → yêu thú xuất hiện → yêu thú thống trị → tu sĩ khai phá (săn yêu đan) → tranh tài nguyên. Tu sĩ săn quá mức → yêu thú suy → hệ sinh thái mất cân bằng.
- **Thú triều:** yêu thú quá đông + thiếu thức ăn → tràn vào khu người.

---

## 11. Bí cảnh & kỳ ngộ

- **Sinh tự nhiên:** linh mạch cấp cao + thời gian → động thiên / linh tuyền / thiên tài địa bảo.
- **Sinh từ lịch sử (quan trọng nhất):** cường giả chết / phi thăng → động phủ, mộ; đại chiến → cổ chiến trường; văn minh diệt vong → di tích. Đây là cách *lịch sử quá khứ trở thành gameplay hiện tại*.
- **Ẩn & phát hiện:** bí cảnh có `discoveryDifficulty`. Mỗi entity đi qua vùng lân cận roll theo luck + comprehension. Phát hiện → sự kiện, tin tức lan (tông môn gần biết sau vài tháng) → tranh đoạt.
- **Vào bí cảnh:** mô phỏng trừu tượng (không có map riêng ở MVP): roll nguy hiểm/phần thưởng theo realm người vào. Phần thưởng: công pháp, pháp bảo, đan, truyền thừa (thần thông của chủ cũ).
- Nội dung bí cảnh lấy từ **tài sản thật của người đã chết** (pháp bảo có tên, công pháp thất truyền) → nhân quả xuyên thời đại.

---

## 12. Nhân quả & lịch sử

### 12.1 HistoryLog

```
HistoryEvent: id, tick, type, actors[] (entity/faction ids), location, importance, payload
```
- Mọi hệ phát sự kiện qua EventBus; HistoryLog chỉ lưu sự kiện có `importance ≥ ngưỡng` (sinh/tử của người thường không lưu chi tiết, chỉ thống kê).
- Ví dụ type: Born, Died, Killed, Breakthrough, TribulationPassed/Failed, Ascended, FactionFounded/Destroyed/Split/Merged, WarDeclared/Ended, BattleFought, RealmDiscovered, TreasureObtained, Betrayal, Disaster, EraChanged.
- Nén theo thời gian: sau mỗi thời đại, sự kiện nhỏ được gộp thành "biên niên" tóm tắt.

### 12.2 Nhân vật lịch sử

Entity được đánh dấu **Huyền thoại** khi: tổng importance các sự kiện liên quan vượt ngưỡng (giết cường giả, lập tông môn lớn, phi thăng…). Có trang tiểu sử tự sinh từ chuỗi sự kiện của họ, có danh hiệu (vd "Huyết Kiếm Ma Tôn" — ghép từ tag hành vi).

### 12.3 StoryDetector (biến dữ liệu thành câu chuyện)

Các "pattern" chạy trên HistoryLog để phát hiện và **thông báo** cho người chơi:
- *Phế vật nghịch thiên:* ngụy linh căn + nhận truyền thừa + đạt ≥ Kết Đan.
- *Báo thù:* A giết người thân của B → nhiều năm sau B giết A.
- *Đệ tử phản sư:* B rời tông môn của sư phụ A → lập tông môn → đánh tông môn cũ.
- *Tiểu tông thành bá chủ:* tông môn hạng Tiểu → Đại trong ≤ N năm.
- *Hậu nhân phát hiện di tích tổ tiên:* entity phát hiện bí cảnh do tổ tiên trong huyết thống tạo ra.
Người chơi có thể "follow" một nhân vật hoặc thế lực để nhận tin.

---

## 13. Thời đại (Eras)

Era không được script — **được nhận diện** từ chỉ số thế giới mỗi trăm năm:

| Thời đại | Điều kiện nhận diện (ví dụ) |
|---|---|
| Nguyên thủy | chưa có làng, chưa có tu sĩ |
| Văn minh hình thành | ≥ N làng/thành |
| Tu tiên xuất hiện | tu sĩ đầu tiên |
| Tông môn phát triển | ≥ M tông môn |
| Cường giả xuất hiện | có Nguyên Anh+ |
| Đại chiến | tổng thương vong chiến tranh/năm vượt ngưỡng |
| Suy tàn | linh khí trung bình giảm mạnh, dân số giảm |
| Đại kiếp | sự kiện cấp thế giới (người chơi hoặc tự nhiên) |
| Thời đại mới | phục hồi sau đại kiếp |

**Đại kiếp tự nhiên:** linh khí thế giới có chu kỳ dài (vd 10.000 năm) — thời kỳ "mạt pháp" giảm qiCap toàn cầu, ép văn minh tu tiên suy tàn rồi hồi sinh. Đây là động cơ để thế giới qua nhiều thời đại thay vì đứng yên.

---

## 14. Thiên Đạo — quyền năng người chơi

Tổ chức theo tab như WorldBox (thanh công cụ dưới màn hình):

| Tab | Quyền năng |
|---|---|
| **Địa hình** | nâng/hạ đất, núi, biển, sông, sa mạc, rừng, hang; brush kích thước 1–32 |
| **Sinh linh** | thả phàm nhân, động vật, yêu thú, linh thú, ma vật; thả tu sĩ ở cảnh giới chọn |
| **Linh khí** | vẽ linh mạch, tăng/giảm linh khí vùng, tạo phúc địa/cấm địa, tạo bí cảnh, đặt thiên tài địa bảo |
| **Phúc / Họa** | ban linh căn, ban khí vận, ban thần thông/pháp bảo; giáng tâm ma, bệnh dịch, phế tu vi |
| **Thiên tai** | sét, động đất, núi lửa, lũ, hạn, thú triều, thiên kiếp lên một người, đại kiếp toàn cầu |
| **Thời tiết** | mưa, hạn, bão, tuyết, đổi mùa |
| **Sinh/Tử** | hồi sinh (nhân vật lịch sử), tiêu diệt cá thể/thế lực |
| **Quy luật** | slider toàn cầu: linh khí thế giới, tỷ lệ linh căn, tỷ lệ đột phá, độ khắc nghiệt thiên kiếp, bật/tắt ma tu, tốc độ sinh sản |
| **Quan sát** | inspector cá thể/thế lực, bản đồ overlay (linh khí, lãnh thổ, nhiệt độ, dân số, tài nguyên), biên niên sử, cây phả hệ |

Mọi quyền năng → `Command` → áp dụng ở tick kế tiếp → ghi vào HistoryLog như "Thiên ý" (để người chơi thấy hậu quả: "Năm 1203, Thiên Đạo giáng sét… 300 năm sau, nơi đó thành Lôi Cốc cấm địa").

**Đã cài (M6 phần 1):**
- **Tab Thiên Đạo:** thêm Thiên kiếp lên một tu sĩ.
- **Tab Thiên tai:** động đất (đứt linh mạch), núi lửa (dung nham nguội dần, mở mạch địa hỏa), lũ lụt (ngập ruộng rồi rút), hạn hán (mất mùa theo vùng), ôn dịch (lây sang làng lân cận), thú triều.
- Thế giới cũng tự sinh các thiên tai này với tỉ lệ thấp.
- Chi tiết: `Docs/Devlog/12-m6-thien-kiep-thien-tai.md`.

**Đã cài (M6 phần 2):**
- **Tab Quy luật:** 7 luật toàn thế giới, gồm linh khí, linh căn, đột phá, độ khắc nghiệt thiên kiếp, sinh sản, thiên tai và bật/tắt ma đạo.
- **Sinh/Tử:** hồi sinh (người sống lại mang huyết thù với kẻ đã giết mình) và diệt môn.
- **Đại kiếp toàn cầu:** linh khí còn một nửa, thiên tai liên miên trong 8–15 năm.
- **Thời tiết:** mưa (cắt hạn hán), bão, rét.
- Kẻ thù có thể đánh lén người đang độ kiếp.
- Chi tiết: `Docs/Devlog/13-m6-quy-luat-sinh-tu-dai-kiep.md`.

---

## 15. Rendering (theo phong cách WorldBox)

Tham khảo 3 ảnh: địa hình là khối ô vuông (viền bờ biển bậc thang), còn nhà, cây và người là pixel art chi tiết hơn, phủ nhiều ô. Tông màu tươi, outline tối.

**Mật độ pixel:**
- 1 cell = 1 texel của texture địa hình, hiển thị phóng to thành khối vuông.
- Sprite vật thể vẽ theo **8 art-pixel / cell** (PPU = 8 nếu 1 cell = 1 world unit). Một nhà 3×3 cell là sprite khoảng 24×28 px, kể cả phần mái nhô lên.
- Camera orthographic, zoom theo bậc nguyên (pixel-perfect) để nét pixel không bị nhòe.

| Lớp | Cách vẽ |
|---|---|
| **Địa hình** | `Texture2D` 1 texel = 1 cell, `FilterMode.Point`, chia chunk 32×32 ô (xem 15.1) để chỉ upload phần dirty. Màu cell tính từ terrain/biome/độ cao + biến thiên màu nhẹ theo hash vị trí (tránh mảng màu phẳng, như cỏ ở ảnh 2) + viền cát/nước nông. Tùy chọn sau: shader vẽ mỗi cell bằng tile 8×8 từ atlas để địa hình cũng có chi tiết. |
| **Nước** | shader trên quad địa hình: animate màu theo độ sâu (từ `height`), viền sóng ở bờ. |
| **Overlay** | texture thứ hai cùng kích thước (linh khí, lãnh thổ màu thế lực, nhiệt…), blend trong shader, bật/tắt tức thì. |
| **Cây, nhà, công trình** | sprite atlas, neo ở đáy footprint, vẽ bằng `Graphics.RenderMeshInstanced` (1 lệnh cho hàng nghìn sprite), sort theo y để mái/tán che đúng thứ tự. Chỉ vẽ trong camera. Trạng thái (cháy, đổ nát, đang xây) = đổi frame trong atlas. |
| **Sinh vật** | sprite instancing, animation 2–4 frame. Khi zoom xa (LOD) → bỏ sprite, vẽ chấm màu vào overlay texture để vẫn thấy "đám đông". |
| **Hiệu ứng** | lửa, sét thiên kiếp, ánh sáng đột phá: particle pool, chỉ khi trong tầm nhìn. |
| **Nhãn** | tên cường giả/tông môn hiển thị khi zoom gần (như "Selon 127" trong ảnh 2). |

### 15.1 Chunk & vẽ lại (đã chốt)

- Map cố định **1024×1024 ô**, chia **chunk 32×32 ô** (32×32 = 1024 chunk). Mỗi chunk là một `Texture2D` 256×256 riêng (8 px/ô) + 1 quad. Không dùng `Texture2DArray`, vì `Apply()` trên mảng sẽ upload lại toàn bộ.
- **Đóng dấu vào texture chunk:** địa hình, đường, tường, nhà, cây, đá, công trình, dấu vết chiến trường. **Sprite instancing:** sinh vật, lửa, sét, hiệu ứng, ô chọn, nhãn tên.
- **Dirty:** thay đổi ô hoặc vật thể → đánh dấu mọi chunk mà *khung hình vẽ* chạm tới (kể cả phần mái/tán tràn sang chunk bên cạnh). Gom trong frame, mỗi chunk vẽ lại tối đa 1 lần.
- **Vẽ lại một chunk:** vẽ địa hình → vẽ mọi vật thể có hình chồng lên chunk (kể cả vật thể gốc ở chunk hàng xóm), sắp theo y → `SetPixelData` + `Apply(false)`.
- **Ngân sách:** tối đa khoảng 8–16 chunk/frame, ưu tiên chunk trong camera. Chunk ngoài màn hình giữ dirty tới khi camera tới.
- **Zoom xa:** chuyển sang texture tổng 1024×1024 (1 texel/ô, màu đại diện), không cần mipmap, cập nhật bằng cách ghi 1 texel.

---

## 16. Hiệu năng & mục tiêu kỹ thuật

| Hạng mục | Mục tiêu |
|---|---|
| Nền tảng | **PC** (Windows trước) |
| Map | 1024² mặc định, 2048² tùy chọn |
| Entity cá thể | 20k |
| Vật thể vẽ (nhà, cây) | 100k+ trên map, chỉ vẽ phần trong camera |
| Tốc độ | x20 vẫn ≥ 30 FPS; "Tua thời đại" ≥ 100 năm/giây không render |
| Lưu game | serialize WorldData + EntityStore + Factions + HistoryLog (nén) |

- MVP: C# thuần + struct arrays là đủ. Khi profile thấy nóng → chuyển hệ nặng (Qi diffusion, ecology, movement) sang **Burst Jobs**. Không bắt đầu bằng DOTS/ECS đầy đủ để tránh over-engineer.

---

## 17. Lộ trình (Roadmap)

| Mốc | Nội dung | Kết quả kiểm chứng |
|---|---|---|
| **M0 — Thế giới tĩnh** ✅ | MapGen từ seed, terrain texture, camera pan/zoom, brush địa hình, overlay linh khí | vẽ & sửa map 1024² mượt, đặt thử vài nhà/cây nhiều cell |
| **M1 — Thời gian & linh khí** ✅ | Clock, tick, mùa, linh mạch, khuếch tán linh khí, Command queue | linh khí lan, cạn, hồi |
| **M2 — Sinh mệnh cơ bản** ✅ | EntityStore, phàm nhân + động vật, ăn/di chuyển/sinh/chết, làng dạng quần thể, sprite instancing | dân số tự tăng/giảm theo tài nguyên |
| **M3 — Tu tiên lõi** ✅ | linh căn, Luyện Khí → Nguyên Anh, tu luyện hút linh khí, đột phá, tẩu hỏa, thọ nguyên | tu sĩ tự tìm phúc địa, có người đột phá/chết già |
| **M4 — Thế lực** ✅ | Faction chung, làng → thành, tông môn lập/tuyển đệ tử, ngoại giao opinion | tông môn tự sinh & tranh linh mạch |
| **M5 — Xung đột & lịch sử** ✅ | chiến đấu cá nhân, trận trừu tượng, HistoryLog, biên niên sử UI, StoryDetector v1 | đọc được "câu chuyện" sau 1.000 năm |
| **M6 — Thiên Đạo** ✅ | đủ các tab quyền năng, thiên kiếp, thiên tai | vòng CREATE → INTERVENE → CONSEQUENCE hoàn chỉnh |
| **M7 — Chiều sâu** ✅ | kinh tế/chợ/thương lộ, yêu thú tiến hóa & yêu tộc, bí cảnh từ lịch sử, thời đại & mạt pháp | hai seed khác nhau → lịch sử khác hẳn (chi tiết: `Docs/Devlog/15-m7-chieu-sau-the-gioi.md`) |

**M0–M3 là "vertical slice":** nếu xem tu sĩ tự tu luyện, tranh linh khí và chết già đã thấy thú vị, thiết kế đúng hướng.

---

## 18. Rủi ro & quyết định mở

| Rủi ro | Giảm thiểu |
|---|---|
| Phạm vi quá lớn cho 1 dev | bám roadmap, mỗi mốc phải chơi được; cắt tính năng M7 nếu cần |
| Simulation chạy nhưng không "kể chuyện" được | đầu tư sớm vào HistoryLog + StoryDetector + thông báo, vì người chơi chỉ thấy cái được hiển thị |
| Thế giới hội tụ về trạng thái chết (một tông môn thắng tất cả, hoặc diệt chủng) | lực cân bằng: thọ nguyên, nội loạn khi lớn, mạt pháp, đại kiếp, chi phí quản lý lãnh thổ rộng |
| Hiệu năng khi tua nhanh | LOD simulation, staggered update, Burst cho hệ nóng |

**Đã chốt:**
- Nền tảng: **PC**.
- Project: `My project (1)` (Unity 6000.3.9f1, URP 2D), code trong `Assets/ThienDao`.
- Đồ họa: địa hình = texture theo cell; vật thể = pixel art phủ nhiều cell, 8 art-px/cell.

**Cần chốt:**
1. Tên game chính thức.
2. Nguồn art: tự vẽ, mua asset, hay dùng placeholder sinh bằng code cho prototype.
