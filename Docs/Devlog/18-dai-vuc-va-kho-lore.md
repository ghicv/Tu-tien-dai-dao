# 18 · Đại vực, quốc gia, kho lore và texture địa hình

**Ngày:** 2026-10-07

## Lý do
- Người chơi muốn map chia vùng như bản đồ Thiên Nam (gửi kèm ảnh): Chính Đạo, Ma Đạo, Thiên Đạo Minh, Cửu Quốc Minh, Sa Mạc Bạo Phong, Vô Biên Hải, Mộ Lan Thảo Nguyên, các nước phàm nhân. Lý do: *"địa hình trong map khá ít và đơn điệu"*.
- *"tất cả data về tên đại lục, nhân vật, tông môn, ma thú… cần gom lại thành một bộ data… random mỗi lần tạo map mới"*, và *"data phải chia phân loại ra chứ k gộp chung"*.
- *"t muốn địa hình của các khu vực phải rõ ràng hơn nữa, nhiều texture hơn"*.

## Đã làm

### Đại vực (`World/Regions.cs`, `MapGenerator`)
- **Bảy đại vực** (`RegionKind`):
  - Chính Đạo, Ma Đạo, Thiên Đạo Minh, Cửu Quốc Minh, Sa Mạc, Thảo Nguyên, Băng Nguyên.
  - Bố cục giữ theo Thiên Nam: sa mạc ở tây, thảo nguyên ở nam, băng nguyên ở bắc, Ma Đạo và Chính Đạo ở giữa.
  - Mỗi seed lật gương 50% và xê dịch tâm vùng, đường biên được bẻ cong bằng noise.
- **Mỗi vùng tự nặn đất của mình:**
  - Độ cao núi (`RegionRidges`), độ chi tiết, nhiệt độ (`RegionWarm`) và độ ẩm (`RegionWet`) riêng.
  - Địa hình đặc trưng:
    - Ma Đạo có **Đất tro** (Ashland) và rừng cây khô.
    - Sa Mạc có **Hoàng thổ** (Badlands) với đá và xương rồng.
    - Thảo Nguyên thưa rừng; Băng Nguyên có lãnh nguyên.
    - Chính Đạo và Cửu Quốc có rừng trúc và đào hoa; Thiên Đạo Minh có rừng thông.
- **Bờ biển:** độ dốc ra mép map trộn giữa hình vuông, hình tròn và noise, nên đại lục không còn vuông vức.
- **Nước phàm nhân** (`Kingdom`):
  - Mỗi vùng có vài nước. Kinh thành đặt trên đất cày được, cách nước 3–20 ô.
  - Lãnh thổ là Voronoi bẻ cong quanh kinh thành.
  - Kinh thành mang tên nước ("Việt Quốc" thành "Việt Kinh", "… Bộ" thành "… Vương Đình").
  - Mỗi làng biết mình thuộc nước nào.
- **Tông môn theo vùng:** ma môn mọc ở Ma Đạo, chính phái ở Chính Đạo, Thiên Đạo Minh và Cửu Quốc.
- **Hiển thị:**
  - Mỗi vùng phủ một tông màu nhẹ (đỏ thẫm Ma Đạo, lam lạnh phương bắc, vàng cát phía tây…).
  - Biên giới vùng là nét đứt đậm, biên giới nước là nét chấm.
  - Zoom xa hiện tên vùng chữ lớn và tên biển; zoom vừa hiện tên nước.
  - Ô đang chọn hiện "đại lục · vùng · nước".

### Kho lore (`Resources/Lore/*.json`, `World/LoreDatabase.cs`)
- Toàn bộ tên gọi rời khỏi code và chia thành 6 file theo loại:

  | File | Nội dung |
  |---|---|
  | `world.json` | Đại lục, biển, tên vùng và các nước của từng vùng |
  | `sects.json` | Chính phái, ma môn, hậu tố tên tông |
  | `people.json` | Họ, tên |
  | `places.json` | Địa danh, núi lửa, lôi địa, âm tiết ghép tên |
  | `items.json` | Linh thảo, pháp bảo, thiên địa linh vật |
  | `beasts.json` | Yêu thú, danh hiệu, tộc yêu |

- **Mỗi thế giới bốc thăm** (`WorldLore.Draw(seed)`):
  - Lấy một đại lục và một biển.
  - Lấy một phần của mỗi danh mục và một tên cho mỗi vùng.
  - Xáo thứ tự các nước.
  - Nhờ vậy hai map khác seed có bộ tên khác nhau.
