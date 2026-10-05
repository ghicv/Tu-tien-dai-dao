using UnityEngine;

namespace ThienDao.World
{
    public static class QiCap
    {
        public const int LeyReach = 60; // cells a ley line's influence extends

        // Recomputes per-cell QiCap inside the rect from QiBase + distance to ley lines (+ water damping).
        public static void Recompute(WorldData w, int x0, int y0, int x1, int y1)
        {
            x0 = Mathf.Clamp(x0, 0, w.W - 1);
            x1 = Mathf.Clamp(x1, 0, w.W - 1);
            y0 = Mathf.Clamp(y0, 0, w.H - 1);
            y1 = Mathf.Clamp(y1, 0, w.H - 1);
            int wx0 = Mathf.Max(0, x0 - LeyReach), wx1 = Mathf.Min(w.W - 1, x1 + LeyReach);
            int wy0 = Mathf.Max(0, y0 - LeyReach), wy1 = Mathf.Min(w.H - 1, y1 + LeyReach);
            int ww = wx1 - wx0 + 1, wh = wy1 - wy0 + 1;
            var dist = new int[ww * wh];
            var queue = new int[ww * wh];
            int head = 0, tail = 0;
            for (int y = wy0; y <= wy1; y++)
            for (int x = wx0; x <= wx1; x++)
            {
                int li = (y - wy0) * ww + (x - wx0);
                if (w.LeyLine[y * w.W + x]) { dist[li] = 0; queue[tail++] = li; }
                else dist[li] = LeyReach + 1;
            }
            while (head < tail)
            {
                int li = queue[head++];
                int d = dist[li] + 1;
                if (d > LeyReach) continue;
                int lx = li % ww, ly = li / ww;
                if (lx > 0 && dist[li - 1] > d) { dist[li - 1] = d; queue[tail++] = li - 1; }
                if (lx < ww - 1 && dist[li + 1] > d) { dist[li + 1] = d; queue[tail++] = li + 1; }
                if (ly > 0 && dist[li - ww] > d) { dist[li - ww] = d; queue[tail++] = li - ww; }
                if (ly < wh - 1 && dist[li + ww] > d) { dist[li + ww] = d; queue[tail++] = li + ww; }
            }
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * w.W + x;
                float near = 1f - Mathf.Min(dist[(y - wy0) * ww + (x - wx0)], LeyReach) / (float)LeyReach;
                float v = w.QiBase[i] / (float)WorldData.MaxQi + near * near * 0.8f;
                if (TerrainInfo.IsWater(w.Terrain[i])) v *= 0.4f;
                w.QiCap[i] = (ushort)(Mathf.Clamp01(v) * WorldData.MaxQi);
            }
        }
    }
}
