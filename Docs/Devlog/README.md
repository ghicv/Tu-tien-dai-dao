# Devlog — Thiên Đạo

Mỗi tính năng / milestone có một file ghi lại: mục tiêu, đã làm gì, quyết định & thông số, lỗi gặp phải, cách kiểm chứng, commit.

| # | File | Nội dung | Commit |
|---|---|---|---|
| 00 | [00-m0-ban-do.md](00-m0-ban-do.md) | M0 — Bản đồ theo seed, render chunk, brush | `c1d66c1` |
| 01 | [01-m1-thoi-gian-linh-khi.md](01-m1-thoi-gian-linh-khi.md) | M1 — Đồng hồ, mùa, linh khí sống, hàng đợi lệnh | `a9d21c4` |
| 02 | [02-m2-sinh-menh.md](02-m2-sinh-menh.md) | M2 — Làng quần thể, động vật, di dân | `d808dc5` |
| 03 | [03-m2-thu-hoang-quan-the.md](03-m2-thu-hoang-quan-the.md) | M2.1 — Thú hoang thành quần thể, hiển thị tượng trưng | `ee83767` |
| 04 | [04-m3-tu-tien.md](04-m3-tu-tien.md) | M3 — Tu tiên lõi, tu sĩ ra ngoài, popup nhân vật | `1ba8595` |
| 05 | [05-m3-1-truong-hop-bien-va-hieu-ung.md](05-m3-1-truong-hop-bien-va-hieu-ung.md) | M3.1 — Nước nhấn chìm, bất biến, chaos test, hiệu ứng sét/nổ | `d1d0d8f` |
| 06 | [06-ui-kieu-worldbox.md](06-ui-kieu-worldbox.md) | UI kiểu WorldBox — toolbar tab + icon, cửa sổ, thẻ nhân vật | `b86d936` |
| 07 | [07-xem-moi-thu-va-highlight.md](07-xem-moi-thu-va-highlight.md) | Sửa click nhân vật; Xem mọi thứ (thú, vật thể, ô đất); highlight khi rê chuột | `9157b11` |
| 08 | [08-m4-the-luc.md](08-m4-the-luc.md) | M4 — Thế lực: lãnh thổ, linh thạch, ngoại giao, chiến tranh, lập tông, ly khai | `855a9a3` |
| 09 | [09-tha-sinh-vat-hien-ngay.md](09-tha-sinh-vat-hien-ngay.md) | Thả thú hiện ngay tại chỗ click; Thiên Đạo (ban linh căn, cơ duyên, thiên phạt) tác động đúng người được chọn | `9d33469` |
| 10 | [10-m5-xung-dot-lich-su.md](10-m5-xung-dot-lich-su.md) | M5 — HistoryLog, đấu pháp, sư đồ & báo thù, StoryDetector, biên niên sử, bảng cường giả theo dõi được | `12d446e` |
| 11 | [11-nhan-vat-chinh.md](11-nhan-vat-chinh.md) | Nhân vật chính: danh sách theo dõi, ProtagonistAI, túi trữ vật, bấm để follow | `89b2e1d` |
| 12 | [12-m6-thien-kiep-thien-tai.md](12-m6-thien-kiep-thien-tai.md) | M6 (phần 1) — Thiên kiếp & lôi địa; thiên tai: động đất, núi lửa, lũ lụt, hạn hán, ôn dịch, thú triều | `f5122ab` |
| 13 | [13-m6-quy-luat-sinh-tu-dai-kiep.md](13-m6-quy-luat-sinh-tu-dai-kiep.md) | M6 (phần 2) — Quy luật, hồi sinh & diệt môn, đại kiếp, thời tiết, đánh lén lúc độ kiếp, lớp phủ thiên tai | `4178ecf` |
| 14 | [14-ui-it-chu-nhieu-icon.md](14-ui-it-chu-nhieu-icon.md) | UI ít chữ, nhiều icon: HUD chip + cảnh báo, tin có icon, thẻ nhân vật dạng chip, thống kê/sự kiện/cường giả bằng icon | `5296fe5` |
| 15 | [15-m7-chieu-sau-the-gioi.md](15-m7-chieu-sau-the-gioi.md) | M7 — Yêu thú & yêu tộc, bí cảnh từ lịch sử, kinh tế & thương lộ, thời đại & mạt pháp; hiệu ứng đánh nhau pixel, nháy trắng, khói trắng | `173fa71` |
| 16 | [16-luu-tai-va-hieu-nang.md](16-luu-tai-va-hieu-nang.md) | Lưu / tải thế giới (F5/F9, tự lưu), đo hiệu năng 1000 năm, sửa lỗi bí cảnh, chuột phải bỏ chọn | `97b495f` |
| 17 | [17-toi-uu-hieu-nang.md](17-toi-uu-hieu-nang.md) | Tối ưu: lưới tra cứu lười, cache ruộng, chunk chỉ trên GPU, job nền trong Editor (148 → 101 ms/năm ở năm 1000) | `38b64a4` |
| 18 | [18-dai-vuc-va-kho-lore.md](18-dai-vuc-va-kho-lore.md) | Đại vực kiểu Thiên Nam, nước phàm nhân, kho lore JSON chia loại (mỗi map bốc thăm), texture riêng cho từng loại đất và từng vùng | _(chờ)_ |

Nguyên tắc xuyên suốt (người chơi đã chốt):
- Mọi tên gọi / lore theo **Phàm Nhân Tu Tiên** (`Assets/ThienDao/Resources/Lore/*.json`, mỗi thế giới bốc thăm một phần).
- **Số lượng mô phỏng có thể lớn, trên map chỉ vẽ tượng trưng.**
- Mục tiêu: **một thế giới sống động nhất có thể.**
- Tu tiên **cực khó**: Kết Đan là trưởng lão, Nguyên Anh là bá chủ một phương, Hóa Thần là truyền thuyết.
