# 41 · Thiết kế lại lõi mô phỏng (bản nháp)

**Ngày:** 2026-10-15

## Lý do
- Bro chơi thử và thấy game *"sandbox thật, nhưng không có cảm xúc, mọi thứ rất ngẫu nhiên và không rõ ràng, không thể hiện rõ sự tu tiên"*, rồi chốt: xóa hết tài liệu cũ, giữ art 2D pixel, **làm lại từ đầu hệ thống core game**.
- Trả lời ba câu hỏi phạm vi: lõi mô phỏng và luật (giữ bản đồ và trình bày); lưu trữ rồi viết mới tại chỗ; bốn mục tiêu (bản sắc và quan hệ, nhân quả rõ, tu luyện hữu hình, ít hệ mà sâu).

## Đã làm (không đổi code)
- Xóa `GDD_ThienDao.md`, `WorldRuleSystem.md`, `Chronicle_ThienDao.md`, `Perf_ThienDao.md` và 12 ảnh cột mốc không ai dùng. Giữ Devlog.
- Gắn tag `archive/core-v1`.
- Viết `Docs/CORE_DESIGN.md`: chẩn đoán có bằng chứng, mô hình sinh linh (khí chất, đạo, tâm, ràng buộc, muốn), 8 hệ thay cho 27 mục kiểm kê, ví dụ đặc tả luật Đột phá, phương án A/B, kế hoạch C0–C7.
- Cập nhật `DECISIONS.md` và `ROADMAP.md` (thêm Phase R, Phase A phụ thuộc R).

## Số đo
- Lõi cũ: 13.053 dòng, 33 file; trình bày khoảng 11.200 dòng.
- Trình bày chạm vào lõi ở 7 file, khoảng 190 chỗ đọc.

## Chờ bro duyệt
Hướng B (đổi dần sau mặt cắt đọc) hay A; nội dung khí chất, đạo, ràng buộc; yêu thú dùng chung mô hình sinh linh hay riêng; phàm nhân đáng chú ý; bắt đầu C1.
