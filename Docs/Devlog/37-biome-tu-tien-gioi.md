# 37 · Biome tu tiên giới

**Ngày:** 2026-10-11

## Lý do
- Người chơi: *"địa hình bio theo phong cách tu tiên giới"*, rồi *"oke làm biome tu tiên giới đi bro"*.
- Theo [`Docs/Vision.md`](../Vision.md): biome nào cũng phải có tác dụng trong mô phỏng, không chỉ để đẹp.

## Cách làm: một lớp biome chồng lên địa hình
- **Địa hình** (`WorldData.Terrain`) vẫn nói mặt đất là gì: rừng, đầm, núi.
- **Biome** (`WorldData.Biome`, mới) nói thiên địa đã biến vùng đất đó thành gì: linh sơn, ma vực, lôi trạch…
- **14 loại** (`World/Biomes.cs`):

| Biome | Ngũ hành hợp | Linh khí | Mọc ở đâu khi khai thiên lập địa | Yêu thú |
|---|---|---|---|---|
| Linh sơn | mọi hệ (+10%) | +25% | đồi núi linh khí cao | hung viên, yêu điêu |
| Tiên đảo | Phong | +22% | đảo nhỏ giữa biển (20–1.500 ô) | yêu điêu, huyền quy |
| Trúc lâm | Mộc | +8% | chỗ trúc mọc ở Chính Đạo và Cửu Quốc | hung viên, yêu hồ |
| Đào hoa lâm | Mộc | +6% | chỗ đào nở ở Chính Đạo và Cửu Quốc | yêu hồ |
| Linh hồ | Thủy | +20%, mặt nước không bị giảm linh khí | hồ nông linh khí cao, cả bờ hồ | huyền quy, phi long |
| Hàn băng nguyên | Băng | +4% | tuyết, đồng rêu, Cực Bắc | yêu hùng, yêu lang |
| Hỏa vực | Hỏa | +10% | dung nham, đồi núi Ma Đạo, hoàng thổ | hỏa kỳ lân, ma ngưu |
| Lôi trạch | Lôi | +15% | một nửa số đầm lầy | yêu điêu, huyết bức |
| Ma vực | ma tu ×1,4, chính đạo ×0,75 | +10% (ma khí) | đất tro và phần lớn Ma Đạo | ma ngưu, huyết bức, đôi khi hỗn độn |
| Độc chướng lâm | Mộc | +12% | nửa còn lại của đầm, rừng rậm phương nam | cự mãng, độc hạt |
| Cấm địa | mọi hệ (+10%) | +35% | 2 vùng sâu trong đất liền, cách nhau ít nhất 300 ô | hỗn độn, yêu hổ |
| Hoang mạc | Thổ | −3% | sa mạc, hoàng thổ | độc hạt |
| Kim thạch sơn | Kim | +5% | đồi núi phương bắc, đồi sa mạc | hung viên, yêu hổ |
| Đất thường | — | — | còn lại: bình nguyên, rừng thường | theo địa hình như cũ |

- **Tu luyện:** tu sĩ có linh căn hợp hệ của vùng thì tu nhanh hơn ×1,35 (`BiomeInfo.CultivationFactor`).
- **Linh khí:** phần cộng thêm nằm ngay trong `QiCap.Recompute`, nên mọi lần tính lại (vẽ linh mạch, rót linh khí…) vẫn giữ đúng tính linh của đất.
- **Chân linh** thích làm hang ổ trên đúng loại đất của mình (điểm chọn ×1,6):

| Chân linh | Thích |
|---|---|
| Chân Long | linh hồ, tiên đảo, linh sơn |
| Thiên Phượng | hỏa vực, linh sơn |
| Kỳ Lân | hỏa vực, linh sơn, trúc lâm |
| Côn Bằng | tiên đảo, hàn băng |
| Huyền Vũ | linh hồ, tiên đảo |

- **Hái linh dược:**
  - Tu sĩ ưu tiên linh dược viên ở trúc lâm, đào hoa, linh sơn; ở các vùng này và độc chướng lâm thì dễ tìm thấy hơn (+15%).
  - Độc chướng lâm và cấm địa cho thảo dược ngàn năm nhiều gấp 2,5 lần.
  - Ở độc chướng lâm có 30% trúng chướng khí, mất 35% sinh lực.
