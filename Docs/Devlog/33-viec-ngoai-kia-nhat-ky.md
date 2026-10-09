# 33 · Việc ngoài kia và nhật ký: tu sĩ sống có mục đích, thấy được trên map

**Ngày:** 2026-10-09

## Lý do
- Người chơi chơi thử 5 phút:
  - *"game thực sự rất chán"*
  - *"nhìn nó k giống một thế giới tự vận hành ấy, trông nó chả có gì"*
  - *"nhìn một sinh vật hoặc nhân vật trong game, chả có câu chuyện gì, chỉ chạy loanh quanh 1 chỗ, rất nhàm"*
- Đo 5 phút đầu ở tốc độ x1 (5 năm game, seed ThienDao):
  - Có 124 tu sĩ còn sống, nhưng trung bình chỉ **4 người** cả thế giới đang ở ngoài map. Còn lại đều đang bế quan, bị ẩn.
  - Cả thế giới chỉ có 46 sự kiện.
  - Ai ra ngoài cũng chỉ là "du ngoạn" tới một điểm ngẫu nhiên, rồi đứng im 20–90 ngày.

## Việc ngoài kia (`Sim/Errands.cs`, mới)
Thay chuyến du ngoạn ngẫu nhiên. Mỗi việc là một chuyện nhỏ có đầu có cuối, diễn ra từng ngày trên map:

| Việc | Ai làm | Đi đâu | Ở đó làm gì (thấy được) | Kết quả |
|---|---|---|---|---|
| **Hái linh dược** | mọi người | 1 trong 5 linh dược viên cố định quanh nhà, chọn chỗ linh khí cao nhất | cúi hái, hạt xanh và lá bay lên | tìm được linh thảo (giơ trên đầu); hiếm khi gặp thảo dược ngàn năm (sự kiện); hoặc tay trắng. Có người mang lên trấn bán. |
| **Săn yêu** | Trúc Cơ trở lên (ma tu hay đi) | con yêu thú gần nhất yếu hơn mình | bám theo nó, mỗi ngày chỉnh lại hướng | thấy nó thì xông vào: đuổi bắt và đánh nhau (devlog 32) |
| **Đi chợ** | ai không phải đệ tử Luyện Khí | trấn gần nhất (từ trấn trở lên mới có chợ) | dạo chợ, linh thạch lấp lánh | đủ tiền thì mua đan đột phá (giơ trên đầu); không thì buôn bán, nghe ngóng. Tiêu tiền giúp trấn có thêm lương thực. |
| **Tuần tra** | đệ tử tông môn, nhất là Luyện Khí | hai thôn quanh tông môn, thôn này rồi thôn kia | đi qua làng | ghi vào nhật ký; dọc đường có thể bị yêu thú đuổi |
| **Ngộ đạo** | Trúc Cơ trở lên | dấu tích cũ gần nhất (ma tu tìm chiến trường cổ), không có thì đỉnh núi linh khí cao | tĩnh tọa, hạt sáng màu cảnh giới hút vào, vòng sáng lan ra | tâm cảnh và ngộ tính tăng; có lúc đốn ngộ, tu vi tăng vọt (sự kiện) |
| **Thăm cố hương** | ai còn nhớ làng sinh ra mình | làng đó | ở lại vài ngày | để lại linh thạch, làng có thêm lương thực; làng đã mất thì chỉ thấy tro tàn |

- **Trên đầu có icon nhỏ** cho biết đang làm gì: lá (hái thuốc), kiếm (săn), linh thạch (đi chợ), cờ (tuần tra), sen (ngộ đạo), mái nhà (thăm nhà).
- **Linh dược viên cố định:** cùng một người luôn đi tới cùng mấy chỗ. Vì vậy đường tới đó mòn thành đường mòn (devlog 31), và tuyến đường được nhớ lại, không phải tìm lại từ đầu.
- **Việc bị ngắt:** gặp chuyện lớn hơn thì việc dừng lại (bị gọi ra trận, đi báo thù, bị yêu thú đuổi, đi tranh bí cảnh). Một chặng đi 150 ngày chưa tới thì coi như lạc đường, quay về.
- **Tần suất mỗi tháng ở nhà:** đệ tử Luyện Khí 1/5, Trúc Cơ 1/4, Kết Đan trở lên 1/8. Trước đây là 1/60.
- **Bước hằng ngày** chỉ duyệt những người đang làm việc, theo thứ tự Index, nên lưu rồi tải lại vẫn chạy ra đúng như cũ.

## Nhật ký
- Mỗi tu sĩ giữ 6 việc nhỏ gần nhất. Lời kể có vài cách nói khác nhau, kèm địa danh chỉ hướng: "ở Hoa Khê Thôn", "gần…", "phía đông nam Thanh Thủy Thôn".
- Thẻ nhân vật trộn nhật ký với sự tích trong sử sách (chữ vàng), xếp theo thời gian, ghi năm và tháng. Bấm vào ai cũng đọc được chuyện của người đó, ví dụ:

> N3·T7 Rời sơn môn, đi hái linh dược phía đông nam Thanh Thủy Thôn.
> N3·T8 Hái được mấy cây Bích Ngọc Trúc.
> N5·T4 Tâm cảnh chững lại, tìm tới đỉnh núi phía đông nam Thanh Thủy Thôn tĩnh tọa ngộ đạo.
> N5·T5 Đốn ngộ! Tu vi tăng vọt.

- Dòng trạng thái giờ ghi đúng việc đang làm: "đang hái linh dược…", "đang tuần tra tới…", "đang chạy trốn yêu thú", "đang giao chiến với yêu thú".

## Kết quả
- **Đo lại 5 phút đầu ở tốc độ x1** (5 năm game):

| | Trước | Sau |
|---|---|---|
| Tu sĩ đang ở ngoài map (trung bình) | 4 | **50** |
| Đang làm việc (trung bình) | 0 | 21 (tuần tra 12, hái thuốc 7, đi chợ 1, ngộ đạo 1) |
| Sự kiện trong 5 năm | 46 | 118 |

- **Test EditMode:** 64/64. Test mới `CultivatorsGoOutOnErrandsAndKeepADiary`: trong năm đầu luôn có từ 10 người trở lên đang làm việc, và có nhật ký.
- Lần này không đo cân bằng 1000 năm (người chơi dặn chỉ đo khi được yêu cầu).
- **Việc tiếp theo:**
  - đối chiếu và clone các chức năng của WorldBox sang bản tu tiên, bỏ những tính năng thừa;
  - mọi sinh vật phản ứng khi Thiên Đạo ra tay;
  - quyền năng chi tiết hơn;
  - phàm nhân đi lại thật.
