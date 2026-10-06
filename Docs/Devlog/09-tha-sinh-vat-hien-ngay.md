# 09 · Thả sinh vật là hiện ngay

**Ngày:** 2026-10-06

## Lý do
Người chơi: *"Khi spawn động vật hay cái gì thì phải cho nó hiển thị luôn"*.

## Nguyên nhân
- **Thú hoang** là quần thể theo vùng 64×64 ([03](03-m2-thu-hoang-quan-the.md)). Thả thú chỉ cộng vào số lượng của vùng.
  - Map vẽ tượng trưng: mỗi hình đại diện cho 20 hươu, 30 thỏ hoặc 3 sói, tối đa 2–3 hình mỗi loài một vùng, và vị trí đặt ngẫu nhiên.
  - Vì vậy vùng đã đủ hình thì thả thêm không thấy gì. Nếu có hình mới thì nó cũng xuất hiện ở chỗ ngẫu nhiên, không phải chỗ click.
- **Ban linh căn** ở làng không có tông môn trong bán kính: đứa trẻ thành tán tu "bế quan" ngay trong làng, nên không hiện trên map.

## Đã làm
- **Hiện thú ngay khi thả:**
  - `WorldBrush.Spawned`: mỗi lần thả, brush báo cho tầng hiển thị loài và vị trí.
  - `UnitRenderer.ShowSpawn` đặt **một hình thú ngay tại điểm click**, đánh dấu `Pinned` trong 60 giây thực.
  - Hình được ghim vượt giới hạn hình của vùng, tối đa thêm 8 hình mỗi loài mỗi vùng. Giữ chuột kéo brush thì hình cũ nhất được dời tới chỗ mới.
  - Hết 60 giây, hình trở lại thành hình thường và bị giới hạn như cũ. Nếu loài đó trong vùng chết hết (sói ăn, đói) thì hình bị xóa ngay. Thả xuống nước thì không hiện gì, khớp với mô phỏng (thú rơi xuống biển là mất).
  - Hiện cả khi đang tạm dừng, vì lệnh được áp dụng trước khi vẽ frame.
- **Các công cụ khác đã hiện ngay từ trước:** nhà, tông môn, cây, lập làng (nhà dựng ngay, dân làng hiện khi zoom đủ gần), địa hình, linh mạch, thiên phạt.

## Thiên Đạo tác động đúng người được chọn
Người chơi: *"ban linh căn là ban trực tiếp vào nhân vật đang chọn nhé, chứ k phải là tạo ra đứa trẻ có linh căn"*.

- **Cách nhắm:**
  - Công cụ Ban linh căn, Ban cơ duyên, Thiên phạt nhắm theo cùng bộ chọn với công cụ Xem.
  - Rê chuột thì người sắp bị tác động có khung trắng, kèm chú thích như "Ban linh căn → Hàn Lập (Ngụy linh căn)".
  - Bấm vào ai thì tác động lên đúng người đó, kể cả tu sĩ đang ở tông môn.
- **Nút trên thẻ:** thẻ tu sĩ (và thẻ làng) có thêm 3 nút hạt mầm, ngôi sao, tia sét, tác động lên đúng người hoặc làng đang chọn.
- **Lệnh:** `DivineActCommand` mang theo `Target` (chỉ số tu sĩ) hoặc `Village` (id làng). Lệnh vẫn nằm trong log nên replay được.
- **Ban linh căn:**
  - **Tu sĩ** (`GrantRootTo`), không tạo ra ai mới:
    - Linh căn nhiều hệ được tẩy luyện thành **Thiên linh căn**, giữ một hệ vốn có của người đó.
    - Thiên linh căn lên **Dị linh căn** (Lôi/Phong/Băng).
    - Dị linh căn đã là cực phẩm, nên thay vào đó ngộ tính và khí vận tăng.
  - **Phàm nhân trong làng** (bấm vào dân làng hoặc nhà, `AwakenMortal`):
    - Một người trưởng thành 15–39 tuổi rời khỏi dân số làng và thức tỉnh Thiên hoặc Dị linh căn.
    - Có tông bảo hộ hoặc tông gần thì lên đường bái nhập, không thì đi lịch luyện làm tán tu. Người đó hiện ngay trên map.
  - **Đất trống:** không có gì xảy ra. Đã bỏ cơ chế cũ tự tạo đứa trẻ ở làng gần nhất.
- **Ban cơ duyên vào làng:** mùa màng bội thu (+6 tháng lương thực).
- **Thiên phạt vào làng:** sét đánh xuống làng.

## Kiểm chứng
- Play mode, đang dừng thời gian: thả 5 lần hươu và 3 lần sói. Đủ 5 hươu và 3 sói hiện đúng chỗ click (8 hình ghim).
- Test mới `GrantRootActsOnTheChosenOne`: tu sĩ được tẩy luyện, không sinh thêm người; phàm nhân trưởng thành thức tỉnh và lên đường; bấm đất trống không có gì xảy ra.
- Test EditMode pass.
