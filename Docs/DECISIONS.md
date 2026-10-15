# Tu Tiên Box — Decision Log

Record confirmed decisions here. Do not treat suggestions as decisions until the user approves them.

Điều gì thuộc "đã chốt" ở đây là điều bro đã nói hoặc đã chọn trong hội thoại; mọi thứ t suy luận từ code nằm ở `Docs/Systems.md`, không nằm ở đây.

## Confirmed vision
- Player is Heavenly Dao / God, not a directly controlled character.
- The world should autonomously evolve and create emergent stories.
- Cultivation, inheritance, sect evolution, and history are the major differentiators.
- Simulation data/logic is separated from rendering and UI.
- **Pixel/cell-based world rendering is confirmed**, not only a direction: 8 px per cell, point filter.

### 2026-10-09 — Định vị sản phẩm
- **Decision:** `Docs/Vision.md` là kim chỉ nam. Ba trụ cột: tu luyện của cá thể; tông môn và truyền thừa; Thiên Đạo sandbox. Câu chốt: *"một thế giới nơi con đường tu luyện của từng cá thể có thể thay đổi vận mệnh của cả nền văn minh."*
- **Context:** bro chơi thử 5 phút, thấy trống, chọn bỏ hướng "clone WorldBox".
- **Consequences:** mỗi tính năng mới phải phục vụ một trụ; không thêm hệ chỉ để có nhiều tính năng.

### 2026-10-09 — Phạm vi
- **Decision:** gỡ Thiên mệnh, AI riêng của nhân vật chính, chính trị hoàng triều, thương mại và thương đội, đường mòn và đường đất, cầu nguyện và tín ngưỡng làng, tin đồn, công cụ đặt nhà và đặt tông môn.
- **Bro đã nói riêng:** "bỏ đi" cho đường mòn, chính trị, cầu nguyện; phần còn lại theo câu "hệ thống hiện tại đi sai hướng, cái nào bỏ được thì bỏ".
- **Consequences:** phàm nhân chỉ còn là nền ("đất trồng"); kinh tế và chính trị phàm nhân không nằm trong roadmap.

### 2026-10-09 — Thế giới
- **Decision:** ván mới nào cũng mở màn ở khai thiên lập địa; chưa có người, chưa có tông môn; chỉ có yêu thú và 5 chân linh (Chân Long, Thiên Phượng, Kỳ Lân, Côn Bằng, Huyền Vũ).
- **Decision:** nhân tộc xuất hiện **cả hai cách**: tự xuất hiện khi tới thời kỳ, và Thiên Đạo tạo ra.
- **Decision:** tên các thời kỳ theo kiểu Hồng Hoang; tên sinh vật, công pháp, địa danh theo Phàm Nhân Tu Tiên.
- **Decision:** tông môn **ít mà lớn**: 5–8 đại tông và thánh địa, mỗi nơi là một quần thể có quy hoạch, cộng vài tiểu môn phái.
- **Decision:** làm biome theo phong cách tu tiên giới, mỗi biome có tác dụng thật trong mô phỏng.

### 2026-10-15 — Mục tiêu và nhịp làm việc
- **Decision:** mục tiêu là **game hoàn chỉnh lên Steam**.
- **Decision:** ván mới luôn bắt đầu ở hồng hoang, **có nút tua nhanh tới mốc** (có người, có tu sĩ, có tông môn).
- **Decision:** thứ tự giai đoạn A: nhịp thời gian → thiên kiêu bảng, cường giả bảng → sợi nhân quả → phản hồi quyền năng → gom quyền năng → chơi thử 15 phút.
- **Decision:** sandbox thuần, **có thành tựu nhẹ** (kiểu "người đầu tiên phi thăng", "tông môn ngàn năm"); không thắng thua.
- **Decision:** theo bộ tài liệu mới (pack): mỗi lần một task nhỏ, có tiêu chí, báo cáo rồi chờ duyệt.
- **Decision:** **lối chơi của tài liệu cũ (`GDD_ThienDao.md`) bị bỏ** vì "chơi nhàm lắm". Cùng ngày bro bảo xóa hết tài liệu cũ: đã xóa `GDD_ThienDao.md`, `WorldRuleSystem.md`, `Chronicle_ThienDao.md`, `Perf_ThienDao.md` và 12 ảnh cột mốc không còn ai dùng. Còn trong lịch sử git (commit `742670f`).
- **Decision:** **giữ nguyên phong cách art 2D pixel hiện tại.** Bảng màu, sprite, pipeline chunk texture và cách vẽ hiệu ứng không bị làm lại.
- **Decision:** **làm lại từ đầu hệ thống core game.** Phạm vi và cách làm chưa chốt; xem "Cần bro xác nhận".

