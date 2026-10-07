# 25 · Sát thương Thiên Đạo, yêu thú to theo giai, tốc độ theo tu vi, Hóa Thần xé rách hư không

**Ngày:** 2026-10-07

## Lý do
- Người chơi:
  - *"sét đánh đang k làm mất máu quái, cái này k đúng logic"*
  - *"tất cả các sinh vật đều có thể bị dính sát thương từ thiên đạo"*
  - *"các sinh vật đang di chuyển quá nhanh, đặc biệt là các tu sĩ"*
  - *"các yêu thú giai càng cao thì càng to"*
  - *"mấy ông tu sĩ tu vi cao, yêu thú giai cao thì đi nhanh hơn"*, *"khi đạt hóa thần còn có thể xé rách hư không mà đi"*

## Đã làm

### Mọi đòn của trời đất tính bằng sinh lực (`Sim/HarmSystem.cs`)
- **Một hệ dùng chung cho mọi đòn:** thiên lôi, động đất, bão, núi lửa.
  - Nhận một hoặc nhiều vòng tròn (bão là một chuỗi vòng dọc đường đi).
  - Lực đòn mạnh nhất ở tâm, còn 30% ở mép.
  - Đòn nặng hơn số máu còn lại thì chết; nhẹ hơn thì mất máu, sau đó hồi dần hoặc bị kẻ khác kết liễu.
- **Đối tượng bị trúng:**
  - **Tu sĩ và yêu thú:** mất máu thật (`Hp`).
  - **Đoàn di dân:** mất phần người tương ứng với lực đòn; hết người thì đoàn tan.
  - **Thương đội:** mất từ một nửa số người trở lên thì tan tác, mất hàng.
  - **Đàn thú trong vùng:** mất phần tương ứng với diện tích bị trúng.
- **Tất định:** không dùng số ngẫu nhiên, nên cùng một đòn trên cùng một thế giới luôn cho cùng kết quả.

### Thiên phạt
- **Một đạo thiên lôi = 4.000 sát thương** (`HarmSystem.Bolt`). Người hay thú bị nhắm trúng mất ít nhất 40% sinh lực.
  - Luyện Khí đến Nguyên Anh chết ngay.
  - Hóa Thần và hung thú cửu giai (khoảng 13.000 máu) chịu được 1–2 đạo, đến đạo thứ 3 thì chết.
- **Nhắm được vào yêu thú:** bấm trực tiếp, hoặc dùng nút sét trên thẻ của yêu thú, đàn thú, đoàn di dân, thương đội.
- **Lan ra xung quanh:** mọi sinh vật trong 3 ô quanh chỗ sét đánh đều trúng. Cây cối bị đốt, đất cháy sém.
- **Sử sách ghi lại:**
  - Kẻ sống sót: "chịu được một đạo thiên lôi, sinh lực còn 7.800/13.000".
  - Thiệt hại xung quanh: "Thiên lôi lan ra: 2 tu sĩ vẫn lạc, 1 yêu thú bỏ mạng".

### Thiên tai cũng gây sát thương
| Thiên tai | Sát thương ở tâm | Ghi chú |
|---|---|---|
| Động đất | 400 do Thiên Đạo gây ra, 200 tự nhiên | Ai đang bay thì không sao |
| Bão | 250 / 120 | Dọc đường bão đi qua; đang bay cũng bị cuốn |
| Núi lửa | 900 | Trong vùng rừng cháy quanh chân núi |

- Tin báo thiên tai ghi thêm số tu sĩ và yêu thú thương vong, ví dụ "…, 3 tu sĩ vẫn lạc, 2 yêu thú bị thương".

### Yêu thú giai càng cao càng to
- **Kích thước theo từng giai:** nhất giai ×1, nhị giai ×1,12, …, bát giai ×2,5, cửu giai ×3.
- **Hung thú** to thêm 15%.
- Pixel vẫn được nhân lên với điểm lấy mẫu sắc nét (point sampling), kích thước làm tròn theo pixel của sprite.
- **Cảnh giao đấu** (fight scene) giờ cũng vẽ yêu thú đúng kích thước của nó, không còn ×1 như trước.

