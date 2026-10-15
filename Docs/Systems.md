# Kiểm kê hệ thống mô phỏng

**Ngày:** 2026-10-15 · **Phạm vi:** `Assets/ThienDao` sau devlog 39 · **Task:** Phase 0′ / 0.1

**Mục đích:** biết chính xác mỗi hệ sở hữu dữ liệu gì, đọc gì, ghi gì, chạy lúc nào, test nào bao nó. Tài liệu này mô tả **code đang có**, không phải thiết kế mong muốn. Chưa đổi dòng code nào.

**Cách đọc:** "Test" là tên test trong `Tests/SimulationTests.cs`. "Không có" nghĩa là không có test nào trỏ thẳng vào hệ đó (có thể vẫn được gián tiếp chạy qua test dài).

---

## 1. Thứ tự chạy (đọc từ `Simulation.Step`)

Một tick là một ngày. 360 ngày một năm, 30 ngày một tháng.

| Nhịp | Thứ tự |
|---|---|
| **Mỗi ngày** | Đồng hồ → `Creatures.Tick` (di chuyển) → `Cultivation.ErrandStep` (việc ngoài kia) → `Beasts.DailyStep` (đuổi bắt, chiến tranh yêu tộc, hung thú tàn sát) |
| **Đầu tháng** | `Qi` → `Forage` → `Disasters` → `Wildlife` → `Settlements` → `Cultivation` → `Combat` → `Beasts` → `Relics` → `Factions` |
| **Đầu năm** | `Settlements` → `Cultivation` → `Techniques` → `Factions` (gồm xếp hạng và xây sơn môn) → `Combat` → `Disasters` → `Scars` → `Beasts` (gồm chiến tranh yêu tộc, con non) → `Relics` → `Eras` → `Stories` → `Clans` |

Lệnh của Thiên Đạo (`IWorldCommand`) đi qua hàng đợi, được áp vào đầu một lượt và ghi vào `Log` để phát lại.

---

## 2. Bảng hệ

