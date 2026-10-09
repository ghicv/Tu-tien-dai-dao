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
| 18 | [18-dai-vuc-va-kho-lore.md](18-dai-vuc-va-kho-lore.md) | Đại vực kiểu Thiên Nam, nước phàm nhân, kho lore JSON chia loại (mỗi map bốc thăm), texture riêng cho từng loại đất và từng vùng | `fe5faa3` |
| 19 | [19-vet-seo-thien-tai.md](19-vet-seo-thien-tai.md) | Vết sẹo thiên tai trên mặt đất (cháy sém, tro, hố, khe nứt, đá nham, chiến trường cổ, phù sa, đất hạn) lành dần theo năm; cây héo úa gần vùng đặc biệt | `52bb698` |
| 20 | [20-nha-cua-theo-quy-mo.md](20-nha-cua-theo-quy-mo.md) | Nhà cửa theo quy mô (nhà tranh / nhà ngói / nhà lầu / phủ đệ), giếng, miếu, chợ, bảo tháp, hoàng cung, tường thành + tháp canh; tối ưu mở ruộng (116 → 84 ms/năm ở năm 1000) | `509dede` |
| 21 | [21-the-gioi-tuong-tac.md](21-the-gioi-tuong-tac.md) | Vết tích và công trình tác động lên mô phỏng, chiến trường cổ, bí cảnh có art riêng, thiên tài địa bảo xuất thế, tranh đoạt cơ duyên, bấm vào sự kiện để lia camera | `7ee44ce` |
| 22 | [22-bay-va-di-bo.md](22-bay-va-di-bo.md) | Bay theo cảnh giới, đi bộ theo địa hình, tìm đường A* né biển, núi và dung nham (lội được sông), không còn xuyên địa hình | `cc0c901` |
| 23 | [23-hung-thu.md](23-hung-thu.md) | Hung thú xuất thế (sự kiện thế giới), liên minh trảm yêu, 14 loài ma thú vẽ tay theo tứ hung (to, cao, hung dữ), cân bằng | `05e674e` |
| 24 | [24-sinh-luc.md](24-sinh-luc.md) | Sinh lực (máu) thật cho tu sĩ và yêu thú: mất máu khi đấu pháp, độ kiếp, trảm yêu; bị thương thì yếu đi và dưỡng thương; máu hiện trên ô thông tin của mọi sinh vật | `aef8949` |
| 25 | [25-sat-thuong-thien-dao.md](25-sat-thuong-thien-dao.md) | Thiên phạt và thiên tai gây sát thương thật lên mọi sinh vật; yêu thú to theo giai; đi chậm lại nhưng tu vi càng cao càng nhanh; Hóa Thần xé rách hư không | `75b82f7` |
| 26 | [26-quyen-nang-phuc-hoa.md](26-quyen-nang-phuc-hoa.md) | Quyền năng còn thiếu: thả tu sĩ theo cảnh giới, đánh thức hung thú, thả yêu thú theo giai, ban pháp bảo, giáng tâm ma, phế tu vi, thiên tài địa bảo, mở bí cảnh | `b6f55db` |
| 27 | [27-cau-nguyen-thien-menh.md](27-cau-nguyen-thien-menh.md) | Tín ngưỡng và cầu nguyện (đáp lời bằng mưa, ban phúc, thiên phạt; miếu thờ, người có linh căn, tà giáo), Thiên mệnh: 3 mục tiêu mềm thế giới tự gợi ý, Thiên uy | `296639d` |
| 28 | [28-cong-phap-truyen-thua.md](28-cong-phap-truyen-thua.md) | Công pháp 5 phẩm (trần cảnh giới, hợp linh căn, ma công), truyền thừa qua tông môn, bí cảnh, giết người đoạt bảo, tông chủ sáng tạo; thất truyền khi diệt môn và khôi phục sơn môn dưới tên cũ; Ban công pháp | `4b8c316` |
| 29 | [29-tri-thuc-tin-don.md](29-tri-thuc-tin-don.md) | Tri thức và tin đồn: tin loang theo tháng, nhảy theo thương đội và di dân, bị thổi phồng; chỉ tông đã nghe mới tranh đoạt, thám hiểm, trảm yêu; tông nghe muộn đến muộn; nghe tin tông suy yếu thì dòm ngó; vòng tin đồn trên map | `4cc7987` |
| 30 | [30-chinh-tri-gia-toc.md](30-chinh-tri-gia-toc.md) | Chính trị nước phàm nhân (vua, triều đại, bất mãn, khởi nghĩa, tranh ngôi, dẹp yên, đổi triều, cát cứ lập nước mới, thống nhất) và tu tiên gia tộc (đất tổ, con cháu mang họ, báo thù, thế thù, hoàng tộc) | `4d210b8` |
| 31 | [31-toi-uu-di-chuyen-duong-mon.md](31-toi-uu-di-chuyen-duong-mon.md) | Tối ưu di chuyển (bỏ qua tu sĩ bế quan, bộ đệm đường đi: 45–50 → 24 ms/năm); đường mòn và đường đất do người đi bộ và dân làng giẫm mà thành, người đi bộ bám theo đường, vẽ pixel nối liền, bỏ không thì cỏ mọc lại | `5fee811` |
| 32 | [32-the-gioi-hien-hinh.md](32-the-gioi-hien-hinh.md) | Thế giới hiện hình: yêu thú đuổi bắt người qua đường (yếu chạy, ngang đánh, mạnh săn), cơ duyên giơ trên đầu phát sáng, thám hiểm bí cảnh là chuyến đi thật, Thiên cơ hiển lộ kéo tu sĩ tới tranh đoạt, chiêu thức theo hệ công pháp và loài yêu thú (tụ khí, né, đánh bật), thú hoang chạy trốn và sói săn mồi, bế quan trong động phủ | `5dbabac` |
| 33 | [33-viec-ngoai-kia-nhat-ky.md](33-viec-ngoai-kia-nhat-ky.md) | Việc ngoài kia thay du ngoạn ngẫu nhiên: hái linh dược, săn yêu, đi chợ, tuần tra, ngộ đạo, thăm cố hương, có icon trên đầu và cảnh làm việc trên map; nhật ký nhân vật trộn với sự tích trên thẻ; tu sĩ ngoài map 4 → 50 | `b99a4e6` |

Nguyên tắc xuyên suốt (người chơi đã chốt):
- Mọi tên gọi / lore theo **Phàm Nhân Tu Tiên** (`Assets/ThienDao/Resources/Lore/*.json`, mỗi thế giới bốc thăm một phần).
- **Số lượng mô phỏng có thể lớn, trên map chỉ vẽ tượng trưng.**
- Mục tiêu: **một thế giới sống động nhất có thể.**
- **Toàn bộ hình ảnh là pixel art**, kể cả mọi hiệu ứng.
- **Hiệu năng đặt lên đầu** cho mọi tính năng, mà vẫn giữ chất sandbox.
- Tu tiên **cực khó**: Kết Đan là trưởng lão, Nguyên Anh là bá chủ một phương, Hóa Thần là truyền thuyết.
