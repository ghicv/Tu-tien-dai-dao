# 16 · Lưu / tải thế giới, đo hiệu năng, chuột phải bỏ chọn

**Ngày:** 2026-10-07

## Lý do
- **Rà soát GDD** cho thấy thiếu lớn nhất là **Save/Load** (§16): chạy 1.000 năm, tắt game là mất. Người chơi chốt làm việc này trước tiên.
- Người chơi hỏi: *"game này sẽ rất nặng về Ram và CPU, bro check coi có đúng k"*.
- Người chơi báo: *"khi click chuột vào 1 cell thì nó hiện viền vàng ở đó luôn mà k biến mất, thêm t tính năng nháy chuột phải thì bỏ chọn"*.

## Lưu / tải (`Sim/SaveGame.cs`)
- **Cách làm: chụp toàn bộ trạng thái**, không replay log lệnh. Replay ván 1.000 năm sẽ mất vài phút.
  - Bộ serializer duyệt cả đồ thị đối tượng của `Simulation` bằng reflection và **giữ nguyên tham chiếu chung**, ví dụ hai danh sách cùng chứa một tu sĩ thì sau khi tải vẫn trỏ cùng một người.
  - Mảng dữ liệu thuần (địa hình, linh khí, chủ đất, vật thể…) được chép dạng byte thô.
  - Bỏ qua delegate và field có `[NonSerialized]`.
- **Khi tải:**
  - Dựng một `Simulation` mới từ cùng seed, để các constructor tự nối đủ đăng ký sự kiện giữa các hệ.
  - **Đổ dữ liệu đã lưu vào ngay các đối tượng đó** (cùng kiểu thì tái dùng), nên mọi liên kết sự kiện vẫn đúng.
- **Định dạng tệp:**
  - Nén gzip; có header ghi seed, tick, hash trạng thái, thời điểm lưu.
  - Có **bảng kiểu kèm danh sách field**. Tệp lưu từ phiên bản code có field khác bị **từ chối với thông báo rõ ràng**, thay vì đọc ra rác.
- **Ghi an toàn:** ghi ra tệp `.tmp` trước rồi mới thay tệp cũ, nên không bao giờ để lại tệp lưu ghi dở.
- **Số đo:** tệp khoảng 4,4 MB ở năm 0 và 6 MB ở năm 1000. Lưu khoảng 0,6 giây. Tải khoảng 2–2,4 giây, phần lớn là sinh lại map để dựng khung.
- **Trong game:**
  - Nút **Lưu / tải** ở tab Thế giới mở cửa sổ có 5 ô: Ô 1–3, Lưu nhanh, Tự động. Mỗi ô ghi thế giới nào, năm bao nhiêu, lưu lúc nào.
  - **F5** lưu nhanh, **F9** tải nhanh.
  - **Tự lưu** sau mỗi 5 phút thời gian thực, chỉ khi thế giới đang chạy.
  - Tải xong thì thế giới ở trạng thái tạm dừng.
  - Thư mục lưu: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/My project (1)/Saves`.
- **Test:**
  - `SaveAndLoadRestoresTheWholeWorld`: chạy 60 năm có lệnh, lưu rồi tải. Hash trùng, bất biến sạch. Sau đó **cả hai thế giới chạy thêm 25 năm vẫn trùng hash**, nên không sót trạng thái ẩn nào.
  - `LoadedWorldKeepsItsEventWiring`: tô đất trên thế giới vừa tải vẫn lan tới hệ cỏ và hệ thế lực.

## Lỗi tìm được khi đo: bí cảnh làm sập năm
- `RelicSystem` duyệt `All` bằng `foreach`, nhưng `Explore` có thể khiến người thám hiểm chết và sinh ra bí cảnh mới ngay trong vòng lặp. Kết quả là `InvalidOperationException`, cả bước năm bị sập.
- Lỗi chỉ lộ ra khi chạy dài (đo 1.000 năm). Đã sửa: duyệt theo chỉ số trên số lượng chốt trước, bí cảnh mới sẽ được xét từ lượt sau.

## Đo hiệu năng (`Editor/PerfProbe.cs`, kết quả ở `Docs/Perf_ThienDao.md`)
- **Đo gì:** menu Editor chạy 1.000 năm không render. Mỗi thế kỷ ghi ms/năm, bộ nhớ managed, số tu sĩ, số dòng sử sách, entity, dung lượng tệp lưu.
- **Đo chi phí từng hệ:** `Simulation` có `SystemMs` (`[NonSerialized]`), tức thời gian CPU cộng dồn của từng hệ trong `Step`.
- **RAM của mô phỏng nhẹ:**
  - Khoảng 23 MB sau khi sinh map, 30 MB ở năm 1000 (20 nghìn dòng sử sách, 8 nghìn tu sĩ từng sống).
- **RAM của phần render:**
  - Mỗi chunk địa hình là texture 256² RGBA có mipmap và giữ cả bản sao CPU, khoảng 680 KB. Bộ đệm tối đa 420 chunk, nên trường hợp xấu nhất **khoảng 290 MB**. Đây là khoản lớn nhất.
  - Ảnh tổng quan và lớp phủ khoảng 20 MB.
  - Đo trong Editor không tách được khỏi bộ nhớ của chính Editor (khoảng 1 GB), nên phải đo trong bản build mới biết con số chính xác.
- **CPU:**
  - Mỗi năm mô phỏng tốn 43 ms ở năm 100, 150 ms ở năm 1000 (≈ 0,42 ms/ngày). Chạy trên một luồng.
  - Ở x1, x5, x20 tốn không quá 5% một nhân CPU.
  - Ở **Tua** (yêu cầu 10 năm/giây) thì không theo kịp. Ngân sách 8 ms mỗi khung hình chỉ cho khoảng 3 năm/giây ở cuối game, khoảng 10 năm/giây ở đầu game.
- **Hệ tốn nhất ở năm 1000** (ms/năm):

  | Hệ thống | ms/năm | Ghi chú |
  |---|---|---|
  | Di chuyển | 44,6 | dựng lại lưới tra cứu mỗi ngày |
  | Làng | 41,8 | |
  | Tu sĩ | 29,0 | |
  | Thế lực | 9,4 | |
  | Linh khí | 6,9 | |
  | Đấu pháp | 5,9 | |

- **Vì sao càng về sau càng nặng:** chi phí tăng chậm hơn số người chết tích lũy, nhưng nhiều vòng lặp vẫn duyệt qua cả 8 nghìn người đã chết.
- **Hướng tối ưu:**
  - Danh sách tu sĩ còn sống riêng.
  - Chỉ dựng lại lưới tra cứu khi có ai cần tra.
  - Bỏ mipmap và bản sao CPU của chunk, giảm bộ đệm xuống khoảng 220 chunk.

## Chuột phải bỏ chọn
- Bấm chuột phải mà **không kéo** (lệch dưới 6 px) thì bỏ chọn. Kéo chuột phải vẫn để di chuyển camera như cũ.
- Nếu không có gì đang được chọn mà đang cầm công cụ khác, chuột phải sẽ trả về công cụ Xem.
- Esc vẫn bỏ chọn như trước.

## Kiểm chứng
- 50/50 test pass.
- Play mode: lưu vào Ô 1 rồi tải lại, hash trùng; cửa sổ Lưu / tải hiển thị đúng.
