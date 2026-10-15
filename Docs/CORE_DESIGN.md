# Thiết kế lại lõi mô phỏng — bản nháp để duyệt

**Ngày:** 2026-10-15 · **Trạng thái:** nháp, **chưa code**. Chờ bro duyệt trước khi làm task C1.

Mẫu đặc tả luật dùng ở mục 6 là mẫu 10 mục của pack, **tạm**; chưa phải quyết định (xem `DECISIONS.md`, "Chưa chốt").

---

## 1. Đã chốt (2026-10-15)
- **Phạm vi:** lõi mô phỏng và luật. **Giữ** bản đồ, sinh thế giới, biome, linh khí, trình bày pixel, giao diện, đồng hồ, hàng đợi lệnh, lưu/tải.
- **Cách làm:** lưu trữ rồi viết mới tại chỗ. Bản hiện tại đã gắn tag `archive/core-v1`.
- **Art:** giữ nguyên 2D pixel hiện tại.
- **Bốn mục tiêu** của việc làm lại, bro chọn cả bốn:

| # | Mục tiêu | Tiêu chí kiểm được |
|---|---|---|
| G1 | Nhân vật có bản sắc, khát vọng, quan hệ → **cảm xúc** | với một tu sĩ bất kỳ, game trả lời được "muốn gì, vì ai, sợ gì" **bằng dữ liệu**, không bằng chuỗi chữ dựng sẵn; cái chết của người có ràng buộc để lại hệ quả hành vi |
| G2 | Nhân quả rõ ràng, không ngẫu nhiên | mọi sự kiện lớn có `Cause` hợp lệ; lần ngược từ sự kiện bất kỳ tới gốc được; mỗi quyết định ghi lại lý do |
| G3 | Quá trình tu luyện hữu hình (đạo, tâm cảnh, bình cảnh, thiên kiếp) | một lần đột phá hiện ra điều kiện đủ/thiếu, có chuẩn bị, có kết quả phân bậc, xác suất và lý do nhìn thấy được |
| G4 | Ít hệ nhưng sâu | từ 27 mục kiểm kê (`Docs/Systems.md`) xuống **không quá 8 hệ mô phỏng**; mỗi hệ có đặc tả và test riêng |

---

## 2. Chẩn đoán: vì sao game "không có cảm xúc, ngẫu nhiên, không rõ sự tu tiên"

| Triệu chứng bro nói | Nguyên nhân trong code hiện tại |
|---|---|
| **Ngẫu nhiên** | Việc ngoài kia chọn theo trọng số cố định rồi tung xúc xắc. Đột phá là một lần tung xúc xắc có hệ số. Không lưu lý do, nên người chơi không có gì để hiểu. |
| **Không có cảm xúc** | Một tu sĩ là ba con số (ngộ tính, khí vận, tâm cảnh) cộng vài số nguyên chỉ sang người khác (sư phụ, kẻ thù, gia tộc). Chỉ có **thù** sinh ra hành động. Không có người nào cần, sợ, nợ ai. Cái chết là một dòng chữ. |
| **Không rõ sự tu tiên** | Tu luyện là `Progress += gain` mỗi tháng, hiện thành một thanh. Không có đạo, không có lựa chọn, không có chuẩn bị, không có cái giá. Bình cảnh chỉ là "thanh đầy mà chưa qua". |
| **Cảm giác trống, rời rạc** | 27 mục kiểm kê, mỗi hệ vài trăm dòng, tác động nhau qua tác dụng phụ. Nhiều hệ nông; không hệ nào đủ sâu để tạo ra một câu chuyện tự nó. |

**Chẩn đoán này là giả thuyết.** Nó chỉ được kiểm bằng chơi thử `Docs/Playtest.md` sau khi có lát cắt dọc (task C6).

---

