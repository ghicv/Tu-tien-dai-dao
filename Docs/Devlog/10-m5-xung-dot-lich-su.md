# 10 · M5 — Xung đột & lịch sử

**Ngày:** 2026-10-06

## Mục tiêu (roadmap M5)
- Chiến đấu cá nhân, trận đánh trừu tượng, HistoryLog, giao diện biên niên sử, StoryDetector v1.
- Kiểm chứng: **đọc được "câu chuyện" sau 1.000 năm**.
- Người chơi bổ sung giữa chừng: bấm vào cao thủ trên bảng xếp hạng thì camera theo tới vị trí hiện tại; nếu người đó đang bế quan thì báo.

## Đã làm

### HistoryLog (`Sim/HistoryLog.cs`)
- **Vai trò:** trí nhớ của thế giới.
  - `EventLog` chỉ giữ 400 dòng gần nhất cho dòng tin.
  - Mọi sự kiện có importance ≥ 2 được lưu suốt ván chơi, cộng với các sự kiện importance ≥ 1 có nhân vật cụ thể (đột phá Trúc Cơ, lần giết người đầu tiên…).
- **Mỗi bản ghi** có `A/B` (chỉ số tu sĩ: chủ thể và đối phương), `FA/FB` (thế lực), vị trí, thời điểm.
- **Truy vấn:** lập chỉ mục theo người (`OfCultivator`) và theo thế lực (`OfFaction`), tìm theo thế kỷ (`InCentury`). Ngoài ra đếm **mọi** sự kiện theo từng thế kỷ để làm số liệu cho biên niên.
- `EventLog.Add` nhận thêm tham số actor; các sự kiện chính của tu sĩ và thế lực đều đã được gắn người.

### Lịch sử cá nhân (`Cultivator`)
- **Dữ liệu mới:**

  | Trường | Ý nghĩa |
  |---|---|
  | `MasterIdx` | sư phụ |
  | `Nemesis`, `NemesisFor`, `NemesisTick` | huyết thù: ai giết ai, từ năm nào |
  | `HuntTarget` | người đang bị truy sát |
  | `Kills`, `KilledBy`, `DeathTick` | số mạng đã giết, bị ai giết, năm mất |
  | `Fame` | danh vọng |
  | `Legend`, `Epithet` | huyền thoại, danh hiệu |
  | `Blessed` | đã được Thiên Đạo điểm hóa |
  | `OriginRoots` | linh căn lúc sinh ra |

- **Bái sư:** đệ tử mới vào tông được giao một sư phụ là trưởng lão Kết Đan+ của tông (không có thì lấy người Trúc Cơ cao hơn mình). Tông môn lúc khởi đầu cũng được chia sư đồ.

### Đấu pháp (`Sim/CombatSystem.cs`)
- **Sức mạnh** = `Realms.Power` theo cảnh giới (1 / 6 / 36 / 200 / 1200) × tiểu cảnh giới × tâm cảnh × khí vận × ma tu ×1,15. Mỗi trận tung ngẫu nhiên ×0,6–1,4.
- **Kết quả:** bên thua chết với xác suất = cơ sở × 2^(cảnh giới bên thắng − bên thua); nếu không chết thì trọng thương bỏ chạy (mất 30% tu vi). Bên thắng đoạt túi trữ vật (+tu vi).
- **Gặp nhau trên đường** (tu sĩ đang ra ngoài, trong 3 ô):
  - Tông đang giao chiến: 70% đánh nhau. Thù địch: 30%.
  - Ma tu gặp người yếu hơn: 35% "giết người đoạt bảo".
  - Chính – tà gặp nhau: 25%.
  - Mỗi người tối đa một trận mỗi năm.
- **Huyết thù và báo thù:**
  - Bị giết thì sư phụ và các đệ tử của người chết ghi thù kẻ giết.
  - Mỗi năm, người mang thù đã đủ mạnh (≥ 0,8 lần kẻ thù, hoặc sắp hết thọ) có 50% lên đường **truy sát**: ngự kiếm bám theo vị trí kẻ thù tối đa 2 năm, tới nơi thì đấu sinh tử.
  - Thua mà còn sống thì vẫn ôm thù. Kẻ thù chết vì lý do khác thì "trời đã đòi nợ".
