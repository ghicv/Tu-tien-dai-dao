# 40 · Phase 0′: chốt nền theo bộ tài liệu mới

**Ngày:** 2026-10-15

## Lý do
- Bro đưa một bộ tài liệu khởi động mới (`TuTienBox_Claude_StarterPack`) và nói: *"làm theo bộ tài liệu mới của tu tiên box đi bro"*, rồi *"lối chơi của tài liệu cũ thì bỏ đi, chơi nhàm lắm"*.
- Bộ pack viết cho một dự án trống, còn repo đã có 39 devlog và 65 test. Nên không làm lại "Phase 0 gốc" mà chốt lại nền: biết mình có gì, tài liệu nào còn hiệu lực.
- Làm theo nhịp của pack: mỗi lần các task nhỏ, có tiêu chí, báo cáo rồi chờ duyệt. **Không có dòng code nào đổi** trong đợt này.

## Đã làm (task 0.1, 0.2, 0.6)
| Task | Kết quả |
|---|---|
| 0.1 Kiểm kê hệ thống | `Docs/Systems.md`: thứ tự chạy, bảng 25 hệ (sở hữu, đọc, ghi, test), phần trình bày, **9 khoản nợ kỹ thuật có bằng chứng** |
| 0.2 Hợp nhất tài liệu | `CLAUDE.md` ở root; `Docs/DECISIONS.md` đã điền; `Docs/ROADMAP.md` có bảng trạng thái thực tế và Phase A, B, C; `Docs/GAME_DESIGN.md`, `WORLD_RULES.md`, `PROJECT_INSTRUCTIONS.md` đưa vào từ pack; `GDD_ThienDao.md` và `WorldRuleSystem.md` đánh dấu đã thay thế / lỗi thời |
| 0.6 Giao thức chơi thử | `Docs/Playtest.md` |

## Phát hiện đáng chú ý
- **D1.** `PerfProbe` và `ChronicleExport` sinh thế giới ở chế độ "đã có người", không phải hồng hoang như ván thật. Mọi số đo dài hạn trước giờ không phản ánh game như người chơi thấy.
- **D2.** 47 trên 53 test dùng chế độ cũ; chỉ 6 test chạy khởi đầu hồng hoang.
- **D3.** Hash trạng thái không bao gồm lớp biome.
- **D4.** Còn nhiều mã chết từ 7 hệ đã gỡ.

## Chưa làm, và lý do
| Task | Lý do |
|---|---|
| 0.3 Phép đo gốc | bro dặn không chạy đo 1000 năm trừ khi được yêu cầu; cần sửa D1 trước |
| 0.4 Spike nhân quả | là code; chờ duyệt thiết kế |
| 0.5 Chính sách tên (IP) và tệp lưu | quyết định của bro, không tự quyết |

## Mâu thuẫn còn mở (ghi trong `DECISIONS.md`)
- Mẫu đặc tả luật: 8 trường (bro) hay 10 mục (pack).
- Tên game: "Tu Tiên Box" hay "Thiên Đạo".
- "Lối chơi của tài liệu cũ" cụ thể là phần nào: t đang hiểu là bỏ cả tài liệu cũ.
