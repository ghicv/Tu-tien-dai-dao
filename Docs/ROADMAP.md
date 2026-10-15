# Tu Tiên Box — Development Roadmap

> **Trạng thái thực tế (cập nhật 2026-10-15).** Phần dưới đường kẻ là roadmap gốc của bộ pack, giữ nguyên. Bảng này đối chiếu từng phase với code thật. Chi tiết hệ nào nằm ở `Docs/Systems.md`.

| Phase | Nội dung | Trạng thái | Ghi chú |
|---|---|---|---|
| 0 | Nền mô phỏng | **Xong** | đồng hồ, thực thể, hàng đợi lệnh, tạm dừng và tốc độ, lưu/tải, test |
| 1 | Sinh mệnh | **Xong, dạng số liệu** | phàm nhân là quần thể theo nhóm tuổi, không phải cá thể |
| 2 | Linh khí và tu luyện | **Xong** | |
| 3 | Tài nguyên và sinh thái | **Xong** | cỏ, thú hoang, linh dược; gỗ/đá/quặng không định làm |
| 4 | Quyết định của NPC | **Một phần** | tu sĩ chọn việc theo trọng số (việc ngoài kia); chưa có chấm điểm tiện ích tổng quát |
| 5 | Xã hội | **Một phần** | làng, thành, gia tộc có; "nước" chỉ còn là địa danh |
| 6 | Tông môn và truyền thừa | **Xong** | gồm sơn môn |
| 7 | Kinh tế | **Đã bỏ** | gỡ ở devlog 34 |
| 8 | Xung đột | **Xong** | cá nhân, thú tộc, tông môn; khởi nghĩa đã bỏ |
| 9 | Cơ duyên Tiên hiệp | **Xong, trừ phi thăng** | |
| 10 | Lịch sử | **Một phần** | có biên niên và nhật ký; **chưa có liên kết nhân quả** |
| 11 | Quyền năng Thiên Đạo | **Một phần** | nhiều quyền năng; chưa có phản hồi hệ quả, chưa gom nhóm |
| 12 | Trình bày và hoàn thiện | **Một phần** | thế giới pixel đã có; thiếu tutorial, âm thanh, bản build |

## Phase tiếp theo (đã duyệt hướng, chờ duyệt từng task)

### Phase 0′ — Chốt nền (đang làm)
Chỉ tài liệu và một spike, không tính năng mới.

| ID | Task | Trạng thái | Tiêu chí hoàn thành |
|---|---|---|---|
| 0.1 | Kiểm kê hệ thống | **Xong** (`Docs/Systems.md`) | mọi hệ trong `Simulation` có một dòng, nợ kỹ thuật có bằng chứng |
| 0.2 | Hợp nhất tài liệu | **Xong** | `CLAUDE.md`, `DECISIONS.md`, bảng trạng thái này; tài liệu cũ đã đánh dấu; mâu thuẫn chưa quyết nằm trong `DECISIONS.md` |
| 0.3 | Phép đo gốc một lần | **Chờ bro cho phép** | 3 seed, chế độ hồng hoang, bảng số liệu (kể cả sửa D1 trước) |
| 0.4 | Spike nhân quả | Chưa làm | từ một sự kiện cuối lần ngược ra ≥ 3 mắt xích, trên 3 chuỗi |
| 0.5 | Chốt chính sách tên (IP) và tệp lưu | **Chờ bro quyết** | mục trong `DECISIONS.md` |
| 0.6 | Giao thức chơi thử 15 phút | **Xong** (`Docs/Playtest.md`) | người khác làm theo được |

### Phase A — Bản chơi thử 15 phút
Điều kiện vào: 0.1–0.6 xong. Thứ tự đã chốt 2026-10-15.

| ID | Task | Tiêu chí hoàn thành |
|---|---|---|
| A1 | Nhịp thời gian: nút tua tới mốc (có người, có tu sĩ, có tông môn) | từ ván mới tới tông môn đầu tiên trong dưới 60 giây thực; cùng seed ra cùng kết quả |
| A2 | Thiên kiêu bảng, cường giả bảng | bảng tự cập nhật; có dấu trên map; có báo khi người mới lọt bảng |
| A3 | Sợi nhân quả | bấm người hoặc tông, lần ngược về gốc ≥ 3 mắt xích, đúng nguyên nhân |
| A4 | Phản hồi quyền năng | mỗi quyền năng lớn có theo dõi hệ quả và báo lại khi hệ quả xuất hiện |
| A5 | Gom quyền năng thành 4 nhóm: Tạo hóa, Ban phúc, Giáng kiếp, Quy luật | thanh công cụ ≤ 4 nhóm; không còn nút trùng ý |
| A6 | Bro chơi thử theo `Docs/Playtest.md` | bốn bước của tiêu chí đều xảy ra; ghi lại chỗ hỏng |