- **Trận đánh giữa tông môn** (M4) giờ là các **cặp đấu pháp**:
  - Tu sĩ hai bên ghép cặp từ mạnh đến yếu.
  - Người chết và người bỏ chạy không còn tính vào lực của phe mình, rồi mới phân thắng bại.
  - Nhờ vậy biết chính xác ai giết ai, và từ đó sinh ra huyết thù.

### StoryDetector (`Sim/StoryDetector.cs`)
- **Danh vọng:** tham gia sự kiện importance *i* thì được *i*² danh vọng (người bị tác động được một nửa).
- **Các mẫu truyện**, mỗi chuyện chỉ kể một lần:

  | Truyện | Điều kiện |
  |---|---|
  | **Báo thù rửa hận** | giết đúng kẻ đã giết sư phụ mình, hoặc ôm hận từ 5 năm trở lên |
  | **Khi sư diệt tổ** | giết chính sư phụ |
  | **Vượt cấp trảm địch** | cảnh giới thấp hơn mà giết được Kết Đan+ |
  | **Sát tinh giáng thế** | 10 mạng |
  | **Phế vật nghịch thiên** | ngũ linh căn lên Kết Đan, hoặc tứ linh căn lên Nguyên Anh, không nhờ Thiên Đạo |
  | **Thiên mệnh chi tử** | người được Thiên Đạo điểm hóa lên Nguyên Anh |
  | **Tán tu nghịch tập** | không sư thừa mà lên Nguyên Anh |
  | **Hóa Thần hiện thế** | đột phá Hóa Thần |
  | **Thanh xuất ư lam** | phân tông diệt tông mẹ |
  | **Thanh lý môn hộ** | tông mẹ diệt phân tông |
  | **Cổ tông diệt vong** | tông trên 200 năm bị diệt |
  | **Huyết hải thâm thù** | hai tông 4 lần binh đao |
  | **Đồng môn tương tàn** | phân tông và tông mẹ khai chiến sau từ 50 năm trở lên |
  | **Tiểu tông thành bá chủ** | tông lập trong ván chơi lên đứng đầu |
  | **Lão quái ngàn năm** | sống quá 1.000 tuổi |
  | **Danh chấn thiên hạ** | danh vọng ≥ 100 và từ Kết Đan trở lên thì thành huyền thoại |

- **Danh hiệu:** ghép theo hành vi và linh căn, không trùng nhau, ví dụ "Huyết Sát Chân Nhân", "Xích Viêm Ma Tôn", "Hàn Băng Tổ Sư", "Kim Kiếm Tán Tiên". Tên hiển thị thành "Huyết Sát Chân Nhân Tần Diệu".
- **Truyện được kể ở đâu:** vào dòng tin dạng `【Tên truyện】 …`, đồng thời ghi vào HistoryLog.

### Giao diện
- **Biên niên sử** (icon sách góc trên phải, hoặc phím **H**): cửa sổ lớn, cuộn bằng con lăn, có 3 tab:
  - **Biên niên:** từng thế kỷ có số liệu (lập tông, ly khai, tuyên chiến, đại chiến, diệt môn, đấu pháp, đột phá, thiên kiếp) và các đại sự importance 3.
  - **Truyền kỳ:** mới nhất trước.
  - **Danh nhân:** xếp theo danh vọng; ghi danh hiệu, cảnh giới, tông, còn sống hay đã vẫn lạc (dưới tay ai), linh căn gốc, số mạng, sư phụ, 3 đại sự tiêu biểu.
- **Thẻ tu sĩ:**
  - Có thêm sư phụ, huyết thù (kẻ thù và người đã mất), số mạng, mốc "lưu danh sử sách".
  - Người đã mất ghi rõ năm mất và kẻ giết.
  - Phần **Tiểu sử** lấy từ HistoryLog thay vì tìm tên trong 400 dòng gần nhất.
- **Thẻ tông môn:** có thêm mục **Sử sách**, gồm 4 đại sự gần nhất.
- **Bảng cường giả:**
  - Mỗi dòng là một nút: tên, cảnh giới, vai trò, và *đang làm gì* hoặc *đang bế quan*.
  - Bấm vào: chọn người đó và camera bay tới, bám theo nếu họ đang ở ngoài. Nếu họ đang bế quan thì hiện thông báo "… đang bế quan tại …, không xuất hiện trên bản đồ".
- **Menu Editor** "Thiên Đạo/Mô phỏng 1000 năm": chạy 1.000 năm không render, xuất `Docs/Chronicle_ThienDao.md`.

## Cân chỉnh sau lần chạy 1.000 năm đầu tiên
Kết quả lần đầu: 417 truyền kỳ, trong đó 209 "Phế vật nghịch thiên" và 117 "Danh chấn thiên hạ"; 194 lần thiên kiếp mỗi thế kỷ; 251 Kết Đan.
- **Thiên kiếp** chỉ giáng khi đã phá được bình cảnh lên Nguyên Anh hoặc Hóa Thần. Trước đây lần thử nào cũng có thiên kiếp. Tỉ lệ thành công tổng thể không đổi.
- **Tỉ lệ đột phá:** Kết Đan 4% → 3%, Nguyên Anh 1,5% → 1,2%.
- **Trục xuất:** trưởng lão khác chính/ma với tông bị trục xuất 25% → 12%/năm.
- **Truyện:** siết điều kiện như bảng trên. Ngưỡng huyền thoại tăng 40 → 100.
- **Ly khai:** khi thế giới đã đủ 24 thế lực thì tỉ lệ ly khai còn 30%, vì không còn chỗ lập tông mới.
- **Kết quả sau cân chỉnh** (seed ThienDao, 1.000 năm, khoảng 2 phút):
  - 36.550 phàm nhân, 1.302 tu sĩ (Kết Đan 156, Nguyên Anh 20, Hóa Thần 0), 24 thế lực.
  - Sử sách ghi 23.311 sự kiện, sinh ra **124 truyền kỳ**:

    | Truyện | Số lần |
    |---|---|
    | Báo thù rửa hận | 36 |
    | Huyết hải thâm thù | 26 |
    | Sát tinh giáng thế | 20 |
    | Đồng môn tương tàn | 15 |
    | Danh chấn thiên hạ | 11 |
    | Thanh lý môn hộ | 5 |
    | Phế vật nghịch thiên | 5 |
    | Cổ tông diệt vong | 5 |
    | Tiểu tông thành bá chủ | 1 |

  - **Đã đọc được câu chuyện:**
    - "Năm 636, Vạn Bá giết Lâm Uyển, sư phụ của Liễu Mộng. Ôm hận 126 năm, nay Liễu Mộng chân nhân tự tay chém kẻ thù."
    - Có cả chuỗi báo thù nối tiếp: năm 403 Nam Cung Khuyết giết Mặc Thiên Đô; năm 426 Tề Huyền, đệ tử của Mặc Thiên Đô, giết lại Nam Cung Khuyết.
  - Bản biên niên đầy đủ: `Docs/Chronicle_ThienDao.md`.

## Kiểm chứng
- **Test mới:**
  - `HistoryRemembersBeyondTheTicker`
  - `KillingAMasterSwornRevengeBecomesAStory`: giết sư phụ thì đệ tử thề báo thù; báo được thì thành truyện và món nợ được xóa.
  - `HigherRealmUsuallyWinsADuel`
- **WorldInvariants** kiểm tra thêm:
  - Liên kết giữa các nhân vật hợp lệ: không tự làm sư phụ hay kẻ thù của chính mình.
  - Không truy sát người đã chết; người chết không còn ở chiến trường.
  - Sử sách đúng thứ tự thời gian.