## 3. Nguyên tắc cho lõi mới
1. **Một sinh linh, nhiều lớp:** tu sĩ, yêu thú, phàm nhân đáng chú ý dùng chung một mô hình, khác nhau ở dữ liệu, không ở hệ riêng.
2. **Mọi quyết định có lý do**, và lý do là dữ liệu (các yếu tố và trọng số), không phải chữ.
3. **Mọi sự kiện có nguyên nhân** (`Cause`) hoặc ghi rõ "tự nhiên" (luật thế giới).
4. **Cảm xúc là trạng thái có hệ quả hành vi**, không phải nhãn trang trí.
5. **Quy mô nhỏ có chủ đích:** phàm nhân vẫn là số liệu; chỉ người "đáng chú ý" mới là cá thể (đã đúng trong lõi cũ).
6. **Tất định:** chỉ `DetRandom`, mọi thay đổi do người chơi qua `IWorldCommand`; phát lại được.
7. **Không ECS/DOTS, không abstraction cho tương lai** (theo `CLAUDE.md`).

---

## 4. Mô hình dữ liệu đề xuất

### Sinh linh (`Being`)
| Nhóm | Trường | Ý nghĩa |
|---|---|---|
| Định danh | tên, sinh, loài (người, yêu thú), nơi ở | |
| **Bẩm sinh** | linh căn, ngộ tính, **khí chất** (ba trục) | khí chất: *chí khí* (vươn lên ↔ an phận), *nhân tâm* (từ bi ↔ tàn nhẫn), *chấp niệm* (nặng tình ↔ buông bỏ) |
| **Đạo** | con đường theo đuổi: kiếm, đan, trận, thể, trường sinh, hộ đạo, báo thù… | chọn khi có đủ cơ duyên và khí chất; có thể đổi, phải trả giá |
| **Tâm** | tâm cảnh (0..1), gánh nặng (ân, oán, hối, nghiệp) | đổi theo sự kiện; ngưỡng tâm cảnh là điều kiện đột phá |
| **Ràng buộc** | danh sách `Bond` | sư–đồ, đạo lữ, huynh đệ, tri kỷ, ân, oán, cừu; có cường độ và sự kiện gốc |
| **Muốn** | mục tiêu hiện tại + lý do | suy ra từ đạo, tâm, ràng buộc, nhu cầu, thọ nguyên còn |
| Tu vi | cảnh giới, tầng, tích lũy, thọ nguyên | giữ thang Phàm Nhân Tu Tiên |
| Của cải | linh thạch, đan dược, công pháp, pháp bảo | |

### Sự kiện (`Event`)
`Id`, tick, loại, chủ thể (cho phép nhiều), nơi, **`Cause`** (id sự kiện hoặc mã luật thế giới), **tối đa 3 yếu tố** (khóa, giá trị) đã dẫn tới nó, mức quan trọng.

---

## 5. Tám hệ thay cho hơn hai mươi

| # | Hệ | Sở hữu | Thay các hệ cũ |
|---|---|---|---|
| 1 | **Thế giới** (giữ) | địa hình, biome, linh khí, thiên tai, thời đại | World, Qi, Forage, Scar, Disaster, Era |
| 2 | **Sinh tử** | sinh, già, chết, quần thể phàm nhân, thú hoang | Settlement (phần dân số), Wildlife, Harm |
| 3 | **Đạo** (tu luyện) | tích lũy, bình cảnh, đột phá, thiên kiếp | Cultivation (phần tu luyện), Techniques (phần học) |
| 4 | **Tâm và ràng buộc** | tâm cảnh, `Bond`, gánh nặng | mới (một phần Clan, Combat.vendetta) |
| 5 | **Ý chí** (quyết định) | mục tiêu và lý do; chọn hành động | Errands, Outings, Combat (phần quyết định), Beast (phần hành vi) |
| 6 | **Tranh đoạt** | xung đột cá nhân, nhóm, thú tộc, hung thú | Combat, Faction (chiến tranh), BeastWars, BeastChase, hung thú |
| 7 | **Truyền thừa và môn phái** | môn phái, công pháp theo dòng, bí cảnh, sơn môn | Faction, SectComplex, Technique, Relic, Clan |
| 8 | **Biên niên** | sự kiện có nhân quả, truy vấn "vì sao" | EventLog, HistoryLog, StoryDetector |

