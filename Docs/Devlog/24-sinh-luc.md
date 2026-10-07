# 24 · Sinh lực (máu)

**Ngày:** 2026-10-07

## Lý do
- Người chơi: *"bổ sung thêm thông số máu cho toàn bộ sinh vật ở ô thông tin"*.
- Theo quy tắc sandbox (GDD §0.0), máu phải là một phần của mô phỏng chứ không chỉ là con số để xem.

## Đã làm

### Máu thật cho tu sĩ và yêu thú
- **Máu tối đa theo cảnh giới** (`Realms.Hp`): phàm nhân 30, Luyện Khí 100, Trúc Cơ 300, Kết Đan 1.000, Nguyên Anh 3.000, Hóa Thần 10.000; mỗi tầng nhỏ cộng 10%.
- **Yêu thú** dùng mức máu của cảnh giới tương ứng; giai chẵn ×1,6, Yêu Vương và hung thú ×1,3.
- **Sức chiến đấu giảm theo vết thương:** `Strength × (0,5 + 0,5 × tỉ lệ máu)`, tức bị thương nặng thì chỉ còn một nửa sức.
- **Những gì làm mất máu:**
  - **Đấu pháp:** kẻ thắng mất 5–70% máu tùy sức đối thủ, kẻ thua bỏ chạy với 15–40% máu.
  - **Đánh yêu thú:** hai bên cùng mất máu; tu sĩ thua chạy về với 10–35%.
  - **Thiên kiếp:** vượt qua được nhưng mất 30–75% máu.
  - **Liên minh trảm yêu:** trận đánh tính bằng máu thật của hung thú. Mỗi đòn không chết làm người bị đánh mất 30–60% máu, và dưới 30% thì rút lui.
- **Hồi máu mỗi tháng:**
  - Tu sĩ ở nhà hồi 25% máu tối đa, ngoài đường hồi 6%.
  - Yêu thú trong hang hồi 12%, hung thú đang đi tàn sát chỉ hồi 4%.
- **Hành vi:**
  - Tu sĩ dưới 40% máu ở nhà dưỡng thương, không ra ngoài lịch luyện.
  - Dưới 60% máu thì không được cử đi tranh đoạt cơ duyên hay liên minh trảm yêu.
  - **Hung thú giữ vết thương giữa các trận:** liên minh thua vẫn để lại máu mất, liên minh sau dễ kết liễu hơn.
- Máu nằm trong state hash và file lưu (`-1` = đầy máu).

### Hiển thị (ô thông tin)
- **Tu sĩ và yêu thú:** chip giọt máu `máu hiện tại / tối đa`. Chip vàng khi dưới 75%, đỏ khi dưới 40%. Tooltip giải thích, ví dụ "trọng thương, ở nhà dưỡng thương; sức chiến đấu giảm", hay "hung thú hồi máu chậm".
- **Đàn thú** (hươu 40, thỏ 12, sói 60), **đoàn di dân**, **thương đội** và **làng** (phàm nhân 30): máu mỗi con hoặc mỗi người (`SpeciesInfo.Hp`).
- Biểu tượng giọt máu mới (`Icons.Blood`).

## Kết quả
- **Test EditMode:** 53/53.
- **Cân bằng (1000 năm, seed ThienDao):**
  - Phàm nhân 32,9 nghìn (30–31 nghìn ở devlog 23), tu sĩ còn sống 861, yêu thú 39.
  - Năm 1000 tốn 100 ms/năm (Editor, chế độ Debug), so với 117 trước đó.
  - Người bị thương nghỉ ở nhà, nên ít chết oan hơn.
