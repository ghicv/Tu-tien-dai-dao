using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Render
{
    public sealed class PixelSprite
    {
        public int W, H;
        public int OffX, OffY; // art-pixel offset from the footprint's bottom-left corner
        public Color32[] Px;   // a = 255 opaque, 0 < a < 255 shadow (darkens what is below)
        public Color32 MapColor;
    }

    // Placeholder pixel art built in code; swap for real sprite sheets later without touching the renderer.
    public static class SpriteLibrary
    {
        static PixelSprite[][] _sprites;

        public static PixelSprite Get(ObjectType type, int variant)
        {
            Ensure();
            var arr = _sprites[(int)type];
            return arr[variant % arr.Length];
        }

        public static int VariantCount(ObjectType type)
        {
            Ensure();
            return _sprites[(int)type].Length;
        }

        static void Ensure()
        {
            if (_sprites != null) return;
            var s = new PixelSprite[(int)ObjectType.Count][];
            var trunk = C(112, 74, 44);

            s[(int)ObjectType.None] = new[] { new Canvas(1, 1).ToSprite(0, 0, default) };

            s[(int)ObjectType.TreeOak] = new[]
            {
                Oak(C(132, 204, 82), C(86, 164, 58), C(52, 120, 44), trunk, 1),
                Oak(C(118, 196, 90), C(72, 150, 62), C(42, 108, 50), trunk, 2),
                Oak(C(150, 210, 90), C(104, 174, 60), C(66, 130, 44), trunk, 3),
            };
            s[(int)ObjectType.TreeAutumn] = new[]
            {
                Oak(C(246, 190, 86), C(226, 140, 52), C(170, 92, 38), trunk, 4),
                Oak(C(250, 214, 96), C(222, 176, 56), C(168, 124, 40), trunk, 5),
                Oak(C(236, 120, 80), C(200, 76, 52), C(140, 48, 40), trunk, 6),
            };
            s[(int)ObjectType.TreeJungle] = new[]
            {
                Jungle(C(96, 176, 78), C(50, 136, 58), C(28, 96, 48), trunk, 7),
                Jungle(C(110, 186, 70), C(62, 146, 48), C(34, 104, 40), trunk, 8),
            };
            s[(int)ObjectType.TreePine] = new[]
            {
                Pine(C(74, 150, 96), C(44, 116, 74), C(28, 84, 58), trunk, false),
                Pine(C(90, 160, 90), C(56, 126, 66), C(34, 92, 52), trunk, false),
            };
            s[(int)ObjectType.TreeSnowPine] = new[] { Pine(C(74, 150, 96), C(44, 116, 74), C(28, 84, 58), trunk, true) };
            s[(int)ObjectType.TreePalm] = new[] { Palm() };
            s[(int)ObjectType.Cactus] = new[] { Cactus() };
            s[(int)ObjectType.Bush] = new[]
            {
                Blob(9, 7, 4.5f, 3f, 3.8f, 2.6f, C(130, 200, 84), C(84, 160, 60), C(50, 116, 46), false, 11),
                Blob(9, 7, 4.5f, 3f, 3.8f, 2.6f, C(130, 200, 84), C(84, 160, 60), C(50, 116, 46), true, 12),
            };
            s[(int)ObjectType.Rock] = new[]
            {
                Blob(9, 7, 4.5f, 2.8f, 3.7f, 2.4f, C(196, 192, 186), C(150, 146, 140), C(108, 104, 100), false, 13),
                Blob(9, 7, 4.5f, 2.8f, 3.7f, 2.4f, C(186, 164, 136), C(144, 122, 98), C(102, 86, 70), false, 14),
            };
            s[(int)ObjectType.House] = new[]
            {
                House(C(64, 112, 204)),
                House(C(196, 72, 52)),
                House(C(150, 96, 56)),
                House(C(52, 140, 130)),
            };
            s[(int)ObjectType.SectHall] = new[]
            {
                SectHall(C(46, 96, 110)),
                SectHall(C(196, 146, 52)),
                SectHall(C(104, 68, 146)),
            };
            s[(int)ObjectType.TreeBamboo] = new[] { Bamboo(21), Bamboo(22), Bamboo(23) };
            s[(int)ObjectType.TreeDead] = new[] { DeadTree(false, 31), DeadTree(true, 32) };
            s[(int)ObjectType.TreePeach] = new[]
            {
                Oak(C(252, 196, 214), C(238, 140, 178), C(192, 88, 132), trunk, 41),
                Oak(C(255, 222, 232), C(246, 170, 196), C(206, 112, 150), trunk, 42),
            };
            _sprites = s;
        }

        // ---------------------------------------------------------------- units (moving things)

        public enum Unit
        {
            Deer, Rabbit, Wolf, Villager0, Villager1, Villager2, Villager3, Migrants,
            CultivatorLK, CultivatorTC, CultivatorKD, CultivatorNA, CultivatorHT, CultivatorDemonic, FlyingSword, Aura,
            Beast, Caravan,
            Count
        }

        public const int UnitFrames = 2;
        const int UnitSlot = 16;
        static PixelSprite[] _units;     // index = unit * UnitFrames + frame; then the same again as white silhouettes
        static Texture2D _unitAtlas;
        static Rect[] _unitUv;

        static int UnitCount => (int)Unit.Count * UnitFrames;

        public static PixelSprite UnitSprite(Unit u, int frame) { EnsureUnits(); return _units[(int)u * UnitFrames + frame]; }

        // white: the sprite's body as a flat white silhouette (the hit flash drawn over a unit).
        public static Rect UnitUv(Unit u, int frame, bool white = false) { EnsureUnits(); return _unitUv[(int)u * UnitFrames + frame + (white ? UnitCount : 0)]; }

        public static Texture2D UnitAtlas
        {
            get
            {
                EnsureUnits();
                if (_unitAtlas != null) return _unitAtlas;
                int cols = 4, rows = (_units.Length + cols - 1) / cols;
                int tw = cols * UnitSlot, th = rows * UnitSlot;
                var px = new Color32[tw * th];
                _unitUv = new Rect[_units.Length];
                for (int k = 0; k < _units.Length; k++)
                {
                    var sp = _units[k];
                    int ox = k % cols * UnitSlot, oy = k / cols * UnitSlot;
                    for (int y = 0; y < sp.H; y++)
                    for (int x = 0; x < sp.W; x++)
                        px[(oy + y) * tw + ox + x] = sp.Px[y * sp.W + x];
                    _unitUv[k] = new Rect(ox / (float)tw, oy / (float)th, sp.W / (float)tw, sp.H / (float)th);
                }
                _unitAtlas = new Texture2D(tw, th, TextureFormat.RGBA32, false)
                {
                    name = "UnitAtlas",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _unitAtlas.SetPixelData(px, 0);
                _unitAtlas.Apply(false);
                return _unitAtlas;
            }
        }

        static void EnsureUnits()
        {
            if (_units != null) return;
            var list = new PixelSprite[(int)Unit.Count * UnitFrames];
            for (int f = 0; f < UnitFrames; f++)
            {
                list[(int)Unit.Deer * UnitFrames + f] = Deer(f);
                list[(int)Unit.Rabbit * UnitFrames + f] = Rabbit(f);
                list[(int)Unit.Wolf * UnitFrames + f] = Wolf(f);
                list[(int)Unit.Villager0 * UnitFrames + f] = Villager(f, C(64, 112, 204));
                list[(int)Unit.Villager1 * UnitFrames + f] = Villager(f, C(196, 72, 52));
                list[(int)Unit.Villager2 * UnitFrames + f] = Villager(f, C(70, 150, 70));
                list[(int)Unit.Villager3 * UnitFrames + f] = Villager(f, C(170, 120, 60));
                list[(int)Unit.Migrants * UnitFrames + f] = MigrantCart(f);
                list[(int)Unit.CultivatorLK * UnitFrames + f] = Cultivator(f, C(120, 150, 185), C(232, 232, 242));
                list[(int)Unit.CultivatorTC * UnitFrames + f] = Cultivator(f, C(70, 165, 115), C(232, 242, 222));
                list[(int)Unit.CultivatorKD * UnitFrames + f] = Cultivator(f, C(226, 180, 70), C(252, 242, 204));
                list[(int)Unit.CultivatorNA * UnitFrames + f] = Cultivator(f, C(150, 95, 205), C(242, 222, 255));
                list[(int)Unit.CultivatorHT * UnitFrames + f] = Cultivator(f, C(234, 236, 244), C(170, 196, 240));
                list[(int)Unit.CultivatorDemonic * UnitFrames + f] = Cultivator(f, C(70, 26, 36), C(214, 40, 52));
                list[(int)Unit.FlyingSword * UnitFrames + f] = FlyingSword(f);
                list[(int)Unit.Aura * UnitFrames + f] = Aura(f);
                list[(int)Unit.Beast * UnitFrames + f] = Beast(f);
                list[(int)Unit.Caravan * UnitFrames + f] = Caravan(f);
            }
            // Every unit again as a white silhouette of its body (soft shadows left out).
            var all = new PixelSprite[list.Length * 2];
            for (int k = 0; k < list.Length; k++)
            {
                all[k] = list[k];
                var src = list[k];
                var px = new Color32[src.Px.Length];
                for (int i = 0; i < px.Length; i++)
                    if (src.Px[i].a == 255) px[i] = new Color32(255, 255, 255, 255);
                all[list.Length + k] = new PixelSprite { W = src.W, H = src.H, OffX = src.OffX, OffY = src.OffY, Px = px, MapColor = src.MapColor };
            }
            _units = all;
            if (_unitUv == null) _ = UnitAtlas;
        }

        // Yêu thú: a big hunched beast, dark coat, glowing eyes and horns, bigger than any wolf.
        static PixelSprite Beast(int frame)
        {
            var cv = new Canvas(16, 13);
            var fur = C(92, 54, 70);
            var furLight = C(140, 84, 102);
            var leg = C(64, 36, 50);
            int la = frame == 0 ? 2 : 3, lb = frame == 0 ? 10 : 9;
            cv.Rect(la, 1, la + 1, 3, leg);
            cv.Rect(lb, 1, lb + 1, 3, leg);
            cv.Rect(2, 4, 11, 7, fur);              // body
            cv.Rect(3, 7, 10, 8, furLight);         // hackles
            cv.Rect(10, 5, 14, 9, fur);             // head
            cv.Rect(13, 4, 15, 5, furLight);        // jaw
            cv.Set(14, 4, C(240, 240, 230));        // fang
            cv.Set(13, 8, C(255, 70, 50));          // eye
            cv.Set(12, 10, C(230, 210, 150));       // horns
            cv.Set(13, 11, C(230, 210, 150));
            cv.Set(11, 10, C(230, 210, 150));
            cv.Rect(0, 7, 1, 9, fur);               // tail
            cv.Set(0, 10, furLight);
            cv.Outline(0.4f);
            cv.Shadow(7.5f, 0.8f, 6.5f, 1.1f);
            return cv.ToSprite(0, 0, fur);
        }

        // Thương đội: a laden ox cart with bales and a pennant.
        static PixelSprite Caravan(int frame)
        {
            var cv = new Canvas(16, 12);
            var wood = C(150, 100, 60);
            var bale = C(214, 170, 90);
            var baleDark = C(176, 132, 64);
            var wheel = C(80, 54, 34);
            cv.Rect(1, 3, 10, 4, wood);
            cv.Rect(2, 5, 5, 7, bale);
            cv.Rect(6, 5, 9, 7, baleDark);
            cv.Rect(3, 8, 8, 9, bale);
            cv.Rect(1, 5, 1, 11, wood);             // pole
            cv.Rect(2, 10, 3, 11, C(200, 50, 50));  // pennant
            foreach (int wx in new[] { 3, 8 })
            {
                cv.Rect(wx - 1, 1, wx + 1, 2, wheel);
                cv.Set(frame == 0 ? wx : wx - 1, 2, C(190, 150, 100));
            }
            var ox = C(120, 96, 80);                // the ox
            cv.Rect(11, frame == 0 ? 1 : 2, 11, 2, C(70, 58, 46));
            cv.Rect(14, frame == 0 ? 2 : 1, 14, 2, C(70, 58, 46));
            cv.Rect(11, 3, 14, 5, ox);
            cv.Rect(14, 5, 15, 6, ox);
            cv.Set(15, 7, C(230, 220, 200));
            cv.Outline(0.45f);
            cv.Shadow(7.5f, 0.8f, 7f, 1f);
            return cv.ToSprite(0, 0, bale);
        }

        static PixelSprite Deer(int frame)
        {
            var cv = new Canvas(12, 12);
            var coat = C(156, 102, 54);
            var belly = C(200, 150, 96);
            var leg = C(110, 70, 40);
            int la = frame == 0 ? 2 : 3, lb = frame == 0 ? 7 : 6;
            cv.Rect(la, 1, la, 3, leg);
            cv.Rect(lb, 1, lb, 3, leg);
            cv.Rect(2, 4, 7, 6, coat);
            cv.Rect(3, 4, 6, 4, belly);
            cv.Rect(7, 6, 8, 7, coat);
            cv.Rect(8, 7, 9, 8, coat);
            cv.Set(9, 8, C(40, 30, 20));
            cv.Set(1, 6, C(240, 236, 226));
            var antler = C(214, 196, 160);
            cv.Set(8, 9, antler);
            cv.Set(7, 10, antler);
            cv.Set(9, 10, antler);
            cv.Outline(0.45f);
            cv.Shadow(5f, 0.8f, 4f, 1f);
            return cv.ToSprite(0, 0, coat);
        }

        static PixelSprite Rabbit(int frame)
        {
            var cv = new Canvas(8, 9);
            var fur = C(196, 178, 156);
            var furDark = C(160, 140, 120);
            int lift = frame;
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 8; x++)
            {
                float dx = (x + 0.5f - 3.4f) / 2.4f, dy = (y + 0.5f - (2.8f + lift)) / 1.6f;
                if (dx * dx + dy * dy <= 1f) cv.Set(x, y, y < 2 + lift ? furDark : fur);
            }
            cv.Rect(5, 3 + lift, 6, 4 + lift, fur);
            cv.Rect(5, 5 + lift, 5, 6 + lift, furDark);
            cv.Set(6, 4 + lift, C(40, 30, 30));
            cv.Set(1, 3 + lift, C(244, 244, 240));
            cv.Outline(0.45f);
            cv.Shadow(3.5f, 0.7f, 2.6f, 0.9f);
            return cv.ToSprite(0, 0, fur);
        }

        static PixelSprite Wolf(int frame)
        {
            var cv = new Canvas(13, 10);
            var fur = C(122, 124, 134);
            var furLight = C(170, 172, 180);
            var leg = C(90, 92, 100);
            int la = frame == 0 ? 2 : 3, lb = frame == 0 ? 8 : 7;
            cv.Rect(la, 1, la, 2, leg);
            cv.Rect(lb, 1, lb, 2, leg);
            cv.Rect(2, 3, 8, 5, fur);
            cv.Rect(3, 3, 7, 3, furLight);
            cv.Rect(8, 4, 10, 6, fur);
            cv.Set(11, 4, furLight);
            cv.Set(9, 7, fur);
            cv.Set(10, 7, fur);
            cv.Set(10, 5, C(230, 200, 60));
            cv.Rect(0, 5, 1, 6, fur);
            cv.Outline(0.45f);
            cv.Shadow(5.5f, 0.8f, 4.5f, 1f);
            return cv.ToSprite(0, 0, fur);
        }

        static PixelSprite Villager(int frame, Color32 shirt)
        {
            var cv = new Canvas(7, 11);
            var skin = C(240, 196, 150);
            var hair = C(70, 46, 30);
            var pants = C(70, 58, 46);
            if (frame == 0)
            {
                cv.Rect(2, 1, 2, 2, pants);
                cv.Rect(4, 1, 4, 2, pants);
            }
            else
            {
                cv.Rect(1, 1, 1, 2, pants);
                cv.Rect(5, 1, 5, 2, pants);
            }
            cv.Rect(1, 3, 5, 5, shirt);
            cv.Set(0, 4, skin);
            cv.Set(6, 4, skin);
            cv.Rect(2, 6, 4, 8, skin);
            cv.Rect(2, 8, 4, 8, hair);
            cv.Set(4, 7, C(40, 30, 20));
            cv.Outline(0.45f);
            cv.Shadow(3.5f, 0.7f, 2.6f, 0.9f);
            return cv.ToSprite(0, 0, shirt);
        }

        // Long-sleeved đạo bào, belt, topknot with hairpin: reads differently from a villager's tunic and trousers.
        static PixelSprite Cultivator(int frame, Color32 robe, Color32 trim)
        {
            var cv = new Canvas(9, 13);
            var robeDark = Shade(robe, 0.78f);
            var skin = C(240, 200, 158);
            var hair = C(36, 28, 30);
            cv.Rect(2, 1, 6, 2, robe);                    // flared hem
            cv.Set(frame == 0 ? 2 : 6, 1, robeDark);       // stride
            cv.Rect(3, 3, 5, 7, robe);                     // body
            cv.Rect(5, 3, 5, 7, robeDark);
            cv.Rect(2, 4, 2, 6, robe);                     // hanging sleeves
            cv.Rect(6, 4, 6, 6, robeDark);
            cv.Rect(3, 4, 5, 4, trim);                     // belt
            cv.Set(4, 7, trim);                            // collar
            cv.Rect(3, 8, 5, 9, skin);                     // face
            cv.Set(5, 9, C(30, 24, 24));                   // eye
            cv.Rect(3, 10, 5, 10, hair);
            cv.Set(3, 9, hair);
            cv.Set(4, 11, hair);                           // topknot
            cv.Set(5, 11, C(236, 196, 80));                // hairpin
            cv.Outline(0.45f);
            cv.Shadow(4.5f, 0.8f, 2.6f, 0.9f);
            return cv.ToSprite(0, 0, robe);
        }

        static PixelSprite FlyingSword(int frame)
        {
            var cv = new Canvas(14, 5);
            var blade = C(206, 228, 246);
            var gold = C(224, 176, 72);
            cv.Rect(3, 2, 12, 2, blade);
            cv.Set(13, 2, C(240, 250, 255));
            cv.Rect(2, 1, 2, 3, gold);
            cv.Rect(0, 2, 1, 2, Shade(gold, 0.8f));
            cv.Outline(0.5f);
            // Sword light flickers between frames.
            var glow = new Color32(130, 224, 255, (byte)(frame == 0 ? 120 : 70));
            for (int x = 4; x <= 11; x++)
            {
                if (cv.P[1 * cv.W + x].a == 0) cv.Set(x, 1, glow);
                if (cv.P[3 * cv.W + x].a == 0) cv.Set(x, 3, glow);
            }
            return cv.ToSprite(0, 0, blade);
        }

        // Soft halo; tinted per realm through vertex colour.
        static PixelSprite Aura(int frame)
        {
            var cv = new Canvas(16, 16);
            float radius = frame == 0 ? 7.5f : 7f;
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x + 0.5f - 8f, dy = y + 0.5f - 8f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                if (d >= 1f) continue;
                cv.Set(x, y, new Color32(255, 255, 255, (byte)(110f * Mathf.Pow(1f - d, 1.5f))));
            }
            return cv.ToSprite(0, 0, new Color32(255, 255, 255, 255));
        }

        static PixelSprite MigrantCart(int frame)
        {
            var cv = new Canvas(16, 12);
            var wood = C(150, 100, 60);
            var cloth = C(232, 222, 190);
            var clothDark = C(200, 188, 156);
            var wheel = C(80, 54, 34);
            cv.Rect(1, 3, 11, 4, wood);
            for (int y = 5; y <= 8; y++)
            for (int x = 2; x <= 10; x++)
            {
                float dx = (x + 0.5f - 6.5f) / 5f, dy = (y - 4.5f) / 4.4f;
                if (dx * dx + dy * dy <= 1f) cv.Set(x, y, x < 7 ? cloth : clothDark);
            }
            foreach (int wx in new[] { 3, 9 })
            {
                cv.Rect(wx - 1, 1, wx + 1, 2, wheel);
                cv.Set(frame == 0 ? wx : wx - 1, 2, C(190, 150, 100));
            }
            var skin = C(240, 196, 150);
            cv.Rect(13, frame == 0 ? 1 : 2, 13, 3, C(70, 58, 46));
            cv.Rect(13, 4, 14, 5, C(64, 112, 204));
            cv.Set(13, 6, skin);
            cv.Set(14, 6, skin);
            cv.Outline(0.45f);
            cv.Shadow(7.5f, 0.8f, 7f, 1f);
            return cv.ToSprite(0, 0, cloth);
        }

        static Color32 C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        public static Color32 Shade(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color32[] P;

            public Canvas(int w, int h)
            {
                W = w;
                H = h;
                P = new Color32[w * h];
            }

            public void Set(int x, int y, Color32 c)
            {
                if ((uint)x < (uint)W && (uint)y < (uint)H) P[y * W + x] = c;
            }

            public void Rect(int x0, int y0, int x1, int y1, Color32 c)
            {
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(x, y, c);
            }

            public void Outline(float k)
            {
                var src = (Color32[])P.Clone();
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (src[y * W + x].a == 255) continue;
                    if (Opaque(src, x - 1, y, out var n) || Opaque(src, x + 1, y, out n) ||
                        Opaque(src, x, y - 1, out n) || Opaque(src, x, y + 1, out n))
                        P[y * W + x] = Shade(n, k);
                }
            }

            bool Opaque(Color32[] src, int x, int y, out Color32 c)
            {
                if ((uint)x < (uint)W && (uint)y < (uint)H && src[y * W + x].a == 255)
                {
                    c = src[y * W + x];
                    return true;
                }
                c = default;
                return false;
            }

            public void Shadow(float cx, float cy, float rx, float ry)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f && P[y * W + x].a == 0) P[y * W + x] = new Color32(0, 0, 0, 110);
                }
            }

            public PixelSprite ToSprite(int offX, int offY, Color32 map) =>
                new PixelSprite { W = W, H = H, OffX = offX, OffY = offY, Px = P, MapColor = map };
        }

        // Shade level from a top-left light: 0 = highlight, 1 = mid, 2 = dark.
        static int LightLevel(float px, float py, float cx, float cy, float r, uint seed, int x, int y)
        {
            float s = (-(px - cx) + (py - cy)) / r;
            int level = s > 0.55f ? 0 : s < -0.45f ? 2 : 1;
            if (Hash.U32(seed, x, y) % 7 == 0) level = Mathf.Min(2, level + 1);
            return level;
        }

        static PixelSprite RoundTree(int w, int h, float[] blobs, int trunkX0, int trunkX1, int trunkTop,
            Color32 hi, Color32 mid, Color32 dark, Color32 trunk, int offX, uint seed)
        {
            var cv = new Canvas(w, h);
            var trunkDark = Shade(trunk, 0.8f);
            for (int y = 1; y <= trunkTop; y++)
            for (int x = trunkX0; x <= trunkX1; x++)
                cv.Set(x, y, x == trunkX1 ? trunkDark : trunk);

            var pal = new[] { hi, mid, dark };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                bool inside = false;
                for (int b = 0; b < blobs.Length; b += 3)
                {
                    float dx = px - blobs[b], dy = py - blobs[b + 1];
                    if (dx * dx + dy * dy <= blobs[b + 2] * blobs[b + 2]) { inside = true; break; }
                }
                if (!inside) continue;
                cv.Set(x, y, pal[LightLevel(px, py, blobs[0], blobs[1], blobs[2], seed, x, y)]);
            }
            cv.Outline(0.5f);
            cv.Shadow(w * 0.5f, 1.2f, w * 0.36f, 1.4f);
            return cv.ToSprite(offX, 0, mid);
        }

        static PixelSprite Oak(Color32 hi, Color32 mid, Color32 dark, Color32 trunk, uint seed) =>
            RoundTree(12, 16, new[] { 6f, 9.5f, 4.3f, 3.9f, 8f, 2.7f, 8.1f, 8f, 2.7f, 6f, 11.5f, 3.2f },
                5, 6, 5, hi, mid, dark, trunk, -2, seed);

        static PixelSprite Jungle(Color32 hi, Color32 mid, Color32 dark, Color32 trunk, uint seed) =>
            RoundTree(18, 22, new[] { 9f, 13f, 5.6f, 5.2f, 11f, 3.6f, 12.8f, 11f, 3.6f, 9f, 16.5f, 4f },
                8, 9, 7, hi, mid, dark, trunk, -1, seed);

        static PixelSprite Pine(Color32 hi, Color32 mid, Color32 dark, Color32 trunk, bool snow)
        {
            var cv = new Canvas(11, 17);
            cv.Rect(5, 1, 5, 2, trunk);
            var snowLight = C(238, 243, 248);
            var snowDark = C(196, 208, 222);
            for (int y = 3; y <= 15; y++)
            {
                int t = y - 3;
                int layer = Mathf.Min(t / 4, 2);
                int local = t - layer * 4;
                float hw = Mathf.Max(0.4f, 4.6f - layer * 1.25f - local * 0.95f);
                for (int x = 0; x < 11; x++)
                {
                    float dx = x + 0.5f - 5.5f;
                    if (Mathf.Abs(dx) > hw + 0.01f) continue;
                    bool darkSide = dx > hw * 0.35f;
                    Color32 c = dx < -hw * 0.3f ? hi : darkSide ? dark : mid;
                    if (snow && local >= 2) c = darkSide ? snowDark : snowLight;
                    cv.Set(x, y, c);
                }
            }
            cv.Outline(0.5f);
            cv.Shadow(5.5f, 1.2f, 3.5f, 1.3f);
            return cv.ToSprite(-1, 0, mid);
        }

        static PixelSprite Palm()
        {
            var cv = new Canvas(14, 17);
            var trunkA = C(150, 110, 64);
            var trunkB = C(122, 86, 50);
            for (int y = 1; y <= 10; y++)
            {
                int tx = 5 + y * 2 / 10;
                cv.Set(tx, y, y % 2 == 0 ? trunkA : trunkB);
                cv.Set(tx + 1, y, trunkB);
            }
            var hi = C(120, 196, 80);
            var mid = C(70, 150, 60);
            float cx = 7.5f, cy = 11f;
            float[] angles = { 165f, 130f, 90f, 50f, 15f, 200f, -20f };
            foreach (float a in angles)
            {
                float rad = a * Mathf.Deg2Rad;
                for (int step = 1; step <= 5; step++)
                {
                    int fx = (int)(cx + Mathf.Cos(rad) * step);
                    int fy = (int)(cy + Mathf.Sin(rad) * step - step * step * 0.09f);
                    cv.Set(fx, fy, mid);
                    cv.Set(fx, fy + 1, step < 4 ? hi : mid);
                }
            }
            cv.Set(7, 10, C(110, 70, 40));
            cv.Set(9, 10, C(110, 70, 40));
            cv.Outline(0.5f);
            cv.Shadow(7f, 1.2f, 3f, 1.2f);
            return cv.ToSprite(-3, 0, mid);
        }

        // Trúc: a clump of thin jointed stalks with feathery leaves.
        static PixelSprite Bamboo(uint seed)
        {
            var cv = new Canvas(10, 19);
            var hi = C(170, 214, 96);
            var mid = C(116, 176, 70);
            var dark = C(70, 130, 52);
            var leaf = C(96, 168, 64);
            int[] xs = { 2, 5, 7 };
            for (int k = 0; k < xs.Length; k++)
            {
                int top = 12 + (int)(Hash.U32(seed, k, 1) % 6);
                for (int y = 1; y <= top; y++)
                {
                    bool joint = (y + k) % 4 == 0;
                    cv.Set(xs[k], y, joint ? dark : (k == 1 ? hi : mid));
                }
                // Leaves fan out from the upper joints.
                for (int y = top - 6; y <= top; y += 3)
                {
                    int dir = (Hash.U32(seed, k, y) & 1) == 0 ? -1 : 1;
                    cv.Set(xs[k] + dir, y, leaf);
                    cv.Set(xs[k] + dir * 2, y + 1, leaf);
                    cv.Set(xs[k] - dir, y - 1, mid);
                }
            }
            cv.Outline(0.55f);
            cv.Shadow(5f, 1.1f, 3.6f, 1.2f);
            return cv.ToSprite(-2, 0, mid);
        }

        // Cây khô: a bare, blackened tree of the ash plains.
        static PixelSprite DeadTree(bool tall, uint seed)
        {
            var cv = new Canvas(11, tall ? 16 : 13);
            var bark = C(70, 60, 58);
            var barkHi = C(104, 92, 86);
            int top = tall ? 13 : 10;
            for (int y = 1; y <= top; y++) cv.Set(5, y, y % 3 == 0 ? barkHi : bark);
            cv.Set(6, 1, bark);
            // Branches reach up and out, crooked.
            for (int b = 0; b < 4; b++)
            {
                int y0 = top - 1 - b * 2;
                int dir = b % 2 == 0 ? -1 : 1;
                int len = 2 + (int)(Hash.U32(seed, b, 7) % 3);
                for (int s = 1; s <= len; s++) cv.Set(5 + dir * s, y0 + s / 2, s == len ? barkHi : bark);
            }
            cv.Outline(0.6f);
            cv.Shadow(5.5f, 1.1f, 3f, 1.1f);
            return cv.ToSprite(-1, 0, bark);
        }

        static PixelSprite Cactus()
        {
            var cv = new Canvas(8, 11);
            var hi = C(110, 186, 96);
            var mid = C(70, 150, 70);
            var dark = C(50, 118, 56);
            cv.Rect(3, 1, 3, 9, hi);
            cv.Rect(4, 1, 4, 9, mid);
            cv.Rect(1, 4, 1, 7, hi);
            cv.Set(2, 4, mid);
            cv.Rect(6, 5, 6, 8, dark);
            cv.Set(5, 5, mid);
            cv.Outline(0.5f);
            cv.Shadow(4f, 1f, 2.5f, 1f);
            return cv.ToSprite(0, 0, mid);
        }

        static PixelSprite Blob(int w, int h, float cx, float cy, float rx, float ry,
            Color32 hi, Color32 mid, Color32 dark, bool berries, uint seed)
        {
            var cv = new Canvas(w, h);
            var pal = new[] { hi, mid, dark };
            var berry = C(206, 52, 64);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float dx = (px - cx) / rx, dy = (py - cy) / ry;
                if (dx * dx + dy * dy > 1f) continue;
                var c = pal[LightLevel(px, py, cx, cy, Mathf.Max(rx, ry), seed, x, y)];
                if (berries && Hash.U32(seed + 99u, x, y) % 9 == 0) c = berry;
                cv.Set(x, y, c);
            }
            cv.Outline(0.55f);
            cv.Shadow(cx, 0.9f, rx + 0.2f, 1f);
            return cv.ToSprite(0, 0, mid);
        }

        static PixelSprite House(Color32 roof)
        {
            var cv = new Canvas(24, 27);
            var plaster = C(226, 206, 164);
            var plasterDark = C(190, 170, 130);
            var beam = C(150, 104, 66);
            var door = C(100, 62, 38);
            var glass = C(156, 206, 236);

            cv.Rect(3, 2, 20, 10, plaster);
            cv.Rect(3, 2, 20, 2, plasterDark);
            cv.Rect(3, 2, 3, 10, beam);
            cv.Rect(20, 2, 20, 10, beam);
            cv.Rect(10, 2, 13, 7, door);
            cv.Rect(10, 7, 13, 7, beam);
            cv.Rect(5, 5, 7, 7, glass);
            cv.Rect(16, 5, 18, 7, glass);
            cv.Set(5, 7, C(236, 246, 252));
            cv.Set(16, 7, C(236, 246, 252));

            for (int y = 11; y <= 24; y++)
            {
                float hw = 11f - (y - 11) * 0.72f;
                for (int x = 0; x < 24; x++)
                {
                    if (Mathf.Abs(x + 0.5f - 12f) > hw) continue;
                    var c = x < 12 ? roof : Shade(roof, 0.85f);
                    if (y == 11) c = Shade(roof, 0.62f);
                    else if ((y - 11) % 3 == 2) c = Shade(c, 0.82f);
                    cv.Set(x, y, c);
                }
            }
            cv.Rect(16, 17, 17, 22, C(160, 86, 64));
            cv.Rect(16, 22, 17, 22, C(120, 64, 48));

            cv.Outline(0.45f);
            cv.Shadow(12f, 1.2f, 11.5f, 1.8f);
            return cv.ToSprite(0, 0, roof);
        }

        static PixelSprite SectHall(Color32 roof)
        {
            var cv = new Canvas(40, 46);
            var stone = C(178, 172, 162);
            var stoneLight = C(206, 200, 190);
            var wall = C(178, 54, 42);
            var pillar = C(132, 36, 30);
            var door = C(74, 42, 30);
            var gold = C(214, 170, 70);
            var goldLight = C(240, 210, 110);

            cv.Rect(2, 1, 37, 5, stone);
            cv.Rect(2, 5, 37, 5, stoneLight);
            cv.Rect(2, 1, 37, 1, Shade(stone, 0.8f));
            for (int y = 1; y <= 4; y++) cv.Rect(15, y, 24, y, y % 2 == 0 ? stoneLight : stone);

            cv.Rect(6, 6, 33, 15, wall);
            foreach (int px in new[] { 6, 14, 24, 32 }) cv.Rect(px, 6, px + 1, 15, pillar);
            cv.Rect(17, 6, 22, 12, door);
            cv.Rect(16, 6, 16, 13, gold);
            cv.Rect(23, 6, 23, 13, gold);
            cv.Rect(16, 13, 23, 13, gold);

            for (int y = 15; y <= 21; y++)
            {
                float hw = 18.5f - (y - 15) * 1.7f;
                for (int x = 0; x < 40; x++)
                {
                    if (Mathf.Abs(x + 0.5f - 20f) > hw) continue;
                    var c = x < 20 ? roof : Shade(roof, 0.86f);
                    if (y == 15) c = gold;
                    else if (y == 21) c = goldLight;
                    else if ((y - 15) % 2 == 1) c = Shade(c, 0.85f);
                    cv.Set(x, y, c);
                }
            }
            cv.Rect(1, 16, 2, 16, roof);
            cv.Rect(37, 16, 38, 16, Shade(roof, 0.86f));
            cv.Set(1, 17, roof);
            cv.Set(38, 17, Shade(roof, 0.86f));

            cv.Rect(12, 22, 27, 27, wall);
            foreach (int px in new[] { 12, 19, 26 }) cv.Rect(px, 22, px + 1, 27, pillar);
            cv.Rect(16, 24, 17, 25, goldLight);
            cv.Rect(22, 24, 23, 25, goldLight);

            for (int y = 27; y <= 33; y++)
            {
                float hw = 13f - (y - 27) * 1.6f;
                for (int x = 0; x < 40; x++)
                {
                    if (Mathf.Abs(x + 0.5f - 20f) > hw) continue;
                    var c = x < 20 ? roof : Shade(roof, 0.86f);
                    if (y == 27) c = gold;
                    else if ((y - 27) % 2 == 1) c = Shade(c, 0.85f);
                    cv.Set(x, y, c);
                }
            }
            cv.Rect(6, 28, 8, 28, roof);
            cv.Rect(31, 28, 33, 28, Shade(roof, 0.86f));
            cv.Set(6, 29, roof);
            cv.Set(33, 29, Shade(roof, 0.86f));

            cv.Rect(19, 34, 19, 39, gold);
            cv.Rect(20, 34, 20, 39, Shade(gold, 0.8f));
            cv.Rect(19, 40, 20, 41, goldLight);

            cv.Outline(0.45f);
            cv.Shadow(20f, 1f, 19.5f, 1.6f);
            return cv.ToSprite(0, 0, roof);
        }
    }
}