### Phase B — Chiều sâu (sau khi A đạt)
Sinh linh biết Thiên Đạo (phản ứng khi người chơi ra tay) · thiên kiêu cùng thế hệ đối đầu, cây sư đồ · tài nguyên tông môn để tranh (linh mạch, linh điền, kho tàng) · cường giả xuất thế, thọ nguyên cạn thì liều mạng · biome và sơn môn đổi theo thời đại · thành tựu nhẹ.

### Phase C — Hoàn thiện cho Steam
UI và tutorial · âm thanh · bản build và kiểm hiệu năng trên máy yếu · chính sách tệp lưu · tên và lore hợp lệ để phát hành · demo.

---

## Roadmap gốc của bộ pack (giữ nguyên)

This roadmap describes dependency order, not a promise to implement every feature immediately. Each phase needs explicit acceptance criteria before moving forward.

## Phase 0 — Simulation foundation
Scope:
- World state container.
- Simulation clock/tick.
- Entity ID and lifecycle.
- Basic spatial representation/query.
- Pause and time scaling.
- Minimal save/load only if needed for the first test.

Done when:
- Entities can spawn, update on simulation ticks, and be removed.
- Simulation can pause and run at different speeds.
- Tests demonstrate lifecycle and tick behavior.

## Phase 1 — Life simulation
Scope:
- Human entity.
- Age, health, hunger, food.
- Basic movement and food seeking.
- Aging and death.
- Reproduction only after survival loop is stable.

Done when:
- A small population changes over time without direct player commands.
- Population trends can be observed and debugged.

## Phase 2 — Spiritual world and cultivation
Scope:
- Spiritual energy field/zone.
- Talent and cultivation rate.
- Basic cultivation progress, bottleneck, breakthrough.
- Realm-dependent lifespan.

Done when:
- NPCs exposed to different conditions develop differently.
- Outcomes are explainable from recorded state/rules.

## Phase 3 — Resources and ecology
Scope:
- Basic plants/food regeneration.
- Resource consumption and depletion.
- Simple animals/beasts only if required to test ecology.

Done when:
- Resource scarcity affects survival or behavior.

## Phase 4 — NPC decisions
Scope:
- Needs, goals, feasible actions, simple utility scoring.
- Basic knowledge of nearby locations/resources.
- Limited memory/relationships only when needed.

Done when:
- NPC behavior is not a single scripted sequence and responds to state changes.

## Phase 5 — Society
Scope:
- Family/group/village abstractions.
- Roles and basic leadership.
- Population and resource coordination.

Done when:
- Groups can form and change in response to simulation conditions.

## Phase 6 — Sect and inheritance
Scope:
- Sect membership/leadership/resources.
- Recruitment and technique inheritance.
- Relations and territorial interests.

Done when:
- Sects can form, grow, weaken, split, or disappear through rules.

## Phase 7 — Economy
Scope:
- Production/consumption/trade only where needed by society and sect behavior.

## Phase 8 — Conflict
Scope:
- Combat and conflict driven by threats, resources, relationships, and power.
- Consequences on population, territory, and organizations.

## Phase 9 — Xianxia opportunities
Scope:
- Secret realms, ruins, inheritance, artifacts, tribulation, ascension.
- Add each only when it connects to current simulation systems.

## Phase 10 — History
Scope:
- Important event records, character/sect timelines, causal explanations.

## Phase 11 — Heavenly Dao
Scope:
- Player powers that modify entities, environment, and rules.
- Inspect and explain consequences.

## Phase 12 — Presentation and polish
Scope:
- Pixel/cell world rendering.
- Entity visuals and animations.
- UI, inspectors, event feed, history view, time controls.
- Performance profiling and optimization.

## Phase gate
Before starting a phase:
1. Confirm dependencies exist.
2. Define the smallest testable task.
3. Define acceptance criteria.
4. Implement only that task.
5. Verify it before proceeding.

Do not implement the whole roadmap in one request.