| Hệ | File | Sở hữu | Đọc | Ghi / phát ra | Test |
|---|---|---|---|---|---|
| **Đồng hồ** | `Core/SimClock` | tick, ngày/tháng/mùa/năm | — | cờ đầu tháng/năm | `CalendarRollsOver` |
| **Thế giới** | `World/WorldData`, `MapGenerator`, `Biomes`, `Regions` | lưới ô: địa hình, độ cao, nhiệt, ẩm, linh khí gốc, trần linh khí, linh mạch, vùng, biome, sẹo, chủ làng; vật thể; tên vùng | seed | được các hệ khác sửa qua `NotifyXxxChanged` | `SameSeedProducesSameWorld`, `DifferentSeedsProduceDifferentWorlds`, `BiomesGiveTheLandItsSpirit` |
| **Linh khí** | `Sim/QiSystem`, `World/QiCap` | linh khí hiện tại (khối 8×8) | trần linh khí, biome, lôi địa, quy luật, đại kiếp, chu kỳ thời đại | khuếch tán hằng tháng; tu sĩ và yêu thú hút | `DrainedQiRecovers…`, `InfusedQiSpreads…`, `CultivatorsDrawDownQi…` |
| **Cỏ** | `Sim/ForageSystem` | cỏ theo khối 8×8 | mùa, thiên tai | thú ăn cỏ | gián tiếp qua `LifeContinuesForThirtyYears` |
| **Thú hoang** | `Sim/WildlifeSystem` | hươu, thỏ, sói theo vùng 64×64 (số liệu) | cỏ, mùa | yêu thú ăn bớt, làng săn | `WorldStartsWithVillagesAndAnimals`, `LifeContinues…` |
| **Thực thể cá thể** | `Sim/EntityStore` | vị trí, đích, loài, thời điểm sinh (mảng SoA) | — | — | qua các test hệ khác |
| **Di chuyển** | `Sim/CreatureSystem` | không | thực thể, đích, địa hình | vị trí | `WalkersGoRoundWater…`, `HoaThanTearsTheVoid…` |
| **Tìm đường** | `Sim/NavSystem` | lưới nút 4×4, bộ nhớ đệm tuyến | địa hình | hướng đi | gián tiếp |
| **Dân làng và tông môn (nền)** | `Sim/SettlementSystem` | làng và tông môn dạng quần thể theo nhóm tuổi, nhà, ruộng, công trình, nhóm di dân | lương thực, thiên tai, quy luật | sinh, chết, di cư, lập bộ lạc, thức tỉnh linh căn | `LifeContinues…`, `VillageShrinks…`, `VillageUnderARaisedMountain…`, `MenComeOfThemselves…` |
| **Tu sĩ** | `Sim/CultivationSystem`, `Errands` | danh sách tu sĩ: cảnh giới, linh căn, tiến độ, thọ nguyên, nhà, việc ngoài kia, nhật ký | linh khí, biome, công pháp | đột phá, tẩu hỏa, thiên kiếp, chết, cơ duyên trên đầu | `CultivatorsBreakThrough…`, `SectsStartWithCultivators`, `GrantRootActsOn…`, `DivineTribulation…`, `RevivedCultivator…`, `EnemiesStrikeDuringTribulation`, `CultivatorsGoOutOnErrands…` |
| **Công pháp** | `Sim/TechniqueSystem` | danh sách công pháp, trấn phái công pháp | tu sĩ, tông môn | tốc độ tu luyện, trần cảnh giới, truyền, đoạt, thất truyền | `MethodsCapTheClimb…` |
| **Đấu pháp** | `Sim/CombatSystem` | trận nhỏ, thù oán | tu sĩ, yêu thú | thương tích, cái chết, thù | `HigherRealmUsuallyWinsADuel`, `KillingAMasterSwornRevenge…` |
| **Thế lực** | `Sim/FactionSystem` | tông môn: lãnh thổ, linh thạch, quan hệ, trận chiến | tu sĩ, linh mạch | chiến tranh, lập phái, tách phái, diệt môn | `SectsStartAsFactionsHoldingLand`, `NewSectsAreFounded…`, `WarIsFoughtOverLand…`, `DestroyedSectLeavesNoDangling…`, `AnnihilatedSect…` |
| **Sơn môn** | `Sim/SectComplex` (một phần `FactionSystem`) | hạng, công trình đã xây, bỏ hoang, phế tích | thực lực, số đệ tử, linh thạch, đất | sự kiện xây, bỏ hoang | `SectsBuildTheirSonMonAndLeaveRuins` |
| **Gia tộc** | `Sim/ClanSystem` | gia tộc, thế thù | tu sĩ Kết Đan | thù báo | `ClansRiseOnAncestralLand…` |
| **Yêu thú** | `Sim/BeastSystem`, `BeastChase`, `BeastHorde`, `BeastWars` | yêu thú, chân linh, yêu tộc, hung thú, chiến tranh yêu tộc | linh khí, biome, tu sĩ, làng | đuổi bắt, tàn sát, tiến giai, đánh nhau, cổ chiến trường | `BeastsAwakenAndGrow`, `CultivatorSlaysBeast…`, `YeuVuongGathersAClan`, `HungThuRavages…`, `HungThuTearsThrough…`, `BeastsChase…`, `YeuTocGoToWar…`, `PrimordialWorldOpens…`, `AFallenChanLinh…` |
| **Bí cảnh** | `Sim/RelicSystem` | bí cảnh, cuộc tranh đoạt | cái chết, tông diệt, chân linh chết | khám phá, tranh đoạt, thiên cơ hiển lộ | `DeadExpertsLeaveCaves…` |
| **Thiên tai** | `Sim/DisasterSystem` | thiên tai, thời tiết, dấu tích (lôi địa, núi lửa, chiến trường) | quy luật, thế giới | thiệt hại, sẹo, hóa ma vực | `CalamitiesScar…`, `WorldBringsCalamitiesOnItself`, `Eruption…`, `Flood…`, `Drought…`, `Plague…`, `Earthquake…`, `BeastTide…`, `GreatCalamity…`, `Rain…`, `Storm…` |
| **Vết sẹo** | `Sim/ScarSystem` | `WorldData.Scar` | thiên tai | độ màu mỡ, oán khí chiến trường | gián tiếp |
| **Sát thương trời** | `Sim/HarmSystem` | không | tu sĩ, thú, dân | thương vong | `ThienPhatWoundsEveryCreature…` |
| **Thời đại** | `Sim/EraSystem` | thời đại, chu kỳ linh khí, mốc hồng hoang | gần như mọi hệ (chỉ đọc) | nhân hệ số linh khí | `EachWorldHasItsOwnAges` |
| **Câu chuyện** | `Sim/StoryDetector` | các câu chuyện đã nhận ra | sự kiện | truyền kỳ, danh hiệu | gián tiếp qua `KillingAMaster…` |
| **Sử sách** | `Sim/HistoryLog`, `EventLog` | sự kiện, đếm theo thế kỷ | mọi hệ ghi vào | — | `HistoryRemembersBeyondTheTicker` |
| **Quy luật** | `Sim/WorldRules` | 7 thanh trượt | lệnh người chơi | nhân vào các hệ | `RulesRewriteTheWorld` |
| **Quyền năng** | `Sim/WorldCommands` | hàng đợi và log lệnh | người chơi | sửa mọi hệ | `ThienDaoPhucHoaPowersChangeTheWorld`, `ReplayFromLogMatchesLiveRun`, `SameCommandsProduceSameState` |
| **Lưu / tải** | `Sim/SaveGame` | — | toàn bộ đồ thị | tệp nén có bảng kiểu | `SaveAndLoadRestoresTheWholeWorld`, `LoadedWorldKeepsItsEventWiring` |
| **Bất biến** | `Sim/WorldInvariants` | — | toàn bộ | báo vi phạm | `InvariantsHoldUnderChaos`, `ChaosIsDeterministic` |

---

## 3. Phần trình bày (không giữ sự thật của mô phỏng)

