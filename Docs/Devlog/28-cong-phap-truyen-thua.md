# 28 · Công pháp và truyền thừa

**Ngày:** 2026-10-08

## Lý do
- Người chơi: *"oke làm công pháp và truyền thừa đi bro"*.
- Đây là mục ưu tiên 1 trong `Docs/WorldRuleSystem.md` (luật N2): tu luyện còn thiếu trục **Technique** mà `WorldRules.md` §5 yêu cầu (Talent + Environment + Resources + **Technique** + Time + Risk).
- Truyền thừa (§12) mới chỉ có pháp bảo, chưa có công pháp.

## Đã làm (`Sim/TechniqueSystem.cs`)

### Công pháp
- **Mỗi bộ công pháp có:**
  - Tên (đặt theo phong cách Phàm Nhân Tu Tiên, `Resources/Lore/items.json`).
  - **Phẩm** từ 1 đến 5: phàm phẩm, hạ phẩm, trung phẩm, thượng phẩm, cực phẩm.
  - **Hệ:** vạn năng, hoặc một trong tám hệ (Kim … Băng).
  - Có thể là **ma công**.
- **Trần cảnh giới:** đến trần thì dù thiên tài cũng không đột phá được, phải tìm công pháp cao hơn.

| Phẩm | Tu được tới | Tốc độ tu luyện |
|---|---|---|
| Phàm phẩm (Dẫn Khí Quyết, mặc định của tán tu) | Trúc Cơ | ×0,8 |
| Hạ phẩm | Kết Đan | ×0,9 |
| Trung phẩm | Nguyên Anh | ×1 |
| Thượng phẩm | Hóa Thần | ×1,1 |
| Cực phẩm | Hóa Thần | ×1,25 |

- **Hợp linh căn:** hệ của công pháp nằm trong linh căn thì ×1,15, không hợp thì ×0,8; công pháp vạn năng ×1. Trung bình cả thế giới khoảng ×1: công pháp quyết định ai đi được tới đâu, không làm tăng số người.
- **Ma công:** dễ tẩu hỏa hơn (×1,3).

### Truyền thừa: công pháp đến từ đâu, đi về đâu

| Nguồn | Luật |
|---|---|
| **Tông môn** | Mỗi tông có một trấn phái công pháp (khởi đầu: hạ, trung hoặc thượng phẩm). Đệ tử vào tông thì học, nếu nó tốt hơn công pháp đang có |
| **Tông chủ sáng tạo** | Mỗi năm, tông chủ từ Kết Đan trở lên có ngộ tính cao có thể bế quan ngộ đạo, viết công pháp mới (Kết Đan: trung phẩm, Nguyên Anh: thượng phẩm, Hóa Thần: cực phẩm) và lập làm trấn phái công pháp |
| **Tán tu tự ngộ** | Tán tu bị bình cảnh công pháp vây khốn, ngộ tính cao, có thể tự ngộ ra công pháp vượt qua bình cảnh |
| **Động phủ người chết** | Kết Đan trở lên chết thì ngọc giản công pháp nằm lại trong động phủ |
| **Phế tích tông bị diệt** | Trấn phái công pháp nằm trong phế tích |
| **Cổ mộ, thượng cổ di tích** | Công pháp thượng cổ đã thất truyền, trung phẩm đến cực phẩm; cổ mộ có thể chứa ma công |
| **Thám hiểm bí cảnh** | Ai vào được thì học nếu tốt hơn, rồi dâng về tông nếu tốt hơn trấn phái công pháp. Bí cảnh có công pháp từ trung phẩm trở lên đáng để các thế lực tranh đoạt |
| **Giết người đoạt bảo** | Kẻ giết lấy luôn ngọc giản trong túi trữ vật của nạn nhân |
| **Ma công** | Người chính đạo chỉ học ma công khi dã tâm lớn hơn lương tâm, và từ đó sa vào ma đạo |
| **Thiên Đạo** | **Ban công pháp:** một bộ cực phẩm hợp linh căn; tông của người đó lập làm trấn phái công pháp |

### Thất truyền và phục hưng
- **Khi một tông bị diệt:**
  - Kẻ thắng có thể **đoạt Tàng Kinh Các**, lấy công pháp của tông bị diệt nếu tốt hơn công pháp của mình.
  - Không thì công pháp **thất truyền** khi không còn ai sống biết nó; phế tích vẫn giữ ngọc giản.
