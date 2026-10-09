# 36 · Yêu tộc tranh bá, vạn thú hiện hình

**Ngày:** 2026-10-09

## Lý do
- Thời hồng hoang (devlog 35) mới có thú nhưng chưa có chuyện gì xảy ra giữa các tộc. Người chơi: *"oke làm yêu tộc tranh bá đi bro"*.
- Người chơi tiếp: *"logic giới hạn số yêu thú xuất hiện trong 1 chunk làm mất đi cái đã mắt về số lượng… chỉ show quái ở trong khu vực người chơi có thể nhìn thấy, còn khi zoom xa thì tắt hết"*.

## Yêu tộc tranh bá (`Sim/BeastWars.cs`, mới)
- **Lãnh địa:** mỗi tộc giữ vùng đất quanh hang ổ của vương.
  - Bán kính = 40 ô + 5 ô cho mỗi con quy phục; chân linh cộng thêm 40 ô; tối đa 170 ô.
  - Số con quy phục được đếm lại mỗi năm.
- **Khai chiến:**
  - Lãnh địa hai tộc chạm hoặc gần chạm nhau (trong 1,25 lần tổng bán kính) thì mỗi năm có 30% khai chiến. Hai chân linh với nhau thì chỉ 9%.
  - Tộc mạnh hơn (tổng sức chiến đấu cả tộc) là bên đánh.
  - Đánh xong cả hai nghỉ 3–7 năm.
- **Chiến trường:** nằm trên đất của bên bị đánh, ở khoảng 60% đường từ hang ổ bên đánh sang hang ổ bên bị đánh.
  - Mỗi bên xua tối đa 20 con mạnh nhất ra đó.
  - Thấy nhau trong 20 ô thì hai bên lao vào nhau.
  - Chạm nhau trong 1,8 ô thì đánh: con yếu hơn bị xé xác (30–90%) hoặc trọng thương chạy về hang.
- **Yêu vương xuất trận:** đến giữa cuộc chiến, hai vương đích thân ra chiến trường.
  - Vương thua thường được kẻ dưới cứu đi: chỉ 45% số lần chết so với yêu thú thường, chân linh chỉ 25%.
- **Kết cục:**

| Kết cục | Điều kiện | Hậu quả |
|---|---|---|
| Tộc thua mất vương | vương tử trận | yêu thú của tộc đó quỳ phục kẻ thắng |
| Tộc thua còn vương | vương sống, thua về sức | vương bỏ linh mạch, dời hang ổ ra xa 40–80 ô |
| Hòa | sức hai bên chênh nhau dưới 15% | hai bên lui binh |

- **Dấu tích:** có từ 3 con chết trở lên thì chỗ đó thành **cổ chiến trường**. Oán khí ở đó về sau có thể sinh ra hung thú, hoặc thu hút ma tu.
- **Địa danh** lấy theo đại vực ("Nam Cương", "Sa Mạc Bạo Phong"), vì thời hồng hoang chưa có làng nào để gọi tên.
- **Con non:** chừng nào chưa có tông môn, mỗi năm mỗi tộc có thêm 1–3 con non sinh ra trong lãnh địa, nên quân số hồi lại sau chiến tranh. Tộc nào đã có 45 con thì không sinh thêm.
- Yêu thú đang chinh chiến thì không đi lảng vảng, không đi đột kích làng, không rình mồi, và tu sĩ không đi săn chúng.

## Hiện trên map
- **Vòng lãnh địa:** chấm theo màu tộc quanh hang ổ. Chân linh mang màu của loài: Chân Long vàng, Thiên Phượng đỏ, Kỳ Lân cam, Côn Bằng xanh lam, Huyền Vũ ngọc. Có cờ trên hang ổ.
- **Chiến trường:** vòng đỏ đập nhịp, icon kiếm, nhãn *"Yêu tộc đại chiến: Kỳ Lân tộc – Huyền Vũ tộc"*.
- **Đại quân hành quân:** tung bụi trên đường.
- **Cảnh hai yêu thú đánh nhau** (`Fx.BeastBrawl`):
  - Mỗi bên đúng loài, đúng kích cỡ, đúng chiêu: rồng phun lửa, rắn phun độc, thú nhỏ vồ cào.
  - Hai con thật được giấu đi trong lúc cảnh đấu diễn.
- **Nhãn yêu thú trên map:** chỉ còn yêu vương và yêu thú từ thất giai trở lên (trước là từ ngũ giai), vì hồng hoang đầy yêu thú.

## Vạn thú hiện hình
- **Thú hoang:**
  - Trước: mỗi vùng 64×64 ô chỉ vẽ tối đa 5 hươu, 5 thỏ, 3 sói, mỗi con tượng trưng cho 20–30 con thật.
  - Nay: **mỗi con một token** (tối đa 40 hươu, 40 thỏ, 12 sói mỗi vùng), **chỉ vẽ ở các vùng trong camera**. Zoom xa dưới 2,5 px/ô thì tắt hẳn.
- **Yêu thú thời hồng hoang:** mở ván có 400 con (trước 150), trần 500 con.
  - Linh thú khai linh trí nhanh gấp 3. Sự kiện khai linh trí giờ chỉ hiện trên bảng tin, không ghi vào sử sách.
  - Sau khi có người, trần trở lại 160 như cũ.
- **Yêu thú đi thẳng qua hoang dã, có né vật cản, thay vì tìm đường A\*:** một đại quân vài chục con mà ngày nào cũng xin đường thì quá tốn.
  - Phần di chuyển giảm từ khoảng 47 xuống 30 ms/năm.
- **Sử sách bớt spam:** yêu thú tiến giai chỉ thành sự kiện lớn từ thất giai trở lên.

## Chạy thử (seed ThienDao, 120 năm, chỉ để xem diễn biến)
- 33 cuộc chiến trong 120 năm. Bầy thú giữ quanh 330–380 con; 5 chân linh vẫn sống.
- Một số dòng sử sách từ lần chạy trước khi thêm con non:
  - *"Long tộc thắng trận ở Nam Cương; Bắc Minh Côn Bằng bại lui, dẫn Côn Bằng tộc bỏ linh mạch."*
  - *"Côn Bằng tộc thắng trận ở Nam Cương; Thái Cổ Chân Long bại lui, dẫn Long tộc bỏ linh mạch, dời hang ổ về Âm Ma Vực."*
- Mô phỏng thời hồng hoang khoảng 73 ms/năm.

## Kết quả
- **Test EditMode:** 62/62. Test mới `YeuTocGoToWarOverTheLand`:
  - trong 30 năm có chiến tranh, có trận kết thúc, có yêu thú tử trận;
  - bầy thú vẫn trên 250 con;
  - không con nào còn "hành quân" trong một trận đã kết thúc.
- **Ảnh chụp:** đại quân Huyền Vũ tộc và Kỳ Lân tộc kéo về chiến trường; nhãn chiến trường, vòng đỏ, bụi tung lên đều hiện ra.
