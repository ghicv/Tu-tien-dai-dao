# 06 · UI kiểu WorldBox

**Ngày:** 2026-10-06

## Lý do
Người chơi: *"làm lại hệ thống UI cho t nhé, đừng vứt hết lên màn hình như thế, làm hệ thống UI giống WorldBox ấy"*. UI cũ dùng IMGUI: một panel trái dài hết màn hình, thanh thời gian và bảng bên phải luôn hiện, các panel chồng lên nhau ở cửa sổ nhỏ.

## Đã làm
Bỏ toàn bộ IMGUI và chuyển sang **uGUI dựng bằng code**: `Assets/ThienDao/UI/`.
- **`Ui.cs`:**
  - Khung pixel 9-slice sinh bằng code (viền tối, vát sáng, viền dày khoảng 9 px) cho panel, nút và nút đang chọn (viền vàng).
  - Hàm dựng label (có bóng chữ), nút icon, thanh tiến độ.
  - `Tooltip`: rê chuột vào nút là hiện chú thích.
- **`Icons.cs`:** icon pixel cho toolbar.
  - Dùng lại pixel art trong game: cây, nhà, tông môn, hươu, thỏ, sói, xe di dân.
  - Ô địa hình mẫu theo đúng màu từng loại; dải màu cho lớp phủ.
  - Glyph vẽ tay: mắt, xóa, sét, sao, linh khí, linh mạch, mũi tên rót/hút, mầm linh căn, vương miện, cuộn sự kiện, biểu đồ, địa cầu, nút tốc độ, +/−, đóng.
- **`GameUI.cs`:**
  - **Đáy giữa:** thanh công cụ.
    - Nút Xem to ở bên trái.
    - 6 tab: Địa hình · Sinh linh · Linh khí · Thiên Đạo · Lớp phủ · Thế giới. Tab Thế giới có ô seed (Enter để tạo), nút Tạo thế giới, Ngẫu nhiên và bật/tắt nhãn tên.
    - Cỡ cọ +/− ở bên phải.
  - **Trên trái:** đồng hồ nhỏ gồm ngày, mùa, 5 nút tốc độ dạng icon và một dòng tóm tắt. Bên dưới là dòng tin sự kiện quan trọng, hiện 8 giây rồi mờ dần, tối đa 4 dòng.
  - **Trên phải:** 3 icon mở cửa sổ Bảng cường giả, Sự kiện, Thống kê. Mặc định đóng hết; có thể mở nhiều cửa sổ chồng dọc.
  - **Thẻ nhân vật hoặc làng** (click bằng công cụ Xem):
    - Tu sĩ: vai trò, tông môn, cảnh giới, việc đang làm, tuổi và thọ nguyên, linh căn, chỉ số, "Chuyện đời", nút Theo dõi.
    - Làng: dân số, lương thực; nếu là tông môn thì có tông chủ và số thành viên theo cảnh giới.
  - **Nhãn trên map** (canvas riêng, không chặn chuột): tên làng kèm dân số; tu sĩ từ Kết Đan đang ra ngoài.
  - **Gợi ý dưới con trỏ** khi rê lên tu sĩ hoặc làng.
- `WorldBootstrap` chỉ còn logic (sinh thế giới, input, chọn, theo dõi, tốc độ, con trỏ brush) và cung cấp dữ liệu cho UI.
- Input: `EventSystem` + `InputSystemUIInputModule`. Phím tắt giữ nguyên: Space, 1–4, Tab, [ ], Esc, F1.

## Lỗi gặp
- `GetComponent<LayoutElement>() ?? AddComponent` dính lỗi fake-null của Unity, phải kiểm tra null tường minh.
- `preferredWidth = 0` ghi đè độ rộng do layout tự tính, làm thanh công cụ không giãn → tách thành hàm chỉ đặt chiều cao.
- Dòng tin bị đè chữ khi tự xuống hàng (cố định chiều cao) → để chiều cao theo nội dung.
- Cửa sổ vừa mở thì trống → làm mới ngay lúc mở; giới hạn mỗi cửa sổ 8 mục để không tràn màn hình.

## Kiểm chứng
- Đã chạy trong Play mode: toolbar, các tab, cửa sổ, thẻ tu sĩ, thẻ làng, nhãn tên, dòng tin.
- Ảnh: `Docs/ui_overview.png`, `Docs/ui_card.png`, `Docs/ui_windows.png`. Test simulation vẫn pass.

## Còn để sau
Kéo thả cửa sổ; cuộn trong cửa sổ sự kiện; icon cho từng mùa; minimap.