### Tốc độ: chậm lại, nhưng tu vi càng cao càng nhanh
- Người chơi: *"mấy ông tu sĩ tu vi cao, yêu thú giai cao thì đi nhanh hơn"*, *"khi đạt hóa thần còn có thể xé rách hư không mà đi, gần như là teleport"*.
- **Đi bộ chậm còn khoảng một nửa:**
  - Tu sĩ Luyện Khí và di dân: 3 → 1,4 ô/ngày. Luyện Khí mỗi tầng nhanh thêm 4%, tầng 13 nhanh gấp 1,5.
  - Thương đội 2,5 → 1,3; thú hoang chậm theo tỉ lệ tương tự.
- **Ngự kiếm phi hành:** Trúc Cơ 4, Kết Đan 6, Nguyên Anh 10 ô/ngày (trước là 10, 14, 20).
- **Hóa Thần xé rách hư không** (`CreatureSystem.TearTheVoid`):
  - Không bay nữa mà bước vào vết nứt hư không và ra ở nơi cần đến ngay trong ngày.
  - Không bao giờ ở ngoài đường, nên không gặp yêu thú hay kẻ địch dọc đường.
  - **Trên map:** một vết nứt hư không pixel mở ra ở chỗ đi và chỗ đến. Vết nứt đen viền tím, có đốm sáng; mở hẹp, toác rộng, hư không tán ra rồi khép lại. Không còn cảnh người trượt ngang map (`FxRenderer.WatchRifts`).
- **Yêu thú theo giai:**
  - Đi bộ: × (1 + 0,17 × (giai − 1)), cửu giai nhanh gấp 2,4 nhất giai.
  - Bay: 2 + 0,9 × giai (nhất giai 2,9, cửu giai 10 ô/ngày).
- **Ở tốc độ x1** (6 ngày/giây):
  - Luyện Khí đi bộ khoảng 8 ô/giây, trước là 18.
  - Trúc Cơ bay 24 ô/giây, trước là 60.
- **Giữ cân bằng khi đi chậm** (`CreatureSystem.RoadPace = 0,45`):
  - Chuyến đi dài gấp khoảng 2,2 lần, tu sĩ ở ngoài đường lâu hơn. Không bù thì bị yêu thú xé xác nhiều hơn khoảng 25%, và số tu sĩ còn sống tụt từ 754 xuống 573 ở năm 500.
  - Vì vậy xác suất mỗi tháng gặp yêu thú hay gặp tu sĩ lạ giữa đường được nhân với 0,45, để mỗi chuyến đi nguy hiểm như cũ.
  - Hạn tập hợp liên minh trảm yêu 240 → 480 ngày.

## Kết quả
- **Test EditMode:** 55/55. Hai test mới:
  - `ThienPhatWoundsEveryCreatureByItsSinhLuc`:
    - Hung thú cửu giai sống qua đạo sét đầu nhưng mất máu.
    - Yêu thú nhị giai và con đứng cạnh nó chết ngay.
    - Đủ số đạo sét thì hung thú cũng chết.
    - Tu sĩ đứng cạnh chỗ sét đánh bị mất máu.
  - `HoaThanTearsTheVoidAndTheMightyMoveFaster`: Hóa Thần tới nơi cách 200 ô ngay trong ngày; Kết Đan bay 6 ô/ngày.
- **Cân bằng (1000 năm, seed ThienDao):**
  - Phàm nhân 29,6 nghìn (32,9 nghìn ở devlog 24), tu sĩ còn sống 683 (861 trước đây), yêu thú 41.
  - Lịch sử là hệ hỗn loạn: cùng seed nhưng chỉ một thay đổi nhỏ cũng lệch ±100 tu sĩ ở năm 500.
  - Bản thử chưa bù `RoadPace` cho 573 tu sĩ ở năm 500; bản cuối cho 703.
- **Hiệu năng:**
  - Năm 1000 tốn 96 ms/năm (Editor Debug), so với 100 ms trước đó.
  - Riêng hệ Di chuyển tăng từ 32 lên 40 ms, vì cùng số người nhưng mỗi chuyến đi dài hơn nên phải lái nhiều ngày hơn. Chỗ này để tối ưu sau.
  - `HarmSystem` chỉ chạy khi có đòn đánh xuống, không tốn gì mỗi ngày.