Hệ 1 dùng lại gần nguyên. Hệ 2–8 viết mới, tái dùng công thức và dữ liệu đã đo ở lõi cũ khi hợp lý, không chép nguyên mã.

---

## 6. Ví dụ đặc tả một luật: Đột phá (mẫu 10 mục của pack)

| Mục | Nội dung |
|---|---|
| **Purpose** | biến "thanh đầy" thành một khoảnh khắc có chuẩn bị, có rủi ro, có giá phải trả |
| **Inputs** | tích lũy, tâm cảnh, nơi bế quan (linh khí, biome), vật phẩm chuẩn bị (đan, pháp bảo hộ pháp), công pháp, khí chất, nghiệp, người hộ pháp |
| **Preconditions** | tích lũy đã đầy; chưa ở trạng thái thương tích nặng; có công pháp cho phép bước kế |
| **Cadence** | kiểm tra hằng tháng; người đủ điều kiện **chủ động chọn lúc** (hệ Ý chí) |
| **Action** | vào bế quan ở một chỗ; thời gian chuẩn bị theo cảnh giới; sau đó thử |
| **Kết quả** | điểm = f(các điều kiện) + nhiễu có biên (±0,1, tất định theo seed). Phân bậc: *thành công* · *thành công nhưng tổn thương* · *thất bại nhẹ, lùi tích lũy* · *tẩu hỏa nhập ma, tâm vỡ, có thể thành ma tu* · *vẫn lạc* |
| **Side effects** | sự kiện có `Cause` = quyết định bế quan và các yếu tố; ràng buộc đổi (ví dụ đồ đệ sợ, kẻ thù mừng); tâm cảnh đổi |
| **Systems affected** | Tâm và ràng buộc, Ý chí (đổi mục tiêu), Biên niên, Truyền thừa (người thành công có thể mở dòng) |
| **Edge cases** | bị tấn công lúc bế quan; nơi bế quan bị thiên tai; người hộ pháp chết; hết thọ nguyên trong lúc chuẩn bị |
| **Tests** | cùng điều kiện cho cùng kết quả (tất định); thiếu điều kiện làm giảm điểm đúng chiều; mỗi bậc kết quả có thể xảy ra; sự kiện sinh ra có `Cause` hợp lệ |
| **Long-term** | người có tâm cảnh vững lên cảnh giới cao hơn; kẻ dùng đan để vượt thường có tâm yếu, dễ tẩu hỏa về sau |

Các con số cụ thể (ngưỡng, hệ số, độ rộng nhiễu) **chưa chốt**; chốt khi làm task C5 và kiểm bằng test.

---

## 7. Ranh giới với trình bày (để giữ nguyên art và giao diện)
Số đo hiện tại (2026-10-15):
- Lõi cũ: 13.053 dòng, 33 file. Trình bày (`Render`, `UI`, `Player`, `WorldBootstrap`): khoảng 11.200 dòng.
- Trình bày chạm vào lõi ở **7 file**: `FxRenderer`, `SectRenderer`, `UnitRenderer`, `WorldRenderer`, `ChronicleWindow`, `GameUI`, `WorldBootstrap`; khoảng **190 chỗ đọc**. Kiểu `Cultivator` xuất hiện 105 lần, `Settlement` 45, `Beast` 34.

Đề xuất: một mặt cắt đọc `SimView` (chỉ đọc) mà trình bày dùng thay cho việc gọi thẳng các hệ. Lõi mới và lõi cũ cùng phải cung cấp `SimView`; nhờ đó thay lõi từng hệ mà trình bày không đổi. Đây cũng là điều pack yêu cầu ("render không giữ sự thật").

---

## 8. Phương án và đánh đổi

