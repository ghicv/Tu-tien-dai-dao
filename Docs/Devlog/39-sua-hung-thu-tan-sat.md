# 39 · Sửa lỗi: hung thú "không tấn công" dân làng

**Ngày:** 2026-10-11

## Lỗi
- Người chơi: *"có lỗi hung thú k tấn công dân làng ???"*.
- Dựng lại lỗi (thế giới đã có người, thả hung thú thất giai ở nhiều chỗ khác nhau): trong mô phỏng hung thú **vẫn tàn sát**, cả 8 con thử đều tàn sát được làng trong năm đầu. Nhưng nhìn trên map thì như nó không làm gì. Có ba nguyên nhân:
  1. **Chỉ xét mỗi tháng một lần.** Hung thú chỉ kiểm tra "đã tới làng chưa" vào đầu tháng (`RampageStep` nằm trong `MonthlyStep`). Tới nơi giữa tháng thì nó đứng sát làng chờ.
  2. **Tàn sát xong trong một khoảnh khắc.** Một dòng sự kiện là xong, rồi nó nằm nghỉ 2–5 tháng ngay giữa làng. Trong lúc đó dân làng trên map vẫn đi lại quanh nó như không có gì.
  3. **Kẹt chướng ngại** (lỗi do devlog 36):
     - Từ khi yêu thú chuyển sang đi thẳng không tìm đường, hung thú gặp sông núi chắn là đứng im.
     - Hàm kiểm tra "kẹt đường" chỉ áp dụng cho kiểu đi tìm đường cũ, nên nó không bao giờ bỏ mục tiêu.
     - Kết quả: có con "no say quay về ngủ" sau 0 lần tàn sát. Lịch sử chạy thử ở devlog 36 có đúng dòng này.

## Sửa
- **Hung thú xét mỗi ngày** (`BeastSystem.RavageDaily`, gọi từ bước hằng ngày):
  - Tới làng (trong 5 ô) là xông vào ngay trong ngày: *"… xông vào Gia Nguyên Thôn! Dân chúng kêu khóc chạy trốn."*
  - Nghỉ xong mà chưa có mục tiêu thì cứ 5 ngày chọn làng một lần, không đợi tới đầu tháng.
- **Tàn sát kéo dài 5–9 ngày:**
  - Mỗi ngày giết một phần số người phải chết. Hung thú đi từ nhà này sang nhà khác trong làng, có khói bụi và vùng bị thương ngay chỗ nó (cảnh trên map).
  - Hết thời gian mới tổng kết: *"… tàn sát Gia Nguyên Thôn: 5 người chết, 2 nhà sụp đổ"*, để lại vết đất, rồi gọi liên minh trảm yêu như cũ.
- **Đánh mạnh gấp đôi:** tỉ lệ chết = 6% × (giai − 4) (trước 3%). Một con thất giai giết khoảng 1/5 dân một làng không tường; tường thành vẫn giảm còn 40%.
- **Hung thú tìm đường lại** (đi theo `Walk` và hệ tìm đường) để vòng qua sông núi; kẹt thật thì bỏ làng đó, chọn làng khác. Yêu thú thường vẫn đi thẳng cho nhẹ.
- **Dân làng trên map biết sợ** (`UnitRenderer.Flee`):
  - Thấy bất kỳ yêu thú nào trong vòng 6 ô thì chạy thẳng ra xa, nhanh gấp 2,5 lần, chân chạy nhanh, tung bụi.
  - Hung thú đuổi kịp (trong 1,2 ô) thì người đó bị xé xác: vệt cào, máu văng.
  - Đây chỉ là hình ảnh; số người chết thật vẫn do mô phỏng tính.

## Kết quả
- **Thử lại:** hung thú thả cách Gia Nguyên Thôn (40 dân) 30 ô thì ngày 26 xông vào làng, cắn xé tới ngày 33, giết 5 người, sập 2 nhà.
- **Test EditMode:** 65/65. Test hồi quy mới `HungThuTearsThroughTheTownOverDays`:
  - trong 3 tháng hung thú phải vào làng;
  - cuộc tàn sát kéo dài từ 4 ngày trở lên;
  - có người chết.
