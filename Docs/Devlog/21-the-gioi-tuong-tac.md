# 21 · Thế giới tương tác: vết tích, công trình, bí cảnh và tranh đoạt cơ duyên

**Ngày:** 2026-10-07

## Lý do
- Người chơi chốt quy tắc thiết kế sandbox (GDD §0.0): ưu tiên các hệ tương tác và tự sinh nội dung. Đánh giá lại theo quy tắc đó thì vết tích (19) và công trình (20) mới chỉ để nhìn.
- Các yêu cầu của người chơi trong đợt này:
  - *"t muốn mọi thứ trong thế giới này đều có thể tương tác đc với nhau"*.
  - *"mấy cái công trình cổ đại, cổ mộ, hay những chỗ có thiên tài địa bảo xuất thế thì… vẽ thêm art riêng… hiển thị nổi bật… có các thế lực đến tranh dành cơ duyên"*.
  - *"tranh đoạt là phải đánh nhau, đấu phép giữa các tu sĩ, tông môn"*.
  - *"bảo vật tranh đoạt được có thể có ích đối với tông môn hoặc tu sĩ nào đó"*.
  - *"khi t click vào cái text của sự kiện đó thì camera lia đến chỗ đang xảy ra sự kiện"*.

## Đã làm

### Vết tích tác động lên mô phỏng
- **Độ màu mỡ** (`WorldData.Fertility` × `ScarInfo.FertilityFactor`):
  - Phù sa: tới +75%.
  - Tro núi lửa: khi còn dày thì vùi mất mùa (×0,4), sau ~10 năm trở lại bình thường, khi đã phong hóa thì phì nhiêu nhất (×1,45).
  - Đất cháy, đất hạn, đất bị giày xéo, đá nham, hố, khe nứt: bạc màu theo độ đậm.
- **Cỏ cho thú** (`ForageSystem`, `ScarInfo.ForageFactor`): cỏ chết trên đất cháy và dưới lớp tro, nên hươu và thỏ bỏ đi, sói đói theo.
- **Dân di cư chọn đất:** họ xem 3 chỗ hợp lệ và lấy chỗ màu mỡ nhất (`SoilAround`), nên tự tìm đến phù sa và tro núi lửa đã phong hóa.
- **Phản hồi:** cache thu hoạch và sức chứa cỏ được tính lại khi vết tích đổi (`LookChanged`). Thẻ ô hiện "đất màu mỡ +x%" hoặc "đất bạc màu −x%".

### Chiến trường cổ (`Landmark.Battlefield`)
- **Hình thành:**
  - Trận chiến giữa các tông môn có tu sĩ vẫn lạc sẽ để lại địa danh "Cổ chiến trường …", tích oán khí bằng số người chết.
  - Đánh tiếp ở đó thì oán khí dày thêm. Sau 120 năm thì tan.
- **Tác động:**
  - Ma tu đứng trên chiến trường tu luyện nhanh hơn, tới ×2 (`GrudgeAt`).
  - Ma tu đi tìm chiến trường cổ để lập động phủ (`HeaviestBattlefield`), và sử sách ghi lại việc đó.
  - Mỗi năm có xác suất (theo oán khí) oán khí ngưng tụ thành yêu thú. Yêu thú cấp cao hơn khi oán khí càng dày; sinh xong thì oán khí giảm 1/3.
  - Thẻ ô hiện oán khí và số năm nữa thì tan.

### Công trình tác động lên mô phỏng
- **Tường thành:**
  - Thú triều giết ít hơn 60%, và sự kiện ghi "… đóng cổng thành cố thủ".
  - Dân mất đất chạy về thành có tường gần nhất (trong 100 ô) (`RefugeHost`).
- **Miếu thổ địa:** ôn dịch nhẹ hơn 30%.
- **Chợ:** gửi đoàn buôn gấp đôi, và thương nhân thích đến thành có chợ.
- **Kinh đô:**
  - Kinh đô bị bỏ hoang thì nước dời đô về thành lớn nhất còn lại. Thành đó dựng hoàng cung và tường.
  - Không còn thành nào thì nước diệt vong. Nhãn trên map đổi thành "Cố …".
  - Di dân lập làng trên đất cũ thì **phục quốc**.
- Thẻ làng hiện quy mô và tác dụng từng công trình.

### Bí cảnh có art riêng (`ObjectType.RelicCave … RelicTreasure`)
- **Mỗi bí cảnh là một công trình pixel trên map**, có 2 trạng thái: còn phong ấn và đã bị vét sạch.
  - **Động phủ:** cửa đá có phù ấn trong khối núi; khi bị vét thì thành miệng hang tối.
  - **Di tích tông môn:** cột đỏ gãy, mái ngói sụp, bậc đá.
  - **Cổ mộ:** gò mộ cỏ, bia đá, hai tượng canh, ma trơi; khi bị vét thì mộ bị đào.
  - **Thượng cổ di tích:** cổng đá khổng lồ có phù văn tím và cổng không gian; khi bị vét thì xà cổng đổ.
  - **Thiên địa linh vật:** hoa sen pha lê phát sáng trên đài đá.
- **Hào quang** (`FxRenderer.DrawRelicBeacons`):
  - Bí cảnh đã được biết đến có cột sáng theo màu loại, nhấp nháy theo bậc, cùng vòng hào quang và hạt sáng bay lên.
  - Khi đang bị tranh đoạt thì có vòng đỏ đập quanh.
  - Mọi thứ đều vẽ pixel: phóng to theo bội nguyên, alpha theo bậc.
- **Cổ mộ và thượng cổ di tích có sẵn từ lúc tạo thế giới** (3–4 mỗi loại), ẩn trong vùng hoang, cách làng trên 50 ô. Cấp 3–5, ba tầng, có pháp bảo.
  - Tên lấy từ kho lore mới `places.json` (`ancientRuins`, `ancientTombs`), ví dụ Hư Thiên Điện, Trụy Ma Cốc, Huyết Sắc Cấm Địa, Kiếm Trủng, Vạn Cốt Mộ…
- **Thẻ thông tin bí cảnh** (người chơi là Thiên Đạo nên thấy hết): bên trong có gì, nguy hiểm cấp mấy, ai đã phát hiện, có đang bị tranh đoạt không.

### Thiên tài địa bảo xuất thế (`RelicSystem.Emerge`)
- Mỗi năm có xác suất linh khí đậm nhất thế giới ngưng tụ thành linh vật, cấp theo linh khí.
- Sự kiện "Bảo quang xung thiên…" kèm cột sáng; cả thiên hạ đều biết, và tranh đoạt bắt đầu ngay.

### Tranh đoạt cơ duyên (`RelicSystem.StartContest`, `ResolveContests`)
- **Áp dụng cho:** cơ duyên lớn, tức bí cảnh cấp 3 trở lên, linh vật, cổ mộ, thượng cổ di tích.
- **Ai đến:**
  - Mỗi tông môn trong 260 ô cử tới 3 tu sĩ mạnh nhất (từ Trúc Cơ, đang ở nhà, không bận chiến tranh).
  - Thêm tán tu và ma tu mạnh nhất, đi một mình. Tối đa 5 phe.
  - Họ bay đến thật (`SendToBattle`), và người xem thấy họ tụ về.
- **Hỗn chiến:**
  - Người mạnh nhất của hai phe mạnh nhất đấu pháp với nhau (`Combat.Duel`, có cảnh đánh trên map). Người thua chết hoặc bỏ chạy, phe hết người thì bị loại.
  - Phe trụ lại cuối cùng vào thám hiểm. Có tổng kết: số trận, số người vẫn lạc.
  - Mỗi người chết hoặc bỏ chạy làm tông môn của họ ghét tông môn thắng thêm (`Factions.Grievance`), và chiến tranh có thể nổ ra từ đó.