| | A. Viết lại một lần ("big bang") | B. Đổi dần sau mặt cắt đọc (**đề xuất**) |
|---|---|---|
| Cách làm | xóa lõi cũ, viết lõi mới, nối lại trình bày | dựng `SimView` trước; viết từng hệ mới; mỗi bước thay một hệ cũ |
| Chạy được trong lúc làm | **Không** nhiều tuần | **Có** ở mỗi bước |
| Test cũ | mất gần hết | giữ những test còn đúng; bỏ test của hệ đã thay |
| Rủi ro | lớn, khó biết khi nào hỏng | thấp hơn; chi phí là cầu nối tạm |
| Phù hợp "lưu trữ rồi viết mới tại chỗ" | Có | Có |

**Nói thẳng về rủi ro:**
1. Làm lại lõi **không tự đảm bảo** game có cảm xúc. Cảm xúc tới từ nội dung thiết kế: khí chất, đạo, ràng buộc, cái giá. Kiến trúc chỉ để chúng có chỗ sống. Nếu thiết kế này sai, viết lại cũng vô ích. Vì vậy phải có lát cắt dọc sớm (C6) để chơi thử.
2. Mất khoảng 13.000 dòng và 65 test (khoảng 1/5 test còn đúng sau khi đổi). Bù lại có tag `archive/core-v1`.
3. "Ít mà sâu" có thể làm thế giới trông ít sự kiện hơn. Cần chú ý giữ cảm giác thế giới sống.

---

## 9. Kế hoạch task (mỗi task độc lập, có tiêu chí)

| ID | Task | Tiêu chí hoàn thành |
|---|---|---|
| C0 | **Lưu trữ** | tag `archive/core-v1` — **xong** |
| C1 | **Mặt cắt đọc `SimView`**: trình bày chỉ đọc qua nó; lõi cũ vẫn đứng sau | `grep` truy cập trực tiếp các hệ cũ trong `Render`, `UI`, `Player`, `WorldBootstrap` = 0; 65 test pass; cùng seed chạy N năm ra cùng hash như trước; ảnh chụp giống nhau |
| C2 | **Sinh linh, `Bond`, tâm** (chỉ dữ liệu) | tạo, lưu/tải, băm, khôi phục đúng; test vòng đi–về |
| C3 | **Biên niên có `Cause`** | 3 chuỗi mẫu lần ngược ≥ 3 mắt xích; 100% sự kiện quan trọng có `Cause` hoặc mã luật |
| C4 | **Ý chí**: quyết định có lý do, thay việc ngoài kia | với một sinh linh bất kỳ truy vấn được mục tiêu và lý do; cùng seed cùng quyết định |
| C5 | **Đạo**: đột phá hữu hình theo mục 6 | bốn test ở mục 6 pass; giao diện hiện điều kiện đủ/thiếu |
| C6 | **Lát cắt dọc**: một đời tu sĩ có đạo, ràng buộc, đột phá, cái chết có sức nặng | chơi thử theo `Docs/Playtest.md`, ghi kết quả bốn bước |
| C7+ | Thay dần các hệ còn lại (Tranh đoạt, Truyền thừa, Sinh tử) | mỗi hệ cũ xóa khỏi nhánh chính khi hệ mới thay đủ; số hệ ≤ 8 |

Mỗi task kết thúc bằng báo cáo và chờ bro duyệt task kế, đúng quy trình của pack.

---

## 10. Cần bro duyệt hoặc quyết
1. **Hướng B** (đổi dần sau mặt cắt đọc) hay A (viết một lần)?
2. **Nội dung thiết kế ở mục 4:** ba trục khí chất, danh sách đạo, loại ràng buộc. Đây là nội dung của game, không phải kỹ thuật; bro sửa trực tiếp được.
3. **Yêu thú dùng chung mô hình sinh linh** (đơn giản hóa phần tâm), hay giữ mô hình riêng?
4. **Phàm nhân đáng chú ý** (con cháu, người thân của tu sĩ) có là sinh linh cá thể khi cần cho ràng buộc hay không?
5. Cho phép bắt đầu **C1**?
