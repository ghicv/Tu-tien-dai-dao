# 38 · Sơn môn

**Ngày:** 2026-10-11

## Lý do
- Người chơi gửi ảnh một tông môn kiểu Bồng Lai tiên đảo: *"một tông môn nó phải trông như thế này mới đúng chất tu tiên"*. Sau đó: *"làm Sơn môn đi bro"*.
- Theo [`Docs/Vision.md`](../Vision.md), tông môn **ít mà lớn**:
  - mỗi tông là một quần thể có quy hoạch;
  - công trình nào cũng gắn với một chức năng;
  - hưng suy hiện ra bằng kiến trúc;
  - bị diệt thì thành phế tích.

## Hạng tông môn (`Sim/SectComplex.cs`)
- Mỗi năm các tông được xếp theo thực lực:

| Hạng | Bao nhiêu tông | Tối đa công trình |
|---|---|---|
| Thánh địa | 1 tông, hoặc 2 nếu có từ 8 tông trở lên | 17 |
| Đại tông | 5 tông kế tiếp | 12 |
| Tiểu môn phái | còn lại | 5 |

- **Không mất danh vì tụt một bậc:** thánh địa vẫn giữ danh nếu chỉ tụt một hạng, đại tông cũng vậy.
- **Sự kiện:** chỉ báo lần đầu một tông lên tới hạng đó, ví dụ "… được thiên hạ tôn là thánh địa".

## Quần thể xây dần
- **Một bản quy hoạch chung**, trục bắc–nam, đối xứng. Rộng 57 × 40 ô quanh chính điện. Thứ tự xây:

| # | Công trình | # | Công trình |
|---|---|---|---|
| 1 | chính điện, quảng trường, lư hương, bia đá | 10 | linh điền phía đông |
| 2 | sơn môn (đường đá, sư tử đá) | 11 | hộ sơn trận đài phía tây |
| 3 | đệ tử xá phía tây | 12 | hộ sơn trận đài phía đông |
| 4 | đệ tử xá phía đông | 13 | bảo tháp trấn sơn |
| 5 | tường nội môn, có tháp góc | 14 | điện trưởng lão phía tây |
| 6 | Tàng Kinh Các | 15 | điện trưởng lão phía đông |
| 7 | Diễn Võ Trường | 16 | tường ngoại môn |
| 8 | Luyện Đan Phòng | 17 | linh trì có sen, linh thụ |
| 9 | linh điền phía tây | | |

- **Muốn xây tới đâu:** theo điểm = số đệ tử + 4 × số Kết Đan + 15 × số Nguyên Anh trở lên + linh thạch ÷ 300. Cứ 4 điểm được thêm một công trình, nhưng không vượt mức tối đa của hạng.
- **Mỗi năm xây một công trình nếu đủ linh thạch** (40 + 25 × số thứ tự của công trình).
  - Đất phải có ít nhất 80% ô đi được và không thuộc làng khác. Không vừa thì bỏ qua công trình đó.
  - Cây và đá trên chỗ xây bị dọn đi (tường chỉ dọn đường viền).
  - Sự kiện "… xây xong Tàng Kinh Các", có cột sáng.
- **Suy tàn:** điểm tụt quá 2 công trình so với số đang đứng thì công trình xây sau cùng bị bỏ hoang ("cỏ mọc đầy sân"). Khá lên thì sửa công trình hoang trước rồi mới xây mới.
- **Bị diệt hoặc không còn truyền nhân:** sơn môn thành **phế tích** đứng trên map thêm 300 năm.

## Vẽ (`Render/SectArt.cs`, `Render/SectRenderer.cs`)
- **Cả quần thể ghép thành một hình pixel art**, 8 px mỗi ô, nhìn từ trên xuống thấy mặt trước công trình:
  - bậc đá, tường kem, cột đỏ, cửa và song cửa;
  - mái ngói có viền tối, nóc vàng, góc mái cong.
- **Từng công trình:**
  - Tàng Kinh Các và chính điện hai tầng mái; bảo tháp 5 tầng có chóp vàng;
  - đệ tử xá là các dãy nhà nhỏ;
  - Diễn Võ Trường có vòng tròn, giá binh khí, hình nộm rơm;
  - Luyện Đan Phòng có đỉnh đồng và lửa;
  - linh điền thành luống, có linh thảo phát sáng;
  - trận đài là đĩa đá có vòng phù văn bát quái và 8 trụ;
  - linh trì có sen; điện trưởng lão có hoa đào.
- **Màu theo tông:**

| Loại tông | Mái | Tường |
|---|---|---|
| Chính đạo | ngọc bích, nóc vàng | kem, cột đỏ |
| Ma môn | đỏ máu, nóc xương | đá xám, cột đen |
| Thánh địa | chính điện mái vàng | — |
| Tiểu môn phái | đá xám xanh | — |

- **Phế tích và công trình bỏ hoang:** phai màu, tối đi, có lỗ thủng, rêu và cỏ mọc.
- **Hiển thị:**
  - Mỗi tông là một sprite nằm trên mặt đất, dưới người và hiệu ứng. Hình chỉ dựng lại khi sơn môn đổi (xây xong, bỏ hoang, lên hạng), tức vài năm một lần.
  - Đã có sẵn ảnh xuất thử: `Screenshots/sonmon_holy.png`, `sonmon_demonic.png`, `sonmon_ruin.png` (thư mục ảnh chụp, không commit).
- **Sinh hoạt trong sơn môn:**
  - khói nhang trước chính điện;
  - khói luyện đan và thỉnh thoảng một đốm lửa;
  - tia kiếm khí ở diễn võ trường (đệ tử so chiêu);
  - trận đài phát sáng xanh, đỏ rực và dồn dập khi tông đang có chiến sự;
  - đèn nhấp nháy trên đỉnh bảo tháp.
- **Đệ tử và trưởng lão sống bên trong:**
  - đệ tử ở đệ tử xá, diễn võ trường, linh điền, quảng trường, sơn môn, linh trì;
  - trưởng lão ở điện trưởng lão, Tàng Kinh Các, Luyện Đan Phòng, chính điện, bảo tháp.
  - Số người hiện ra tăng theo hạng: thánh địa tới 16 đệ tử và 6 trưởng lão cùng lúc.

## Kết quả
- **Chạy thử 12 năm (thế giới đã có người, seed ThienDao):** 2 thánh địa (Thi Hồn Tông, Thiên Khuyết Bảo), 5 đại tông, 1 tiểu môn phái. Các tông đã xây từ 2 tới 8 công trình.
- **Test EditMode:** 64/64. Test mới `SectsBuildTheirSonMonAndLeaveRuins`:
  - có thánh địa;
  - tông nào cũng có (hoặc đã thử xây) chính điện;
  - số công trình không vượt mức của hạng;
  - có tông đã có sơn môn và đệ tử xá;
  - đệ tử được tìm thấy trong sơn môn;
  - tông mất thì để lại phế tích.
- **Ảnh trong game:** Thiên Khuyết Bảo (đại tông) với chính điện, Tàng Kinh Các, Luyện Đan Phòng, diễn võ trường, hai dãy đệ tử xá, sơn môn, tường nội môn; đệ tử đi lại trên quảng trường. Thi Hồn Tông (ma môn) mái đỏ.
- **Chưa làm:**
  - quần thể chưa mọc theo địa thế (vẫn là một bản quy hoạch chung, chỗ nào không xây được thì bỏ qua);
  - nhà cũ của làng hầu cận vẫn có thể ló ra ở chỗ chưa có công trình;
  - thời hồng hoang phải đợi tới khi có tông môn đầu tiên mới thấy sơn môn.
