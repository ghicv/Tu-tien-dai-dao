# 04 · M3 — Tu tiên lõi

**Ngày:** 2026-10-06 · **Commit:** xem `git log` ("M3: tu tiên lõi…")

## Mục tiêu (GDD)
Linh căn, cảnh giới Luyện Khí → Nguyên Anh (và Hóa Thần), tu luyện hút linh khí, đột phá, tẩu hỏa nhập ma, thọ nguyên. Kiểm chứng: tu sĩ tự tìm phúc địa, có người đột phá, có người chết già.

## Yêu cầu thêm của người chơi trong lúc làm
- Tu sĩ mặc **skin khác phàm nhân**: đạo bào, búi tóc cài trâm, màu áo theo cảnh giới.
- **Từ Trúc Cơ trở lên ngự kiếm phi hành.**
- Ở tông môn chỉ vẽ **vài đệ tử cấp thấp và vài trưởng lão**. Đại năng, tán tu **thỉnh thoảng xuất hiện lác đác** khi đang du ngoạn, lịch luyện, tìm tài nguyên.
- **Click vào nhân vật thì mở popup thông tin.**
- Thứ bậc theo truyện: **Kết Đan là trưởng lão, Nguyên Anh là tông chủ và bá chủ một phương, Hóa Thần cực hiếm. Tu tiên cực khó.**

## Đã làm
- `Sim/Realms.cs`: bảng cảnh giới. Luyện Khí 13 tầng; các cảnh giới sau chia sơ kỳ, trung kỳ, hậu kỳ, đại viên mãn. Bảng có:
  - Thọ nguyên: 120 / 220 / 500 / 1000 / 2000 năm.
  - Linh khí cần, mức hút, điểm tu vi mỗi tiểu cảnh, tỉ lệ đột phá, thời gian chờ giữa các lần thử.
- Linh căn: 5 hệ + biến dị; Thiên / Dị / Chân / Ngụy linh căn với tốc độ ×4 / ×5 / ×2–1 / ×0,5–0,3.
- `Sim/CultivationSystem.cs`:
  - Khởi tạo mỗi tông: tông chủ Kết Đan hậu kỳ hoặc đại viên mãn, 1–3 trưởng lão Kết Đan, 3–7 đệ tử nội môn Trúc Cơ, 10–20 đệ tử ngoại môn Luyện Khí. Có khoảng 50% khả năng có một lão tổ Nguyên Anh ở tông có linh khí đậm nhất. Thêm 20 tán tu.
  - Mỗi tháng: tu luyện = linh căn × linh khí tại chỗ × ngộ tính × tâm cảnh, đồng thời hút linh khí tại chỗ. Ma tu tu nhanh gấp 1,5.
  - Đột phá:
    - Trúc Cơ 10%, cộng 25% nếu được tông ban Trúc Cơ Đan.
    - Kết Đan 4%; Nguyên Anh 1,5% kèm thiên kiếp.
    - Hóa Thần 0,3%, chỉ được thử ở nơi linh khí từ 8.000 trở lên, kèm đại thiên kiếp.
    - Từ Trúc Cơ trở lên, mỗi lần thử cách nhau 3 năm. Gần hết thọ nguyên thì liều thử dù tu vi chưa đủ.
  - Thất bại thì giảm tu vi và tâm cảnh; có thể tẩu hỏa nhập ma: chết, tụt cảnh giới, hoặc sa vào ma đạo.
  - Mỗi năm: trẻ 10 tuổi có 1% (cộng thêm theo linh khí) thức tỉnh linh căn, rời làng vào tông gần nhất trong bán kính 260 ô hoặc làm tán tu. Tu sĩ thấy linh khí nơi ở không đủ thì đi tìm động phủ mới.
  - Ra ngoài: mỗi tháng có khoảng 1/60 cơ hội; đi du ngoạn (tăng tâm cảnh), lịch luyện (tăng ngộ tính) hoặc tìm linh thảo (25% cơ hội tăng tu vi). Ở lại 20–90 ngày rồi trở về.
  - Vai trò tự động: người mạnh nhất là tông chủ, có sự kiện kế nhiệm; Kết Đan là trưởng lão; Trúc Cơ là đệ tử nội môn; Luyện Khí là đệ tử ngoại môn.
- `Sim/EventLog.cs`: bảng tin sự kiện có độ quan trọng; là nền cho HistoryLog ở M5.
- Quyền năng mới: **Ban linh căn**, **Ban cơ duyên**, **Thiên phạt**.
- Render:
  - Sprite tu sĩ 5 màu theo cảnh giới, thêm màu ma tu; phi kiếm phát sáng; hào quang từ Kết Đan.
  - Ở tông môn chỉ vẽ tối đa 6 đệ tử và 2 trưởng lão đại diện. Tu sĩ chỉ hiện khi đang ra ngoài.
- UI: Bảng cường giả, bảng tin sự kiện, thống kê tu sĩ theo cảnh giới, nhãn tên từ Kết Đan.
- **Popup nhân vật:** dùng công cụ Xem, click vào tu sĩ (hoặc làng, tông môn). Hiện thanh tu vi, thanh tuổi, linh căn, ngộ tính, tâm cảnh, khí vận, việc đang làm, sự kiện liên quan, nút Theo dõi.

## Điều tiết dân số (sửa thiếu sót của M2)
Dân phàm nhân tăng theo cấp số nhân (1.380 lên 67.820 người sau 150 năm), kéo số tu sĩ tăng theo. Đã thêm:
- Sinh nở giảm dần khi làng tiến tới khoảng 800 người.
- Ôn dịch: mỗi làng 1,5% mỗi năm, chết 10–25% dân.
- Trần 150 làng toàn thế giới.

## Lỗi gặp và cách cân bằng
- Lần chạy đầu: 2.094 tu sĩ và 308 Kết Đan sau 100 năm. Đã tăng điểm tu vi cần cho mỗi tiểu cảnh 2,5–3 lần, hạ tỉ lệ đột phá, thêm thời gian chờ 3 năm, hạ tỉ lệ có linh căn xuống 1%.
- Hàm tìm tu sĩ gần nhất trả về cả người đang bế quan (không hiện) → thêm `FindShownNear`.

## Kiểm chứng (seed `ThienDao`, 200 năm)
| Năm | Tu sĩ | Luyện Khí | Trúc Cơ | Kết Đan | Nguyên Anh | Hóa Thần | Đang hiện trên map | Dân phàm |
|---|---|---|---|---|---|---|---|---|
| 1 | 184 | 133 | 32 | 18 | 1 | 0 | 0 | 1.380 |
| 81 | 315 | 163 | 118 | 33 | 1 | 0 | 19 | 16.564 |
| 201 | 852 | 415 | 365 | 69 | 3 | 0 | 33 | 25.780 |

- 0,16 ms/tick. 13/13 test pass (thêm 3 test cho M3).
- Ảnh: `Docs/m3_popup.png`, `Docs/m3_flying.png`, `Docs/m3_sect.png`.

## Tiếp theo
M4: thế lực. Tông môn như một tổ chức: tài nguyên, lãnh thổ, chiếm linh mạch, ngoại giao, tranh đoạt địa bàn, đệ tử phản bội lập tông mới.
