# 31 · Tối ưu di chuyển, đường mòn và đường đất do người đi mà thành

**Ngày:** 2026-10-08

## Lý do
- Người chơi:
  - *"tối ưu hệ di chuyển đi bro"*
  - *"những tu sĩ đang bế quan thì coi như xóa khỏi map, k update nữa cho đỡ nặng"*
  - *"cell nào được loài người đi bộ qua nhiều thì lâu lâu sẽ thành đường đi, khi đã hoàn thành đường thì những loài người đi bộ sẽ đi theo tuyến đường đó… có visual rõ ràng cho đường → đường đất"*
- Kèm một quy tắc mới, áp dụng cho mọi hệ (đã lưu vào bộ nhớ): *"các hoạt động phải có visual rõ ràng chứ k chỉ là mỗi giả lập và tính toán, phải để người chơi nhìn thấy được"*.

## Tối ưu hệ Di chuyển

### Đo trước khi sửa (năm 300–350, có gắn bộ đếm tạm)
| Loại | ms/năm | Lượt/ngày |
|---|---|---|
| Tu sĩ "đi bộ" | 38,6 | 731 (chỉ 56 người thật sự đang đi) |
| Thương đội | 12,8 | 7 |
| Yêu thú | 2,1 | 29 |
| Tu sĩ bay | 0,8 | 15 |
| Trong đó A\* | 13,9 | 39 lần tìm đường/năm |

- Hơn 90% thời gian dành cho tu sĩ **đang bế quan ở nhà**, vẫn bị xét đường đi mỗi ngày.

### Đã sửa
- **Tu sĩ không trên đường thì không được cập nhật di chuyển** (đúng ý người chơi):
  - Bế quan, hoặc đang ở yên chỗ du ngoạn tới, thì bỏ qua hẳn vòng di chuyển mỗi ngày.
  - Họ vẫn tu luyện, đột phá, sống theo tháng như cũ; chỉ phần đi lại là không chạy.
- **Bộ nhớ đệm đường đi** (`NavSystem._cache`):
  - Tuyến A\* giữa cùng một cặp nút được nhớ lại. Thương đội giữa hai chợ, di dân từ một thị trấn và đường làng đều đi lại các tuyến quen, nên không phải tìm lại từ đầu.
  - Bộ đệm chỉ bị xóa khi đất đổi từ đi được sang không đi được hoặc ngược lại, giữ tối đa 4.096 tuyến.
  - Bộ đệm được lưu cùng file save, nên chơi tiếp sau khi tải vẫn đi đúng những con đường như ván chưa dừng.

### Sau khi sửa
- **Đo lại năm 300–350:**
  - Hệ Di chuyển còn 32 ms/năm (trước là 64, cả hai số đều tính chi phí của bộ đo).
  - Tu sĩ đi bộ còn 4,4 ms (6 lượt/ngày); A\* còn 4,9 ms; 27/39 lần tìm đường mỗi năm dùng lại bộ đệm.
- **Đo 1000 năm (không bộ đo):**
  - Hệ Di chuyển năm 1000 còn **23,6 ms/năm**, trước khoảng 45–50 ms.
  - Tổng mô phỏng còn **104,6 ms/năm**, trước 112 ms.

## Đường mòn và đường đất (`Sim/PathSystem.cs`)

### Luật
- **Ai giẫm đường:** người đi bộ bước sang ô mới thì giẫm ô đó một lượt. Tu sĩ Luyện Khí tính 1 lượt; đoàn di dân và thương đội tính 3 (đông người, có xe).
- **Dân làng đi lại thường ngày:**
  - Phàm nhân trong làng là con số dân, không đi lại thật. Vì vậy mỗi năm, dân mỗi làng giẫm tuyến tới **2 làng gần nhất** (trong vòng 70 ô), giẫm nhiều hơn khi làng đông (1–8 lượt).
  - Trấn và thành còn đi về **kinh đô** của nước mình: quan lộ, +2 lượt.
  - Tuyến đi thẳng nếu đất cho phép, không thì theo A\* (có bộ đệm). Mỗi tháng chỉ xử lý 1/12 số làng.
