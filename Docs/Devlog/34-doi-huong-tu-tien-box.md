# 34 · Đổi hướng: Tu Tiên Box, gỡ các hệ đi sai đường

**Ngày:** 2026-10-09

## Lý do
- Người chơi chơi thử xong thì kết luận: *"chúng ta đang đi sai con đường của một tựa game tu tiên sandbox"*.
- Hai bên ngồi lại so fantasy của WorldBox với fantasy của Tu Tiên Box:
  - WorldBox: *"Tôi tạo ra một thế giới và xem chuyện gì xảy ra."*
  - Tu Tiên Box: *"Tôi tạo ra thiên địa và chứng kiến chúng sinh tranh đoạt con đường trường sinh."*
- Định vị mới: **một thế giới nơi con đường tu luyện của từng cá thể có thể thay đổi vận mệnh của cả nền văn minh.**
- Toàn bộ tầm nhìn nằm ở [`Docs/Vision.md`](../Vision.md):
  - ba trụ cột: Cultivation, Sect & Inheritance, Heavenly Dao Sandbox;
  - vòng lõi;
  - tiêu chí prototype 15 phút;
  - tông môn ít mà lớn;
  - timeline từ hồng hoang;
  - biome tu tiên giới.
- Kế hoạch "clone WorldBox" (bom, nam châm, phàm nhân đi làm nghề) bị bỏ trước khi kịp làm.
- Người chơi: *"hệ thống hiện tại đi sai hướng, cái nào bỏ đc thì bỏ đi"*.

## Đã gỡ
| Hệ | Lý do bỏ | Thay bằng |
|---|---|---|
| Thiên mệnh (`DestinySystem`) và cửa sổ Thiên mệnh | sandbox không cần nhiệm vụ | — |
| AI riêng của nhân vật chính (`ProtagonistAI`) | đè lên hệ việc ngoài kia | "Theo dõi" chỉ còn ghim và đi theo; người được theo dõi tự đi làm việc, tự tìm động phủ như mọi người |
| Chính trị hoàng triều (`PoliticsSystem`): vua, bất mãn, khởi nghĩa, tranh ngôi, đổi triều, cát cứ | chất WorldBox | tên nước vẫn còn trên map, chỉ làm địa danh |
| Thương mại, thương đội, chợ, giá (`TradeSystem`) | chất WorldBox | — |
| Đường mòn, đường đất (`PathSystem`) | chất WorldBox, lại tốn hiệu năng | — |
| Cầu nguyện, tín ngưỡng làng (`FaithSystem`) | chất làng phàm nhân | sau này làm lại theo kiểu sinh linh biết đến Thiên Đạo |
| Tin đồn (`KnowledgeSystem`) | rối, khó thấy | tranh đoạt và liên minh trảm yêu tính theo khoảng cách |
| Công cụ đặt nhà dân, đặt đại điện tông môn | người tự xây | — |

- Kéo theo cũng gỡ:
  - vòng tin đồn và khói nhang trên map;
  - các chip bất mãn, tín ngưỡng, chợ, nghe đồn trên thẻ làng và thẻ tông môn;
  - nhãn khởi nghĩa và cầu nguyện trên map;
  - thẻ thương đội;
  - cột tương ứng trong bảng đo hiệu năng;
  - 6 test của các hệ đã bỏ.
- Đồng hồ thời gian theo hệ giờ còn 13 mục.
- Khoảng 2.000 dòng mã sim bị xóa. Git vẫn giữ lịch sử, cần thì lấy lại được.
- Các file lưu cũ không còn mở được, vì cấu trúc mô phỏng đã khác.
- **Còn nằm im, không chạy:** phần vẽ đường và phần tìm đường ưu tiên đường trong `NavSystem` và `WorldRenderer`. Không còn ai tạo ra đường nên mấy phần này không làm gì cả. Sẽ dọn khi làm lại địa hình.

## Kết quả
- Test EditMode: 58/58.

## Tiếp theo
- Khởi đầu từ **hồng hoang**:
  - chỉ có ma thú, hung thú, chân linh;
  - nhân tộc tự xuất hiện khi tới thời kỳ, và Thiên Đạo cũng tạo ra được;
  - các thời kỳ nối tiếp, thời trước để lại phế tích cho thời sau.
- Sau đó: biome tu tiên giới, sơn môn sống, thiên kiêu bảng, sợi nhân quả.