- **Bộ lạc nhân tộc** không dựng lều ở ma vực, cấm địa, độc chướng lâm.
- **Cây cối** theo biome: trúc dày ở trúc lâm, đào ở đào hoa, thân cây khô đen ở ma vực, thông tuyết ở hàn băng, rừng rậm ở độc chướng; cấm địa chỉ có đá.
- **Vùng có tên:** mỗi mảng biome từ 400 ô trở lên được đặt tên, không trùng nhau, kiểu "Xích Diễm Sơn Mạch", "Bích Lạc Ma Vực", "Lạc Nhật Cấm Địa", "Linh Lung Băng Nguyên". Mảng dưới 40 ô thì trả về đất thường.
- **Ma vực lan ra:** một cổ chiến trường đã đổ máu đủ 25 sinh linh thì đất quanh đó hóa ma vực (sự kiện lớn). Chiến tranh yêu tộc ở devlog 36 cũng để lại dấu trên đất.

## Thiên Đạo tạo hóa
- Tab Địa hình có thêm **9 cọ biome**: Tạo linh sơn, Tạo linh hồ, Tạo trúc lâm, Tạo đào hoa lâm, Tạo lôi trạch, Tạo hỏa vực, Tạo ma vực, Tạo cấm địa, Trả về đất thường.
  - Cọ theo cỡ bút, có mép răng cưa.
  - Linh khí của vùng đổi ngay, map vẽ lại.
  - Mỗi nét cách nhau 0,12 giây.
- Ví dụ: vẽ linh sơn lên một bình nguyên thì linh khí ở đó tăng ngay; ma tu kéo về ma vực; Chân Long sẽ nhắm tới linh hồ.

## Hiện trên map (pixel art)
- **Mặt đất:** màu biome phủ lên địa hình, kèm điểm ảnh đặc trưng (cố định theo vị trí, không nhấp nháy):
  - sương trắng và ánh linh thạch trên linh sơn;
  - cánh đào hồng;
  - bóng trúc theo hàng;
  - ánh nước ở linh hồ;
  - vết nứt băng;
  - tàn lửa và khe đỏ ở hỏa vực;
  - đốm máu ở ma vực;
  - đốm vàng lục ở độc chướng lâm;
  - khe đen ở cấm địa;
  - ánh kim ở kim thạch sơn.
- **Bản đồ thu nhỏ:** các vùng hiện thành mảng màu của biome.
- **Không khí từng vùng** (chỉ trong khung nhìn, zoom xa thì tắt):
  - đào hoa rơi cánh, hàn băng rơi tuyết, hỏa vực bay tàn lửa;
  - lôi trạch chớp sét, linh sơn và tiên đảo có mây trôi và hạt linh khí bay lên;
  - linh hồ lấp lánh, ma vực bốc hạt đen, độc chướng tỏa chướng khí, cấm địa lóe đốm máu, kim thạch sơn ánh kim.
- **Nhãn tên vùng** theo màu biome. Zoom xa chỉ hiện vùng lớn từ 3.000 ô.
- **Thông tin ô** có thêm dòng biome, ví dụ "Lôi trạch: hợp Lôi linh căn, sét đánh không ngớt".

## Nhân tộc sống sót trong thế giới 400 yêu thú
- Chạy thử cho thấy bộ lạc đầu tiên bị xóa sổ trong vài chục năm. Lý do: yêu tộc năm nào cũng tràn xuống nhân gian, tầm với tới 200 ô.
- Đã sửa:
  - Yêu tộc chỉ tràn xuống làng nằm **trong lãnh địa của mình**, làng từ 60 người trở lên, xác suất 6% + 0,6% theo quân số (trước là 15% + 3%).
  - Làng dưới 80 người: 60% số lần đột kích, dân trốn được.
  - Bộ lạc chọn chỗ có ít hang yêu thú trong vòng 50 ô.
- Chạy thử 250 năm (seed ThienDao):

| Năm | Diễn biến |
|---|---|
| 72 | bộ lạc đầu tiên |
| khoảng 100 | thủy tổ tu tiên |
| 250 | 8 làng, 644 người; bầy thú khoảng 500 con |

## Kết quả
- **Test EditMode:** 63/63. Test mới `BiomesGiveTheLandItsSpirit`:
  - có đủ linh sơn, ma vực, hàn băng, cấm địa, trúc lâm, hỏa vực;
  - linh khí trên linh sơn cao hơn trung bình gấp 1,5 lần;
  - tên vùng không trùng;
  - đúng hệ thì tu nhanh hơn, ma vực có lợi cho ma tu;
  - cọ linh sơn làm tăng trần linh khí;
  - chiến trường đẫm máu hóa ma vực.
- Sinh map lâu thêm khoảng 0,8 giây vì phải tách vùng đất và mảng biome.
- **Ảnh chụp:** Xích Diễm Sơn Mạch nằm giữa Âm Ma Vực, có Phù Dung Ma Vực, Bích Lạc Ma Vực, Côn Ngô Hỏa Vực; Thiên Phượng làm hang ổ trên linh sơn.