- **Ngưỡng:** 8 lượt thì thành **đường mòn** (cỏ rạp, đất lộ ra); 30 lượt thì thành **đường đất** (đất nện).
- **Bỏ không thì mất:** mỗi năm số lượt giẫm mất 15%. Đường đất tụt dưới 7 lượt thì xuống đường mòn; đường mòn tụt dưới 2 thì thành cỏ. Một tuyến bị bỏ quên sẽ biến mất sau một hai chục năm.
- **Không mọc trên:** ruộng, nước, dung nham.

### Người đi bộ bám theo đường
- **Đi nhanh hơn:** đường đất ×1,6, đường mòn ×1,3.
- **Nút có đường thì rẻ:** một nút A\* (4×4 ô) có đường chạy qua tính theo tốc độ đi trên đường, không phải tốc độ trên đất xấu bên cạnh. Nhờ vậy A\* ưu tiên đường.
- **Điểm đi qua nằm trên đường:** điểm đi qua của nút được đặt trên chính ô đường.
- **Không cắt thẳng qua đồng:** khi rút gọn tuyến, các nút có đường được giữ lại, nên người đi bộ bám theo đường.
- **Vòng tự củng cố:** đường được đi nhiều thì càng mòn, nên mạng đường giữa các làng tự vẽ ra, và đổi theo khi làng mọc lên hay bị bỏ hoang.

### Hình ảnh (pixel art)
- **Đường đất:** dải đất nện rộng khoảng 5 px, có hai vệt bánh xe tối hơn chạy theo hướng đường, thỉnh thoảng có sỏi. Mép đường đan ô cờ với cỏ, không nhòe.
- **Đường mòn:** dải 2–3 px, cỏ mòn lộ đất lốm đốm, còn vài túm cỏ.
- **Nối liền:** đường nối với các ô có đường quanh nó theo cả 4 hướng thẳng và 4 hướng chéo, nên đường đi chéo vẫn liền thành bậc thang.
- **Bản đồ thu nhỏ** (1 điểm ảnh một ô): đường đất là vệt màu đất, đường mòn nhạt hơn.
- **Cập nhật map theo lô:** ô đường đổi thì được gom theo chunk 32×32 và báo cho map và hệ tìm đường mỗi tháng một lần. Renderer vẽ lại chunk trong ngân sách thời gian mỗi khung hình, nên không giật.
- **Thông tin ô:** "đường đất (n lượt qua)" hoặc "đường mòn (n/30 lượt để thành đường đất)". Bảng thống kê có số ô đường đất và đường mòn.
- **Ảnh chụp:** sau 60 năm có 1.495 ô đường mòn và 3.905 ô đường đất. Đường đất uốn từ xóm nhà tranh vào tận cổng thành Bắc Lương Kinh.

## Kết quả
- **Test EditMode:** 62/62. Test mới `WalkersWearTrailsIntoRoadsAndGrassTakesThemBack`:
  - Giẫm đủ thì thành đường mòn và đi nhanh hơn; giẫm thêm thì thành đường đất.
  - Bỏ không 30 năm thì thành cỏ.
  - Thế giới thật sau 40 năm tự vẽ ra hơn 50 ô đường.
- **Hiệu năng, đo 1000 năm (seed ThienDao):**

| Bản | Di chuyển năm 1000 (ms/năm) | Tổng mô phỏng năm 1000 (ms/năm) |
|---|---|---|
| Trước tối ưu (devlog 30) | 44–50 | 112 |
| Bỏ qua tu sĩ bế quan, bộ đệm đường đi | 23,6 | 104,6 |
| Thêm đường mòn, đường đất (bản đầu) | 32,6 | 116 |
| Chỉ báo cho map, chỉ quét làng còn sống, chỉ tính lại nút có đường đổi (bản cuối) | **28,4** | **112** |

- **Chi phí đường mòn và đường đất:** khoảng 1–4 ms/năm, đổi lại có cả một mạng đường tự vẽ ra.
- **Hai chỗ thừa đã sửa ở bản đầu:**
  - Mỗi ô đường mới làm hệ cỏ và hệ làng tính lại. Nay có sự kiện riêng `WorldData.PathsChanged`, chỉ renderer nghe.
  - Hệ tìm đường tính lại cả chunk 32×32. Nay chỉ tính lại đúng nút 4×4 có ô thay đổi.
- **Cân bằng:** phàm nhân 31,1 nghìn, tu sĩ còn sống 816 ở năm 1000, không lệch so với devlog 30.