## Quy ước làm việc đã chốt (không ghi ngày)
- Mọi hình ảnh, kể cả hiệu ứng, là pixel art.
- Hiệu năng đặt lên đầu nhưng không cắt chiều sâu.
- Mọi hoạt động mô phỏng phải hiện thấy được trên map.
- Quyền năng tác động lên đúng cái được bấm, hiện ngay tại chỗ.
- Số lượng có thể lớn (hàng nghìn phàm nhân): phàm nhân, dân làng trên map vẽ tượng trưng. Riêng thú hoang vẽ **đúng số lượng trong vùng camera**, tắt khi zoom xa (2026-10-11).
- Mỗi cột mốc có một devlog trong `Docs/Devlog` và một dòng trong README của nó.
- **Không chạy phép đo cân bằng 1000 năm sau mỗi thay đổi; chỉ khi bro yêu cầu** (2026-10-09).

## Decisions that are implemented (technical facts, not choices still open)
- Engine: **Unity 6000.3.9f1**, URP 2D; nền tảng **PC** (Windows trước).
- Bản đồ cố định **1024×1024**, sinh từ seed tất định; chunk 32×32 ô, mỗi chunk một texture; vật thể đóng dấu vào texture, vật thể chuyển động vẽ bằng sprite.
- Mô phỏng tất định: **phát lại được từ seed và log lệnh**; mọi quyền năng đi qua `IWorldCommand`.
- Lưu/tải toàn bộ đồ thị đối tượng, có bảng kiểu, từ chối tệp khác cấu trúc.
- Các tầng "hồng hoang → nhân tộc → thủy tổ → thượng cổ…" tự nhận ra từ trạng thái thế giới, không lập trình sẵn.

## Not yet decided
- **Mẫu đặc tả luật:** 8 trường của bro (`Docs/WorldRules.md`) hay 10 mục của pack (`Docs/WORLD_RULES.md`). Hiện chưa viết luật mới theo mẫu nào.
- **Tên game chính thức:** "Tu Tiên Box" hay "Thiên Đạo". Hai tên đang dùng song song.
- **Nguồn tên và lore khi phát hành thương mại (rủi ro IP):** hiện đang theo "Phàm Nhân Tu Tiên"; bản thân mã có ví dụ dùng tên nhân vật gốc. Chưa có quyết định.
- **Chính sách tệp lưu cho bản phát hành:** hiện chưa giữ tương thích giữa các phiên bản; cần quy tắc khi lên Steam.
- **Hardware mục tiêu và số thực thể:** chưa chốt; máy phát triển có GPU yếu (GT 730).
- **Ngày/đêm:** chưa có; chưa quyết định có làm hay không.
- **Phi thăng:** chưa có; cảnh giới cao nhất là Hóa Thần.
- **Demo Steam / bản build:** chưa có bản build ngoài Editor.
- **Giao diện cuối cùng và tutorial.**
- **Multiplayer:** không thuộc vision hiện tại.

## Cần bro xác nhận
- **Phạm vi "làm lại từ đầu core game":** phần nào là core (chỉ lõi mô phỏng? cả sinh thế giới? cả trình bày và giao diện?), giữ gì (hạ tầng tất định, hàng đợi lệnh, lưu/tải, test, trình bày pixel), và code hiện tại xử lý ra sao (lưu trữ rồi viết lại tại chỗ, hay viết song song). Đang chờ bro quyết.
- **Còn `Docs/Devlog`** (40 devlog và ảnh minh họa): giữ hay xóa nốt. Hiện giữ vì đó là nhật ký công việc, không phải tài liệu thiết kế.
- **Chú thích trong code** còn nhắc "GDD §N" ở 5 chỗ và nhiều chỗ nhắc hệ đã gỡ (`Docs/Systems.md`, nợ D5): chưa sửa.

## Decision template
- Date:
- Decision:
- Context/problem:
- Options considered:
- Trade-offs:
- Consequences:
