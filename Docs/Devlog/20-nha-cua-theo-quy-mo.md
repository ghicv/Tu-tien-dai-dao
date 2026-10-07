# 20 · Nhà cửa theo quy mô: thôn, trấn, thành, kinh thành

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"đa dạng nhiều loại nhà cửa, kinh thành, làng quê,... thể hiện sự khác biệt rõ ràng về quy mô của các loại này"*.
- Hai luật mới người chơi chốt trong lúc làm:
  - *"art style đều là pixel hết… áp dụng cho toàn bộ hiệu ứng trong game"*.
  - *"tính năng gì cũng phải ưu tiên hiệu năng lên đầu tiên, luôn tìm cách tối ưu được hiệu năng mà vẫn giữ được chất sandbox"*.

## Đã làm

### Cấp bậc của một nơi (`SettlementSystem.Standing`)

| Cấp | Điều kiện | Nhà | Công trình |
|---|---|---|---|
| 0 · Thôn | dưới 150 dân | **Nhà tranh**: vách đất, mái rạ tròn, có nhà kèm đống rơm | Giếng làng |
| 1 · Trấn | 150–399 dân | **Nhà ngói**: nhà ngói thường và nhà dài hai cửa có ống khói | + Miếu thổ địa, Chợ |
| 2 · Thành | từ 400 dân | **Nhà lầu**: hai tầng, ban công, mái cong đầu đao; nhà phố có mái hiên sọc và đèn lồng | + Bảo tháp, tường thành, tháp canh |
| 3 · Kinh thành | kinh đô của một nước | **Phủ đệ**: tường trắng bao sân, sảnh cột đỏ, mái lưu ly bóng, nóc vàng; có kiểu lầu hai mái | + Hoàng cung |

- Mỗi kiểu nhà có 2 dáng × 4 màu mái, tổng 32 sprite nhà.
- Variant của nhà = kiểu × 8 + dáng × 4 + mái.
- **Trung tâm sang hơn rìa:** nhà trong bán kính 7 ô quanh tâm theo đúng cấp; nhà xa hơn kém một cấp.
  - Kinh thành có phủ đệ quanh hoàng cung, nhà lầu ở ngoài.
  - Thành có nhà lầu ở giữa, nhà ngói ở rìa.
- **Lớn lên dần:**
  - Mỗi năm một nơi xây thêm tối đa 1 công trình còn thiếu và xây lại 3 căn theo kiểu mới.
  - Làng đông lên thì nhà tranh dần thành nhà ngói, rồi nhà lầu; dân giảm thì xuống cấp ngược lại.
  - Đổi kiểu tại chỗ bằng `WorldObjects.SetVariant` (sự kiện `Changed`), không xoá rồi đặt lại.
- **Công trình** (`Settlement.Civic`, không tính vào chỗ ở):
  - Đặt ở chỗ trống gần tâm nhất (tìm xoáy ốc ra 14 ô) và chặt cây trên chỗ đó.
  - Hoàng cung chiếm 7×7 ô, đứng trên nền đá ba bậc, có hai mái vàng, hai cánh và tượng thú trên nóc.
  - Không chọn ngẫu nhiên: xây gì, ở đâu đều suy ra từ chính nơi đó.
- **Tường thành** (`ZoneFlags.Wall`):
  - Thành và kinh thành có từ 6 nhà trở lên thì xây một vòng tường quanh nội thành, tức các nhà cách tâm tối đa 14 ô; nhà xa hơn là ngoại thành.
  - Mỗi cạnh có cổng ở giữa, đường buôn đi qua chỗ nào thì chỗ đó cũng là cổng. Sông và vách núi thay cho tường.
  - Mỗi góc có tháp canh, cây trên tường bị chặt.
  - Tường nới ra khi thành lớn lên, mất khi thành suy.
  - Vẽ thẳng trên pixel đất: lối đi lát đá, lỗ châu mai ở các cạnh nhìn ra ngoài, mặt tường tối ở chỗ tường đổ xuống đất.
- Tông môn chỉ đào giếng; các thứ khác đã có hộ sơn đại trận lo.
- `WorldInvariants` kiểm tra luôn ô của công trình.
- Sự kiện `ScarChanged` đổi tên thành `LookChanged`, vì giờ dùng cho cả vết tích và tường thành.

### Hiệu năng
- Mọi thay đổi chỉ chạy một lần mỗi năm cho mỗi nơi; xây lại nhà tối đa 3 căn mỗi năm.
- Tìm chỗ cho công trình là phần tốn nhất. Nếu không còn chỗ thì nơi đó nghỉ 10 năm mới tìm lại (`CivicRetry`), không quét mỗi năm.
- Tường chỉ được đóng lại khi vòng tường thay đổi, và chỉ vẽ lại vùng của vòng tường.
- Đo được: phần Phát triển tốn 1,6 ms/năm ở năm 1000.

### Tối ưu "Mở ruộng" (lỗi cũ, phát hiện khi đo)
- **Lần đo đầu cho thấy năm 1000 tốn 116 ms/năm, so với 101 ms ở [17](17-toi-uu-hieu-nang.md).**
  - Hệ Làng tăng từ 14,3 lên 36,6 ms.
  - Đo chi tiết từng phần (`SettlementSystem.ProfMs`, bảng mới "Trong hệ Làng" trong `Docs/Perf_ThienDao.md`): **Mở ruộng chiếm 31,7 ms**, còn Phát triển chỉ 1,6.
- **Nguyên nhân:**
  - `ClaimFarmland` quét xoáy ốc từ vòng 2 mỗi lần gọi, tức là đi qua lại toàn bộ ruộng đã có (tới khoảng 3.700 ô) rồi mới đến đất trống.
  - Hàm này chạy hằng tháng cho mọi làng còn thiếu ruộng, kể cả làng không còn đất để mở.
  - Từ commit 18 có nhiều làng như vậy hơn: đất tro ở Ma Đạo cày được nhưng kém màu mỡ, nên làng cần nhiều ruộng hơn.
- **Sửa:**
  - Mỗi làng nhớ vòng gần nhất còn đất trống (`Settlement.ClaimFrom`) và lần sau quét tiếp từ đó.
  - Bộ nhớ này bị xoá khi có đất được trả lại gần làng: địa hình đổi (trừ khi do chính việc mở ruộng, vì mở ruộng chỉ lấy đất), nhà hoặc công trình bị phá, mất ruộng do thiên tai, làng dời đi.
  - Kết quả y hệt như quét từ vòng 2.
- **Kết quả (seed ThienDao, 1000 năm):**
  - Lịch sử **trùng hoàn toàn** với lần chưa tối ưu: tu sĩ, dòng sử sách, entity, phàm nhân giống nhau ở mọi thế kỷ.

  | Năm | Trước (ms/năm) | Sau (ms/năm) |
  |---|---|---|
  | 100 | 30,5 | 26,1 |
  | 500 | 93,5 | 65,7 |
  | 1000 | 116,1 | **84,3** |

  - Mở ruộng: 31,7 → 0,5 ms/năm.
  - Năm 1000 giờ còn nhẹ hơn mốc 101 ms của devlog 17.

## Kết quả (seed ThienDao)
- 17 kinh thành, kinh thành nào cũng có hoàng cung, giếng, miếu, chợ, bảo tháp, tường thành và 2–4 tháp canh.
- Invariant: 0 lỗi.
- Thôn là cụm nhà tranh có đống rơm và giếng giữa đồng. Một làng được bơm lên 490 dân thì thành nhà lầu, có bảo tháp, miếu, chợ, tường và tháp canh.
- **Test EditMode:** 51/51.

## Tiếp theo
- Mùa trên mặt đất, ngày/đêm, chim, khói bếp, bóng mây (vẽ pixel).
- Tăng tương tác giữa các sinh vật.