- `Sim/Lore.cs` chỉ còn quy tắc đặt tên (ghép âm tiết, Thôn/Trấn/Thành, đan dược).
- Mọi hệ (Yêu thú, Tu sĩ, Thiên tai, Thế lực, Nhân vật chính, Bảo vật, Làng) giờ đọc từ `world.Lore`.

### Texture địa hình (`Render/TerrainTexture.cs`)
- Mỗi loại đất vẽ mẫu 8×8 px riêng, tính theo toạ độ pixel thế giới nên liền mạch giữa các ô và các chunk:
  - **Cỏ:** mảng cỏ đậm nhạt, ngọn cỏ hai pixel.
  - **Rừng:** tán cây tròn có khe tối, sáng từ góc trên trái; rừng rậm có lá bóng.
  - **Sa mạc:** đụn cát cong theo gió (đỉnh sáng, sườn khuất tối); ở Sa Mạc Bạo Phong đậm hơn.
  - **Hoàng thổ:** vân đất tầng tầng (đất son, đất sét đỏ), rãnh xói.
  - **Đất tro:** nứt nẻ thành mảng; ở Ma Đạo khe nứt còn ánh than hồng.
  - **Đầm lầy:** vũng nước đen giữa lau sậy.
  - **Lãnh nguyên:** sương giá loang lổ, địa y.
  - **Tuyết và đỉnh núi:** bóng xanh, gợn tuyết theo gió, lấp lánh; đỉnh núi lộ đá.
  - **Đồi:** ruộng bậc thang theo sườn, đá tảng.
  - **Núi:** vách đá, khe nứt; ở Ma Đạo có đốm dung nham.
  - **Bãi cát:** gợn sóng, vỏ sò.
  - **Ruộng:** ô bàn cờ 3×3 ô, mỗi thửa một loại cây (lúa vàng, lúa xanh, đất nghỉ), luống ngang hoặc dọc, có bờ ruộng.
  - **Dung nham:** vỏ đá tối vỡ mảng, khe sáng rực.
  - **Nước:** sóng lấp lánh, vân sáng đáy nước nông, dòng chảy trên sông.
- **Nét riêng mỗi vùng:**
  - Hoa dại ở Chính Đạo và Cửu Quốc.
  - Cỏ rạp theo gió ở Mộ Lan.
  - Sỏi đá ở Thiên Đạo Minh.
  - Sương giá trên cỏ ở Băng Nguyên.
- **Ở xa (1 px/ô):** `TerrainTexture.Macro` vẽ vệt đụn cát, tầng đất và mảng rừng ở cỡ ô, nên map tổng quan cũng có vân.
- Tông màu vùng đậm hơn một chút; Chính Đạo có thêm sắc ngọc bích nhạt.
- Ma Đạo không còn tuyết: đỉnh núi thành đá đen, đồng tuyết thành đất tro.
- **Hiệu năng vẽ chunk:**
  - Bản đầu tốn 34 ms mỗi chunk (Editor, chế độ Debug), vì mỗi mẫu noise phải băm 4 lần.
  - Thay bằng bảng ngẫu nhiên 256×256 dựng sẵn (seed chỉ dịch cửa sổ đọc): còn 22 ms.
  - Vẽ 32 hàng ô của chunk song song (`Parallel.For`; texture là hàm thuần của vị trí): còn **5,2 ms**, nằm trong ngân sách 6 ms mỗi khung hình.

### Sửa lỗi
- Đất tro và Hoàng thổ chưa có trong bảng môi trường sống của thú, nên Ma Đạo và vùng hoàng thổ không có hươu, thỏ, sói. Test `WorldStartsWithVillagesAndAnimals` bắt được lỗi này. Đã thêm (`WildlifeSystem.Habitat`).

## Kết quả
- **Seed ThienDao, PhamNhan, HanLap:**
  - Mỗi seed có tên đại lục, biển và vùng khác nhau.
  - 15–18 nước, 19–34 làng, 7–8 tông môn; ma môn nằm ở Ma Đạo.
  - Sinh map khoảng 3 giây.
- **Test EditMode:** 50/50.

## Tiếp theo
- Thế giới sống (bước 2):
  - Vết sẹo thiên tai trên đất (cháy sém, hố, chiến trường cổ).
  - Mùa trên mặt đất, ngày/đêm.
  - Chim, khói bếp, bóng mây.
