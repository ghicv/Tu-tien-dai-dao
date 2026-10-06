# 00 · M0 — Bản đồ

**Ngày:** 2026-10-05 · **Commit:** `c1d66c1` (người chơi tự commit)

## Mục tiêu
Thế giới 1024×1024 ô sinh từ seed, vẽ như WorldBox (địa hình là khối ô; nhà, cây là pixel art phủ nhiều ô), có camera và brush.

## Đã làm
- **Sinh map** (`World/MapGenerator.cs`):
  - Độ cao: fBm + domain warp + ridged noise, chuẩn hóa theo phân vị (42% đất liền).
  - Sông chảy xuống dốc; BFS khoảng cách tới nước.
  - Khí hậu theo vĩ độ + độ cao, ra 16 loại địa hình.
  - Linh mạch nối các đỉnh núi, phúc địa.
  - Làng và tông môn; cây theo biome, mọc thành cụm.
- **Render** (`Render/WorldRenderer.cs`):
  - Chunk 32×32 ô, mỗi chunk là 1 `Texture2D` 256² (8 px/ô).
  - Chỉ vẽ lại chunk bị đánh dấu, ngân sách 6 ms/frame, ưu tiên gần camera.
  - Vật thể tràn sang chunk bên cạnh được vẽ đúng.
  - Texture tổng quan 1 texel/ô khi zoom xa; lớp phủ linh khí, độ cao, nhiệt, ẩm.
- **Pixel art placeholder** sinh bằng code (`Render/SpriteLibrary.cs`): viền tối, bóng đổ.
- **Camera:** kéo, zoom theo con trỏ, WASD. **Brush:** địa hình, cây, nhà, tông môn, xóa, linh khí.

## Quyết định
- Đứng yên thì đóng dấu vào texture; di chuyển thì vẽ sprite. Lý do: vẽ lại chunk mỗi frame cho thứ di chuyển quá đắt.
- Trên 1 texture 8192² thì không dùng `Texture2DArray`, vì `Apply()` sẽ upload lại toàn bộ.

## Lỗi gặp
- MCP lúc đầu nối nhầm vào project khác (Bus Simulator). Từ đó luôn kiểm tra `Application.dataPath`.
- Trùng tên `Terrain` giữa `UnityEngine.Terrain` và enum của mình, phải dùng alias.

## Kiểm chứng
Sinh map khoảng 2,1 giây, khoảng 58.000 vật thể; ảnh `Docs/m0_*.png`.