- **Phục hưng:**
  - Tán tu mang công pháp của một tông đã diệt sẽ dễ khai tông hơn.
  - Khi khai tông, họ **khôi phục sơn môn dưới tên cũ**, và mối thù với kẻ đã diệt tông cũ **sống lại** (thiện cảm −60).
- **Tông chia tách:** tông ly khai mang theo công pháp của tông mẹ.
- **Thèm công pháp:** hai tông giáp ranh chênh nhau từ 2 phẩm trở lên thì thiện cảm giảm, vì tông kém thèm Tàng Kinh Các của tông bên cạnh.
- **Bình cảnh công pháp:** tu sĩ chạm trần thì đi tìm bí cảnh với xác suất gấp 2,5 lần.

**Chuỗi phản ứng mẫu:** tông A diệt tông B → công pháp của B thất truyền vào phế tích → một tán tu chạm trần công pháp, lùng bí cảnh, nhặt được nó → khôi phục B dưới tên cũ → thù với A sống lại → chiến tranh.

### Hiển thị
- **Thẻ tu sĩ:** chip ngọc giản ghi tên công pháp. Tooltip ghi phẩm, hệ, hợp linh căn hay không, tốc độ, tu được tới đâu, có phải trấn phái công pháp không. Chip đỏ khi đã chạm trần công pháp.
- **Thẻ tông môn:** chip trấn phái công pháp.
- **Bí cảnh:** dòng "ngọc giản … (thất truyền)".
- **Sử sách:** sáng tạo công pháp, đoạt ngọc giản, đoạt Tàng Kinh Các, công pháp thất truyền tái hiện, khôi phục sơn môn.
- **Biểu tượng pixel mới:** bó ngọc giản buộc dây đỏ.
- **Quyền năng mới** Ban công pháp (tab Phúc/Họa và hàng nút trên thẻ, giờ 9 nút cỡ 40 px).

### Kỹ thuật
- Lore công pháp lấy nguyên cả kho, không bốc thăm, nên không làm lệch các lượt bốc thăm khác của thế giới.
- Tên mỗi công pháp chỉ dùng một lần; khi kho tên cạn thì đặt thành quyển sau ("… đệ nhị quyển").
- RNG riêng, sinh theo chỉ số của công pháp.
- Lưu và tải tự động qua graph serializer. Công pháp nằm trong state hash.

## Kết quả
- **Test EditMode:** 58/58. Test mới `MethodsCapTheClimbAndFallenSectsRiseAgainFromTheirLegacy`:
  - Tông nào cũng có công pháp; đệ tử học công pháp của tông.
  - Phàm phẩm dừng ở Trúc Cơ.
  - Ban công pháp cho cực phẩm.
  - Diệt môn thì công pháp thất truyền; người mang nó khôi phục tông dưới tên cũ cùng công pháp cũ.
- **Cân bằng** (cùng seed, so với bản tắt công pháp):
  - Lần thử đầu, hệ số tốc độ ×0,85–1,35 làm Kết Đan nhiều gấp đôi ở năm 600 (56 so với 23), trái nguyên tắc "tu tiên cực khó". Mình hạ hệ số xuống ×0,8–1,25.
  - Sau khi hạ: năm 600 có 26 Kết Đan (bản tắt công pháp 23), 0 Nguyên Anh (bên kia cũng 0). Trúc Cơ nhiều hơn (317 so với 193), vì công pháp vạn năng hoặc hợp hệ giúp Luyện Khí đều tay hơn.
  - **Thế giới tự phục hưng sơn môn 8 lần trong 600 năm** dù người chơi không can thiệp.
- **1000 năm (seed ThienDao):**
  - Phàm nhân 34,3 nghìn, tu sĩ còn sống 987 (Trúc Cơ 460, Kết Đan 62, Nguyên Anh 1).
  - 62 bộ công pháp từng xuất hiện, 2 bộ đang thất truyền.
  - 122 ms/năm (124 ms ở devlog 27). Hệ Công pháp chỉ chạy theo năm và tra cứu O(1), chi phí nằm trong hệ Tu sĩ (22 ms).
- **Còn để ý:** cảnh giới cao vẫn rất hiếm (Nguyên Anh 0–2, chưa có Hóa Thần trong 1000 năm), cả khi tắt công pháp. Nếu muốn thế giới có "bá chủ một phương" thường xuyên hơn thì nên chỉnh tỉ lệ đột phá Nguyên Anh riêng.
