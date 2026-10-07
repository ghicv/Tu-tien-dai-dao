# 19 · Vết sẹo thiên tai và cây héo úa

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"các thiên tai, cháy nổ ảnh hưởng đến cell thì cũng làm thay đổi màu của cell đi đó nhé"*, rồi *"oke làm tiếp vết sẹo thiên tai đi bro"*.
- Đang làm thì người chơi thêm: *"mấy cái cây gần vùng địa hình đặc biệt thì k xanh tốt được như thế đâu bro"*.

## Đã làm

### Lớp vết tích (`WorldData.Scar`, `Sim/ScarSystem.cs`)
- Mỗi ô có 1 byte vết tích: 4 bit loại (`ScarKind`), 4 bit độ đậm (1–15).
- Chín loại vết tích, mỗi loại lành với tốc độ riêng (`ScarInfo.YearsPerStep`, số năm để mất một bậc):

  | Vết tích | Sinh ra từ | Năm mỗi bậc |
  |---|---|---|
  | Đất cháy sém | thiên kiếp, thiên phạt, sét của bão, đấu pháp từ Kết Đan, diệt môn, chân núi lửa | 3 |
  | Tro núi lửa | tro rơi trong bán kính 40 ô quanh núi lửa | 2 |
  | Hố thiên kiếp | tâm thiên kiếp, thiên phạt, thiên lôi của người chơi | 40 |
  | Khe nứt địa chấn | 3–5 vết nứt toả ra từ tâm động đất | 12 |
  | Đá nham nguội | nón núi lửa, dung nham khi nguội | 60 |
  | Chiến trường cổ | trận chiến giữa các tông môn (lớn theo số tu sĩ vẫn lạc) | 8 |
  | Phù sa sau lũ | ô vừa rút nước | 1 |
  | Đất nứt nẻ vì hạn | vùng đại hạn | 1 |
  | Đất bị thú triều giày xéo | nơi thú triều tràn qua | 1 |

- **Không đổi lịch sử thế giới:**
  - Hệ vết tích dùng luồng ngẫu nhiên riêng (`ScarRng`), nên vẽ vết sẹo không làm thay đổi kết quả tung xúc xắc của thiên tai.
  - Không hệ nào khác đọc vết tích. Ngoại lệ duy nhất: sét thiên kiếp / thiên phạt biến cây gần tâm thành cây khô.
- **Lành dần theo khối:**
  - Mỗi năm, từng khối 32×32 ô đến lượt mới giảm một bậc, nên một năm không vẽ lại cả map.
  - Viền mờ biến mất trước, lõi đậm còn lâu.
- **Lọc theo đất:** đất nứt nẻ và đất bị giày xéo chỉ có trên đất có cỏ cây hoặc ruộng; phù sa không có trên núi; biển không giữ vết tích.
- Người chơi tô lại địa hình thì xoá vết tích dưới nét cọ.
- `Scar` nằm trong state hash và trong file lưu. File lưu cũ bị bảng schema từ chối, vì cấu trúc thế giới đã đổi.

### Vẽ (`TerrainTexture.Scarred`, `PaintScar`, `ScarTint`)
- **Màu theo ô** (thấy được ở map xa): cháy sém thành đen, tro thành xám, chiến trường thành nâu đỏ, đá nham thành xám lam…
- **Chi tiết theo pixel:**
  - **Cháy sém:** gốc rạ cháy, vảy tro, than còn âm ỉ khi vết còn mới.
  - **Tro núi lửa:** tro dồn thành đống.
  - **Hố:** đáy vỡ nát, vành đất đá đổ bật lên.
  - **Khe nứt:** khe tối sâu hoắm.
  - **Đá nham nguội:** khe sáng giữa các khối cột đá.
  - **Chiến trường cổ:** xương trắng, gươm gãy gỉ, vệt máu khô.
  - **Phù sa:** gợn bùn, vũng nước loang sáng.
  - **Đất hạn:** nứt nẻ thành mảng.
  - **Đất thú triều:** vết móng.
- **Rìa lởm chởm:**
  - Vết tích loang 1–2 pixel sang ô bên cạnh, và độ đậm dao động theo từng pixel.
  - Vì vậy rìa không bị vuông theo ô, và khi phai thì vết sẹo vỡ lấm tấm chứ không mờ đều.
- Thẻ thông tin ô hiện "Vết tích: … · đất lành lại sau khoảng N năm".

### Cây héo úa gần vùng đặc biệt (`WorldRenderer.Wither`)
- Mỗi cây được chấm độ héo từ 0 (xanh tốt) đến 1 (chết), lấy mức cao nhất trong các nguồn:
  - Vết tích dưới gốc (cháy, tro, đá nham, hố…).
  - Lôi địa: 0,5.
  - Đất tro: 0,45. Hoàng thổ: 0,3.
  - Dung nham: 0,9 nếu sát bên, 0,6 nếu cách 2 ô.
  - Ở Ma Đạo cộng thêm 0,15.
- Lá ngả màu nâu xám nhưng giữ sáng tối. Độ héo từ 0,8 trở lên thì chỉ còn trơ thân cây khô.
- Chỉ ảnh hưởng hình vẽ: cây vẫn còn trong mô phỏng, khi đất lành thì xanh lại.
- Sét thiên kiếp / thiên phạt đốt gần hết cây trong vùng: gần tâm cháy rụi, xa hơn còn lại cây khô cháy đen.

## Kết quả
- Thả núi lửa, động đất, thiên lôi, thú triều, hạn hán cạnh nhau ở Chính Đạo:
  - Núi lửa có nón đá đen, dòng dung nham, vòng cháy sém, tro xám phủ quanh, cây sát dòng dung nham chết khô.
  - Động đất để lại khe nứt toả ra từ tâm.
  - Thiên lôi để lại vùng đen kịt có than hồng, hố ở giữa, thân cây khô.
- **Test EditMode:** 51/51 (mới: `CalamitiesScarTheLandAndTheLandHeals`).

## Tiếp theo
- Nhà cửa đa dạng: kinh thành, thành, trấn, thôn khác hẳn nhau về quy mô và kiểu nhà (người chơi vừa yêu cầu).
- Sau đó: mùa trên mặt đất, ngày/đêm, chim, khói bếp, bóng mây.
