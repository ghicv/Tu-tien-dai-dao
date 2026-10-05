using System.Collections.Generic;
using System.Threading.Tasks;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.World
{
    // Every stage draws from its own seed-derived stream, so the same seed always yields the same world.
    public static class MapGenerator
    {
        const float LandRatio = 0.42f;
        const int MaxWaterDist = 48;

        public static WorldData Generate(string seedText)
        {
            uint seed = Hash.FromString(seedText);
            var w = new WorldData(seed, seedText);
            GenerateHeight(w);
            bool[] river = CarveRivers(w);
            int[] waterDist = WaterDistance(w);
            for (int i = 0; i < waterDist.Length; i++) w.WaterDist[i] = (byte)Mathf.Min(waterDist[i], 255);
            GenerateClimate(w, waterDist);
            ClassifyTerrain(w, river, waterDist);
            GenerateQi(w);
            var noTree = new bool[w.W * w.H];
            PlaceSettlements(w, waterDist, noTree);
            PlaceVegetation(w, noTree);
            return w;
        }

        static void GenerateHeight(WorldData w)
        {
            int n = w.W;
            uint s = w.Seed;
            float[] h = w.Height;

            Parallel.For(0, n, y =>
            {
                float ny = y / (float)n;
                for (int x = 0; x < n; x++)
                {
                    float nx = x / (float)n;
                    float wx = Noise.Fbm(s + 11u, nx * 3f, ny * 3f, 3) * 0.10f;
                    float wy = Noise.Fbm(s + 17u, nx * 3f + 5.2f, ny * 3f + 1.3f, 3) * 0.10f;
                    float px = nx + wx, py = ny + wy;

                    float continent = Noise.Fbm(s + 1u, px * 2.5f, py * 2.5f, 4);
                    float detail = Noise.Fbm(s + 2u, px * 9f, py * 9f, 5);
                    float edge = Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(ny - 0.5f)) * 2f;
                    float falloff = Noise.SmoothStep(0.72f, 1f, edge);

                    float v = continent + detail * 0.22f - falloff * 0.9f;
                    float mountainMask = Noise.SmoothStep(0.08f, 0.35f, continent);
                    v += Mathf.Max(0f, Noise.Ridged(s + 3u, px * 6f, py * 6f, 4) - 0.45f) * mountainMask * 0.9f;
                    h[y * n + x] = v;
                }
            });

            // Remap by quantile so every seed gets the same land/sea and lowland/mountain proportions.
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < h.Length; i++)
            {
                if (h[i] < min) min = h[i];
                if (h[i] > max) max = h[i];
            }
            const int bins = 4096;
            var hist = new int[bins];
            float scale = (bins - 1) / Mathf.Max(1e-6f, max - min);
            for (int i = 0; i < h.Length; i++) hist[(int)((h[i] - min) * scale)]++;
            var cdf = new float[bins];
            float acc = 0f, total = h.Length;
            for (int b = 0; b < bins; b++)
            {
                cdf[b] = (acc + hist[b] * 0.5f) / total;
                acc += hist[b];
            }

            float sea = 1f - LandRatio;
            for (int i = 0; i < h.Length; i++)
            {
                float q = cdf[(int)((h[i] - min) * scale)];
                if (q < sea)
                {
                    h[i] = 0.5f * (q / sea);
                }
                else
                {
                    float ql = (q - sea) / (1f - sea);
                    h[i] = 0.5f + 0.5f * ql * ql * ql;
                }
            }
        }

        static bool[] CarveRivers(WorldData w)
        {
            int n = w.W;
            float[] h = w.Height;
            var river = new bool[n * n];
            var rng = new DetRandom(w.Seed ^ 0xA5A5A5u);
            var path = new List<int>(4096);
            var onPath = new HashSet<int>();
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            int made = 0;

            for (int attempt = 0; attempt < 2000 && made < 45; attempt++)
            {
                int sx = rng.Range(8, n - 8), sy = rng.Range(8, n - 8);
                float sh = h[sy * n + sx];
                if (sh < 0.66f || sh > 0.86f) continue;
                if (NearRiver(river, n, sx, sy, 14)) continue;

                path.Clear();
                onPath.Clear();
                int cx = sx, cy = sy;
                float uphillBudget = 0.03f;
                bool reached = false;

                for (int step = 0; step < 5000; step++)
                {
                    int ci = cy * n + cx;
                    path.Add(ci);
                    onPath.Add(ci);
                    if (h[ci] < WorldData.SeaLevel || river[ci])
                    {
                        reached = true;
                        break;
                    }

                    int best = -1;
                    float bestH = float.MaxValue;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + dx[k], ny = cy + dy[k];
                        if (nx < 1 || ny < 1 || nx >= n - 1 || ny >= n - 1) continue;
                        int ni = ny * n + nx;
                        if (onPath.Contains(ni)) continue;
                        float v = h[ni] + rng.NextFloat() * 0.004f;
                        if (v < bestH)
                        {
                            bestH = v;
                            best = k;
                        }
                    }
                    if (best < 0) break;
                    float rise = bestH - h[ci];
                    if (rise > 0f)
                    {
                        uphillBudget -= rise;
                        if (uphillBudget < 0f) break;
                    }
                    cx += dx[best];
                    cy += dy[best];
                }

                if (!reached || path.Count < 40) continue;

                bool wide = path.Count > 220;
                for (int p = 0; p < path.Count; p++)
                {
                    int ci = path[p];
                    if (h[ci] < WorldData.SeaLevel) break;
                    river[ci] = true;
                    if (wide && p > path.Count / 2 && ci % n < n - 2) river[ci + 1] = true;
                }
                made++;
            }
            return river;
        }

        static bool NearRiver(bool[] river, int n, int x, int y, int r)
        {
            for (int yy = Mathf.Max(0, y - r); yy <= Mathf.Min(n - 1, y + r); yy++)
            for (int xx = Mathf.Max(0, x - r); xx <= Mathf.Min(n - 1, x + r); xx++)
                if (river[yy * n + xx]) return true;
            return false;
        }

        // Multi-source BFS distance (cells) to sea or lake water.
        static int[] WaterDistance(WorldData w)
        {
            int n = w.W;
            var dist = new int[n * n];
            var queue = new int[n * n];
            int head = 0, tail = 0;
            for (int i = 0; i < dist.Length; i++)
            {
                if (w.Height[i] < WorldData.SeaLevel)
                {
                    dist[i] = 0;
                    queue[tail++] = i;
                }
                else dist[i] = MaxWaterDist + 1;
            }
            while (head < tail)
            {
                int i = queue[head++];
                int d = dist[i] + 1;
                if (d > MaxWaterDist) continue;
                int x = i % n, y = i / n;
                if (x > 0 && dist[i - 1] > d) { dist[i - 1] = d; queue[tail++] = i - 1; }
                if (x < n - 1 && dist[i + 1] > d) { dist[i + 1] = d; queue[tail++] = i + 1; }
                if (y > 0 && dist[i - n] > d) { dist[i - n] = d; queue[tail++] = i - n; }
                if (y < n - 1 && dist[i + n] > d) { dist[i + n] = d; queue[tail++] = i + n; }
            }
            return dist;
        }

        static void GenerateClimate(WorldData w, int[] waterDist)
        {
            int n = w.W;
            uint s = w.Seed;
            Parallel.For(0, n, y =>
            {
                float ny = y / (float)n;
                for (int x = 0; x < n; x++)
                {
                    float nx = x / (float)n;
                    int i = y * n + x;
                    float hv = w.Height[i];
                    // South is warm, north is cold; altitude cools.
                    float t = 0.12f + 0.78f * (1f - ny) + Noise.Fbm(s + 4u, nx * 4f, ny * 4f, 3) * 0.30f
                              - Mathf.Max(0f, hv - 0.55f) * 0.9f;
                    float d = Mathf.Min(waterDist[i], MaxWaterDist) / (float)MaxWaterDist;
                    float m = 0.40f + Noise.Fbm(s + 5u, nx * 5f, ny * 5f, 4) * 0.55f + (1f - d) * 0.25f;
                    w.Temperature[i] = (byte)(Mathf.Clamp01(t) * 255f);
                    w.Moisture[i] = (byte)(Mathf.Clamp01(m) * 255f);
                }
            });
        }

        static void ClassifyTerrain(WorldData w, bool[] river, int[] waterDist)
        {
            for (int i = 0; i < w.Height.Length; i++)
            {
                float hv = w.Height[i];
                float t = w.Temperature[i] / 255f;
                float m = w.Moisture[i] / 255f;
                Terrain r;

                if (hv < 0.30f) r = Terrain.DeepOcean;
                else if (hv < 0.42f) r = Terrain.Ocean;
                else if (hv < WorldData.SeaLevel) r = Terrain.Shallow;
                else if (river[i]) r = Terrain.River;
                else if (hv >= 0.91f) r = Terrain.Peak;
                else if (hv >= 0.80f) r = t < 0.22f ? Terrain.Peak : Terrain.Mountain;
                else if (hv >= 0.70f) r = t < 0.20f ? Terrain.Snow : Terrain.Hills;
                else if (waterDist[i] <= 2 && hv < 0.6f) r = t < 0.2f ? Terrain.Snow : Terrain.Beach;
                else if (t < 0.18f) r = Terrain.Snow;
                else if (t < 0.32f) r = Terrain.Tundra;
                else if (t > 0.70f) r = m < 0.38f ? Terrain.Desert : m < 0.55f ? Terrain.Savanna : Terrain.Jungle;
                else if (m > 0.74f && hv < 0.56f) r = Terrain.Swamp;
                else if (m > 0.58f) r = Terrain.Forest;
                else if (m < 0.30f) r = Terrain.Savanna;
                else r = Terrain.Grass;

                w.Terrain[i] = r;
            }
        }

        static void GenerateQi(WorldData w)
        {
            int n = w.W;
            var rng = new DetRandom(w.Seed ^ 0x51A11u);
            var nodes = new List<Vector2Int>();
            const int region = 128;

            for (int ry = 0; ry < n / region; ry++)
            for (int rx = 0; rx < n / region; rx++)
            {
                float best = 0f;
                int bx = 0, by = 0;
                for (int y = ry * region; y < (ry + 1) * region; y += 4)
                for (int x = rx * region; x < (rx + 1) * region; x += 4)
                {
                    float hv = w.Height[y * n + x];
                    if (hv > best)
                    {
                        best = hv;
                        bx = x;
                        by = y;
                    }
                }
                if (best >= 0.72f) nodes.Add(new Vector2Int(bx, by));
            }

            for (int a = 0; a < nodes.Count; a++)
            {
                int nearest = -1, second = -1;
                float d1 = float.MaxValue, d2 = float.MaxValue;
                for (int b = 0; b < nodes.Count; b++)
                {
                    if (a == b) continue;
                    float d = (nodes[a] - nodes[b]).sqrMagnitude;
                    if (d < d1) { d2 = d1; second = nearest; d1 = d; nearest = b; }
                    else if (d < d2) { d2 = d; second = b; }
                }
                if (nearest >= 0 && d1 < 320f * 320f) DrawLeyLine(w, nodes[a], nodes[nearest], ref rng);
                if (second >= 0 && d2 < 260f * 260f && rng.NextFloat() < 0.5f) DrawLeyLine(w, nodes[a], nodes[second], ref rng);
            }

            const int maxDist = 60;
            var dist = new int[n * n];
            var queue = new int[n * n];
            int head = 0, tail = 0;
            for (int i = 0; i < dist.Length; i++)
            {
                if (w.LeyLine[i]) { dist[i] = 0; queue[tail++] = i; }
                else dist[i] = maxDist + 1;
            }
            while (head < tail)
            {
                int i = queue[head++];
                int d = dist[i] + 1;
                if (d > maxDist) continue;
                int x = i % n, y = i / n;
                if (x > 0 && dist[i - 1] > d) { dist[i - 1] = d; queue[tail++] = i - 1; }
                if (x < n - 1 && dist[i + 1] > d) { dist[i + 1] = d; queue[tail++] = i + 1; }
                if (y > 0 && dist[i - n] > d) { dist[i - n] = d; queue[tail++] = i - n; }
                if (y < n - 1 && dist[i + n] > d) { dist[i + n] = d; queue[tail++] = i + n; }
            }

            uint s = w.Seed;
            var qi = new float[n * n];
            Parallel.For(0, n, y =>
            {
                for (int x = 0; x < n; x++)
                {
                    float noise = Noise.Fbm(s + 8u, x / 160f, y / 160f, 3) * 0.5f + 0.5f;
                    qi[y * n + x] = 0.06f + noise * 0.12f;
                }
            });

            // Phúc địa: rare hotspots close to ley lines.
            int spots = 0;
            for (int attempt = 0; attempt < 3000 && spots < 6; attempt++)
            {
                int x = rng.Range(20, n - 20), y = rng.Range(20, n - 20);
                int i = y * n + x;
                if (dist[i] > 10 || !TerrainInfo.IsLand(w.Terrain[i])) continue;
                const int r = 14;
                for (int yy = y - r; yy <= y + r; yy++)
                for (int xx = x - r; xx <= x + r; xx++)
                {
                    float d = Mathf.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                    if (d <= r) qi[yy * n + xx] += 0.35f * (1f - d / r);
                }
                spots++;
            }

            for (int i = 0; i < qi.Length; i++)
                w.QiBase[i] = (ushort)(Mathf.Clamp01(qi[i]) * WorldData.MaxQi);
            QiCap.Recompute(w, 0, 0, n - 1, n - 1);
        }

        static void DrawLeyLine(WorldData w, Vector2Int a, Vector2Int b, ref DetRandom rng)
        {
            int x = a.x, y = a.y;
            for (int step = 0; step < 4000 && (x != b.x || y != b.y); step++)
            {
                w.LeyLine[w.Idx(x, y)] = true;
                int ddx = b.x - x, ddy = b.y - y;
                bool moveX = rng.NextFloat() < Mathf.Abs(ddx) / (float)(Mathf.Abs(ddx) + Mathf.Abs(ddy));
                if (rng.NextFloat() < 0.22f) moveX = !moveX; // meander
                if (moveX) x += ddx != 0 ? (ddx > 0 ? 1 : -1) : (rng.NextFloat() < 0.5f ? 1 : -1);
                else y += ddy != 0 ? (ddy > 0 ? 1 : -1) : (rng.NextFloat() < 0.5f ? 1 : -1);
                x = Mathf.Clamp(x, 0, w.W - 1);
                y = Mathf.Clamp(y, 0, w.H - 1);
            }
            w.LeyLine[w.Idx(b.x, b.y)] = true;
        }

        static void PlaceSettlements(WorldData w, int[] waterDist, bool[] noTree)
        {
            int n = w.W;
            var rng = new DetRandom(w.Seed ^ 0xC0FFEEu);
            var villages = new List<Vector2Int>();

            for (int attempt = 0; attempt < 6000 && villages.Count < 40; attempt++)
            {
                int x = rng.Range(16, n - 16), y = rng.Range(16, n - 16);
                int i = y * n + x;
                Terrain t = w.Terrain[i];
                if (t != Terrain.Grass && t != Terrain.Savanna && t != Terrain.Forest) continue;
                if (waterDist[i] < 3 || waterDist[i] > 14) continue;
                if (TooClose(villages, x, y, 70)) continue;

                var site = new VillageSite { X = x, Y = y, Roof = (byte)rng.Range(0, 4) };
                int houses = rng.Range(5, 12);
                for (int k = 0; k < houses * 6 && site.Houses.Count < houses; k++)
                {
                    int ox = rng.Range(-3, 4) * 4 + rng.Range(0, 2);
                    int oy = rng.Range(-3, 4) * 4 + rng.Range(0, 2);
                    int id = TryPlaceBuilding(w, ObjectType.House, x + ox, y + oy, site.Roof, noTree);
                    if (id >= 0) site.Houses.Add(id);
                }
                if (site.Houses.Count >= 3)
                {
                    villages.Add(new Vector2Int(x, y));
                    w.VillageSites.Add(site);
                }
                else
                {
                    foreach (int id in site.Houses) w.Objects.Remove(id);
                }
            }

            var sects = new List<Vector2Int>();
            ushort qiNeeded = (ushort)(WorldData.MaxQi * 0.6f);
            for (int attempt = 0; attempt < 6000 && sects.Count < 7; attempt++)
            {
                int x = rng.Range(16, n - 16), y = rng.Range(16, n - 16);
                int i = y * n + x;
                if (w.QiCap[i] < qiNeeded) continue;
                if (TooClose(sects, x, y, 150) || TooClose(villages, x, y, 30)) continue;

                byte roof = (byte)rng.Range(0, 3);
                if (TryPlaceBuilding(w, ObjectType.SectHall, x - 2, y - 2, roof, noTree) < 0) continue;
                var site = new VillageSite { X = x, Y = y, Roof = roof, Sect = true };
                for (int k = 0; k < 12; k++)
                {
                    int ox = rng.Range(-2, 3) * 4, oy = rng.Range(-2, 3) * 4;
                    int id = TryPlaceBuilding(w, ObjectType.House, x + ox, y + oy, roof, noTree);
                    if (id >= 0) site.Houses.Add(id);
                }
                sects.Add(new Vector2Int(x, y));
                if (site.Houses.Count > 0) w.VillageSites.Add(site);
            }
        }

        static bool TooClose(List<Vector2Int> points, int x, int y, int minDist)
        {
            foreach (var p in points)
                if ((p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) < minDist * minDist) return true;
            return false;
        }

        static int TryPlaceBuilding(WorldData w, ObjectType type, int x, int y, byte variant, bool[] noTree)
        {
            int id = w.Objects.Place(type, x, y, variant);
            if (id < 0) return -1;
            int fw = ObjectInfo.FootprintW[(int)type], fh = ObjectInfo.FootprintH[(int)type];
            for (int yy = y - 1; yy <= y + fh; yy++)
            for (int xx = x - 1; xx <= x + fw; xx++)
                if (w.InBounds(xx, yy)) noTree[w.Idx(xx, yy)] = true;
            return id;
        }

        static void PlaceVegetation(WorldData w, bool[] noTree)
        {
            int n = w.W;
            uint s = w.Seed ^ 0x7EE7EEu;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (noTree[i] || w.Objects.CellObject[i] >= 0) continue;
                float clump = Noise.Fbm(w.Seed + 9u, x / 40f, y / 40f, 2) * 0.5f + 0.5f;
                float roll = Hash.Float01(s, x, y);
                ObjectType type = PickVegetation(w, i, clump, roll, Hash.U32(s + 1u, x, y));
                if (type == ObjectType.None) continue;
                w.Objects.Place(type, x, y, (byte)(Hash.U32(s + 2u, x, y) & 0xFF));
            }
        }

        static ObjectType PickVegetation(WorldData w, int i, float clump, float roll, uint pick)
        {
            float t = w.Temperature[i] / 255f;
            float dense = 0.5f + clump;
            switch (w.Terrain[i])
            {
                case Terrain.Forest:
                    if (roll < 0.42f * dense) return t < 0.42f ? ObjectType.TreePine : (pick % 4 == 0 ? ObjectType.TreeAutumn : ObjectType.TreeOak);
                    return ObjectType.None;
                case Terrain.Jungle:
                    if (roll < 0.22f * dense) return ObjectType.TreeJungle;
                    if (roll < 0.40f * dense) return ObjectType.TreeOak;
                    if (roll < 0.46f * dense) return ObjectType.Bush;
                    return ObjectType.None;
                case Terrain.Grass:
                    if (roll < 0.05f * dense) return pick % 6 == 0 ? ObjectType.TreeAutumn : ObjectType.TreeOak;
                    if (roll < 0.08f * dense) return ObjectType.Bush;
                    return ObjectType.None;
                case Terrain.Savanna:
                    if (roll < 0.02f * dense) return ObjectType.TreeOak;
                    if (roll < 0.06f * dense) return ObjectType.Bush;
                    return ObjectType.None;
                case Terrain.Swamp:
                    if (roll < 0.12f * dense) return ObjectType.TreeOak;
                    if (roll < 0.20f * dense) return ObjectType.Bush;
                    return ObjectType.None;
                case Terrain.Tundra:
                    if (roll < 0.22f * dense) return ObjectType.TreePine;
                    return ObjectType.None;
                case Terrain.Snow:
                    if (roll < 0.07f * dense) return ObjectType.TreeSnowPine;
                    return ObjectType.None;
                case Terrain.Hills:
                    if (roll < 0.14f * dense) return t < 0.45f ? ObjectType.TreePine : ObjectType.TreeOak;
                    if (roll < 0.17f * dense) return ObjectType.Rock;
                    return ObjectType.None;
                case Terrain.Mountain:
                    if (roll < 0.05f) return ObjectType.Rock;
                    if (roll < 0.08f && t > 0.3f) return ObjectType.TreePine;
                    return ObjectType.None;
                case Terrain.Desert:
                    if (roll < 0.012f) return ObjectType.Cactus;
                    if (roll < 0.017f) return ObjectType.Rock;
                    return ObjectType.None;
                case Terrain.Beach:
                    if (roll < 0.025f && t > 0.5f) return ObjectType.TreePalm;
                    return ObjectType.None;
                default:
                    return ObjectType.None;
            }
        }

        // Tree the brush plants on a cell, matching what generation would put there.
        public static ObjectType TreeFor(WorldData w, int i, uint pick)
        {
            float t = w.Temperature[i] / 255f;
            switch (w.Terrain[i])
            {
                case Terrain.Jungle: return pick % 3 == 0 ? ObjectType.TreeJungle : ObjectType.TreeOak;
                case Terrain.Snow: return ObjectType.TreeSnowPine;
                case Terrain.Tundra: return ObjectType.TreePine;
                case Terrain.Desert: return ObjectType.Cactus;
                case Terrain.Beach: return ObjectType.TreePalm;
                case Terrain.Mountain: return ObjectType.TreePine;
                case Terrain.Peak: return ObjectType.None;
                default:
                    if (t < 0.42f) return ObjectType.TreePine;
                    return pick % 4 == 0 ? ObjectType.TreeAutumn : ObjectType.TreeOak;
            }
        }
    }
}