| Phần | Việc | Ghi chú |
|---|---|---|
| `Render/WorldRenderer`, `TerrainTexture` | vẽ chunk, bản đồ thu nhỏ, biome | nghe `WorldData.*Changed` |
| `Render/UnitRenderer` | tu sĩ, yêu thú, dân làng, thú hoang | thú hoang, dân làng trên map là **hình ảnh trang trí**, số thật nằm ở mô phỏng |
| `Render/FxRenderer`, `FightScenes` | hiệu ứng, cảnh đấu | cảnh đấu dựng lại từ sự kiện |
| `Render/SectArt`, `SectRenderer` | sơn môn | chỉ dựng lại khi `Built/Ruined/Tier` đổi |
| `UI/GameUI`, `Icons` | giao diện | chỉ đọc, ra lệnh qua `WorldBrush` |
| `Player/WorldBrush`, `CameraController` | đầu vào | chỉ xếp lệnh vào hàng đợi |

Kiểm tra nguyên tắc "render không giữ sự thật": không thấy chỗ nào trong phần trình bày ghi vào trạng thái mô phỏng. Đây là kết quả đọc code, **không có test tự động**.

---

## 4. Nợ kỹ thuật đã kiểm chứng (chưa sửa)

| # | Vấn đề | Bằng chứng | Hệ quả | Mức |
|---|---|---|---|---|
| D1 | **Công cụ đo dài hạn chạy sai chế độ.** `PerfProbe` và `ChronicleExport` sinh thế giới bằng `Generate(seed)` (chế độ "đã có người"); ván thật dùng `Generate(seed, true)` (hồng hoang) | `Editor/PerfProbe.cs:27`, `ChronicleExport.cs:25` | Mọi số đo dài hạn không phản ánh game như người chơi thấy | Cao |
| D2 | **Test chủ yếu chạy chế độ cũ:** 6 test dùng hồng hoang, 47 test dùng chế độ "đã có người" | `grep` trong `SimulationTests.cs` | Khởi đầu thật của game ít được bảo vệ | Cao |
| D3 | **`ComputeStateHash` không băm `World.Biome` và `BiomePatches`** | `Simulation.ComputeStateHash` không đọc biome | Hai thế giới chỉ khác nhau ở biome vẫn cùng hash; test phát lại không bắt được | Trung bình |
| D4 | **Mã chết từ các hệ đã gỡ:** `Cultivator.Goal/GoalText/GoalUntil/GoalDone/GoalMark` (không đâu gán giá trị khác `None`), `Species.Caravan` cùng sprite và thẻ UI (không còn nơi sinh), `ZoneFlags.Road/Trail` (không ai ghi), cờ đường trong `NavSystem`, `WorldData.PathsChanged`, `PathPixel`/`RoadMap`, `EventKind.Faith`, `EventKind.Destiny`, các trường chính trị trong `Kingdom` (`Ruler`, `Dynasty`, `Stability`…) | `grep` đã chạy | Tăng chi phí đọc, dễ gây hiểu nhầm | Thấp–trung bình |
| D5 | **Chú thích sai thực tế:** nhắc `ProtagonistAI`, `PoliticsSystem`, `TradeSystem`, `FaithSystem`, `PathSystem` ở khoảng 15 chỗ; `ScarSystem` ghi "không hệ nào đọc sẹo" nhưng độ màu mỡ và oán khí đọc nó | `grep` | Hiểu nhầm | Thấp |
| D6 | **Không có test cho phần trình bày** (render, UI, sơn môn, dân bỏ chạy) | — | Hỏng hình ảnh chỉ phát hiện khi chơi | Trung bình |
| D7 | **Một file test duy nhất** (~1.500 dòng) | `Tests/SimulationTests.cs` | Khó tìm, khó chia | Thấp |
| D8 | **Chưa có bằng chứng về bản build** ngoài Editor | không thấy trong repo hay devlog | Không biết hiệu năng và lưu/tải trên bản build | Chưa biết |
| D9 | **`WatchCommand` còn tồn tại** nhưng hết ý nghĩa mô phỏng (chỉ còn ghim, và hệ số ×1,3 của một trạng thái không bao giờ được đặt) | `CultivationSystem.cs:385` | Lệnh vô hại nhưng thừa | Thấp |

**Chưa kiểm:** hiệu năng sau khi gỡ 7 hệ và thêm hồng hoang, biome, sơn môn (số đo gần nhất trong `Docs/Perf_ThienDao.md` đã lỗi thời, và thuộc chế độ cũ theo D1). Theo yêu cầu của bro, t chưa chạy lại; đây là task 0.3.

---

## 5. Số liệu

- **Mã:** khoảng 15.000 dòng trong `Sim` và `World` (chưa tính `Core`, `Render`, `UI`).
- **Test:** 65 (64 chạy hết lần cuối, 1 test hồi quy mới chạy riêng cùng một test cũ).
- **Quyền năng người chơi:** 53 công cụ (`BrushTool`), 10 hành động trực tiếp lên một sinh linh (`DivineAct`), 7 quy luật.