- **Bảo vật có ích:**
  - **Tu sĩ:** pháp bảo (đánh mạnh hơn), linh thạch, đan dược, truyền thừa (ngộ tính, tiến độ tu luyện), linh vật (thọ nguyên).
  - **Tông môn:**
    - Nộp 40% linh thạch vào kho.
    - Truyền thừa được chép vào Tàng Kinh Các, ngộ tính của mọi đệ tử tăng.
    - Linh vật: người có dã tâm hoặc tông chủ thì tự luyện hóa. Người khác dâng làm **trấn phái linh vật**, linh khí nền quanh sơn môn tăng vĩnh viễn (`Enshrine`), nên cả tông tu luyện nhanh hơn, và nơi đó cũng đáng để đánh chiếm hơn.
- **Ví dụ thật (seed ThienDao):** Lôi Linh Tinh Thạch xuất thế.
  - Thanh Hư Môn (3 người), Ngọc Đỉnh Tông (3 người) và tán tu Lý Kinh Hồng kéo đến.
  - 4 trận đấu pháp, 3 tu sĩ vẫn lạc.
  - Thượng Quan Tử Yên chân nhân (Thanh Hư Môn) giành được, nộp 165 linh thạch vào kho rồi luyện hóa linh vật.

### Bấm vào sự kiện để lia camera
- Dòng tin trên ticker và các dòng trong cửa sổ Sự kiện đều bấm được.
- Camera lia mượt tới nơi xảy ra (`CameraController.GlideTo`), phóng to vừa đủ.
- Sự kiện không có tọa độ thì lia tới chỗ tu sĩ liên quan đang đứng, và mở thẻ của người đó.
- Kéo chuột, phím, hay cuộn chuột đều lấy lại quyền điều khiển camera.

## Kết quả
- Vòng lặp sandbox (GDD §0.0), ví dụ: người chơi gây núi lửa → tro vùi mất mùa, thú bỏ đi → mười năm sau tro phong hóa thành đất tốt nhất → di dân kéo đến → làng lên trấn, lên thành → dựng chợ, miếu, tường → thú triều ít giết được người.
- Chiến tranh tông môn → chiến trường cổ → ma tu tụ về, yêu thú sinh ra → làng quanh đó bị thú triều.
- Linh vật xuất thế → các tông môn tranh đoạt, có người chết → thù hận → chiến tranh → thêm chiến trường cổ.
- **Cân bằng (đo 1000 năm, seed ThienDao):**
  - Lần đo đầu: yêu thú tăng từ 14 lên 128, cao điểm có 53 chiến trường cổ cùng sinh yêu thú. Tu sĩ còn sống tụt từ 730 xuống 500, phàm nhân từ 31 nghìn xuống 25 nghìn.
  - Đã chỉnh:
    - Chiến trường cổ chỉ sinh yêu thú khi oán khí từ 8 người trở lên, tối đa 6%/năm.
    - Trên 40 yêu thú thì oán khí ngủ yên.
    - Các trận đánh gần nhau gộp thành một chiến trường.
    - Tranh đoạt cơ duyên bớt chết chóc (tỉ lệ tử vong cơ bản mỗi trận 0,3 → 0,2).
  - Sau khi chỉnh: yêu thú 9–25 con; luôn có 15–23 chiến trường cổ và khoảng 12 bí cảnh mở; tu sĩ còn sống 912; phàm nhân 36 nghìn và vẫn tăng.
  - `PerfProbe` có thêm các cột yêu thú sống, chiến trường cổ, bí cảnh mở.
- **Hiệu năng:** năm 1000 tốn 86,7 ms/năm (84,3 ở devlog 20), với thế giới đông tu sĩ hơn. Hệ Yêu thú tăng từ khoảng 1 lên 4,5 ms; sẽ xem lại khi làm lại yêu thú cho sự kiện hung thú.
- **Test EditMode:** 51/51. Test bí cảnh đã sửa, vì thế giới giờ có sẵn cổ mộ và di tích ngay từ đầu.

## Tiếp theo
- Di chuyển: tu sĩ biết bay thì nhanh và vượt mọi địa hình; tu sĩ đi bộ thì chậm, tự tìm đường né núi và sông (người chơi yêu cầu).
- Mùa, ngày/đêm theo hướng có tác động (sông đóng băng, đêm ma tu mạnh hơn).
