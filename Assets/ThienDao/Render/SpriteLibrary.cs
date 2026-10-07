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
            // Houses: variant = style × 8 + shape × 4 + roof (SettlementSystem.HouseVariant).
            // 0 nhà tranh of a thôn, 1 nhà ngói of a trấn, 2 nhà lầu of a thành, 3 phủ đệ of a capital.
            var roofs = new[] { C(64, 112, 204), C(196, 72, 52), C(150, 96, 56), C(52, 140, 130) };
            var straws = new[] { C(206, 172, 96), C(192, 160, 88), C(214, 184, 110), C(186, 150, 84) };
            var houses = new PixelSprite[32];
            for (int r = 0; r < 4; r++)
            {
                houses[0 + r] = Hut(straws[r], false, (uint)(50 + r));
                houses[4 + r] = Hut(straws[r], true, (uint)(60 + r));
                houses[8 + r] = House(roofs[r]);
                houses[12 + r] = LongHouse(roofs[r]);
                houses[16 + r] = Lau(roofs[r], false);
                houses[20 + r] = Lau(roofs[r], true);
                houses[24 + r] = Mansion(Glazed(roofs[r]), false);
                houses[28 + r] = Mansion(Glazed(roofs[r]), true);
            }
            s[(int)ObjectType.House] = houses;
            s[(int)ObjectType.Well] = new[] { Well() };
            s[(int)ObjectType.Shrine] = new[] { Shrine(C(170, 52, 44)), Shrine(C(52, 120, 104)), Shrine(C(170, 52, 44)), Shrine(C(120, 70, 150)) };
            s[(int)ObjectType.Market] = new[] { Market() };
            s[(int)ObjectType.Watchtower] = new[] { Watchtower() };
            s[(int)ObjectType.Pagoda] = new[] { Pagoda(C(64, 112, 204)), Pagoda(C(196, 72, 52)), Pagoda(C(120, 84, 60)), Pagoda(C(52, 140, 130)) };
            s[(int)ObjectType.Palace] = new[] { Palace() };
            // Bí cảnh: [0] sealed, [1] plundered.
            s[(int)ObjectType.RelicCave] = new[] { RelicCave(false), RelicCave(true) };
            s[(int)ObjectType.RelicRuins] = new[] { RelicRuins(false), RelicRuins(true) };
            s[(int)ObjectType.RelicTomb] = new[] { RelicTomb(false), RelicTomb(true) };
            s[(int)ObjectType.RelicAncient] = new[] { RelicAncient(false), RelicAncient(true) };
            s[(int)ObjectType.RelicTreasure] = new[] { RelicTreasure(false), RelicTreasure(true) };
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

        // ---------------------------------------------------------------- buildings by standing

        // A tiled roof from row y0 up: `rows` courses, half-width hw0 shrinking by dhw a row; a dark eave, tile
        // courses, the ridge in `ridge`; with `upturn` the eave corners flick up (đầu đao).
        static void TiledRoof(Canvas cv, float cx, int y0, int rows, float hw0, float dhw, Color32 roof, Color32 ridge, bool upturn)
        {
            var eave = Shade(roof, 0.62f);
            for (int r = 0; r < rows; r++)
            {
                float hw = hw0 - r * dhw;
                if (hw < 0.5f) break;
                bool top = r == rows - 1 || hw - dhw < 0.5f;
                for (int x = 0; x < cv.W; x++)
                {
                    float d = x + 0.5f - cx;
                    if (Mathf.Abs(d) > hw) continue;
                    var c = d < 0f ? roof : Shade(roof, 0.84f);
                    if (r == 0) c = eave;
                    else if (r % 2 == 0) c = Shade(c, 0.88f);
                    if (top && r > 0) c = ridge;
                    cv.Set(x, y0 + r, c);
                }
            }
            if (!upturn) return;
            int l = Mathf.CeilToInt(cx - hw0 - 0.5f), rt = Mathf.FloorToInt(cx + hw0 - 0.5f);
            cv.Set(l - 1, y0 + 1, eave);
            cv.Set(l - 2, y0 + 2, eave);
            cv.Set(rt + 1, y0 + 1, Shade(eave, 0.85f));
            cv.Set(rt + 2, y0 + 2, Shade(eave, 0.85f));
        }

        // Lưu ly: the glazed, brighter tiles of the capital.
        static Color32 Glazed(Color32 roof) =>
            new Color32((byte)Mathf.Min(255, roof.r * 1.18f + 14), (byte)Mathf.Min(255, roof.g * 1.18f + 14), (byte)Mathf.Min(255, roof.b * 1.18f + 14), 255);

        static void Lattice(Canvas cv, int x0, int y0, int x1, int y1, Color32 wood)
        {
            var dark = C(70, 48, 32);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                cv.Set(x, y, ((x + y) & 1) == 0 ? wood : dark);
        }

        // Nhà tranh of a thôn: mud walls under a round thatch, sometimes a haystack by the door.
        static PixelSprite Hut(Color32 straw, bool stack, uint seed)
        {
            var cv = new Canvas(24, 22);
            var mud = C(176, 138, 96);
            int x0 = stack ? 2 : 4, x1 = stack ? 15 : 19;
            cv.Rect(x0, 2, x1, 8, mud);
            cv.Rect(x0, 2, x1, 3, C(146, 110, 74));
            int dx = (x0 + x1) / 2;
            cv.Rect(dx - 1, 2, dx + 1, 6, C(92, 60, 36));
            cv.Rect(x0 + 2, 5, x0 + 3, 6, C(60, 44, 30));
            float cx = (x0 + x1 + 1) * 0.5f, hw0 = (x1 - x0 + 1) * 0.5f + 2f;
            for (int y = 8; y <= 17; y++)
            {
                float t = (y - 8) / 9.5f;
                float hw = hw0 * Mathf.Sqrt(1f - t * t);
                for (int x = 0; x < 24; x++)
                {
                    if (Mathf.Abs(x + 0.5f - cx) > hw) continue;
                    var c = x + 0.5f < cx ? straw : Shade(straw, 0.84f);
                    if (Hash.U32(seed, x, y) % 6 == 0) c = Shade(c, 0.8f); // loose straw
                    if (y == 8) c = Shade(straw, 0.68f);
                    cv.Set(x, y, c);
                }
            }
            for (int x = Mathf.CeilToInt(cx - hw0); x < cx + hw0; x++)
                if (Hash.U32(seed, x, 0) % 3 == 0) cv.Set(x, 7, Shade(straw, 0.66f)); // ragged eave
            if (stack)
            {
                var hay = C(222, 188, 100);
                for (int y = 1; y <= 7; y++)
                {
                    float t = (y - 1) / 7f;
                    float hw = 3.6f * Mathf.Sqrt(1f - t * t);
                    for (int x = 16; x < 24; x++)
                        if (Mathf.Abs(x + 0.5f - 20f) <= hw) cv.Set(x, y, (x + y) % 3 == 0 ? Shade(hay, 0.84f) : hay);
                }
            }
            cv.Outline(0.45f);
            cv.Shadow(12f, 1.2f, 11f, 1.6f);
            return cv.ToSprite(0, 0, straw);
        }

        // Nhà ngói of a trấn, the long kind: two doors under one roof, a chimney.
        static PixelSprite LongHouse(Color32 roof)
        {
            var cv = new Canvas(26, 24);
            var plaster = C(226, 206, 164);
            var beam = C(150, 104, 66);
            cv.Rect(2, 2, 23, 9, plaster);
            cv.Rect(2, 2, 23, 3, C(190, 170, 130));
            foreach (int px in new[] { 2, 12, 23 }) cv.Rect(px, 2, px, 9, beam);
            cv.Rect(5, 2, 7, 7, C(100, 62, 38));
            cv.Rect(17, 2, 19, 7, C(100, 62, 38));
            Lattice(cv, 14, 5, 15, 7, beam);
            Lattice(cv, 9, 5, 10, 7, beam);
            TiledRoof(cv, 13f, 10, 7, 12.5f, 1.1f, roof, Shade(roof, 0.55f), false);
            cv.Rect(19, 14, 20, 17, C(160, 86, 64));
            cv.Rect(19, 17, 20, 17, C(110, 60, 44));
            cv.Outline(0.45f);
            cv.Shadow(13f, 1.2f, 12f, 1.8f);
            return cv.ToSprite(-1, 0, roof);
        }

        // Nhà lầu of a thành: two storeys, a balcony, upturned eaves; the shop kind has an awning and lanterns.
        static PixelSprite Lau(Color32 roof, bool shop)
        {
            var cv = new Canvas(26, 34);
            var plaster = C(232, 214, 176);
            var wood = C(140, 92, 58);
            cv.Rect(3, 2, 22, 9, plaster);
            cv.Rect(3, 2, 22, 3, Shade(plaster, 0.85f));
            cv.Rect(3, 2, 3, 9, wood);
            cv.Rect(22, 2, 22, 9, wood);
            if (shop)
            {
                cv.Rect(5, 2, 20, 6, C(64, 46, 34));
                var goods = new[] { C(220, 70, 50), C(240, 200, 80), C(110, 180, 80), C(200, 150, 220) };
                for (int x = 6; x <= 19; x += 2) cv.Set(x, 3 + (x / 2) % 2, goods[(x / 2) % 4]);
                for (int x = 4; x <= 21; x++)
                {
                    cv.Set(x, 8, ((x / 2) & 1) == 0 ? roof : C(240, 236, 226));
                    cv.Set(x, 7, Shade(((x / 2) & 1) == 0 ? roof : C(240, 236, 226), 0.8f));
                }
            }
            else
            {
                cv.Rect(11, 2, 14, 7, C(96, 60, 38));
                Lattice(cv, 5, 5, 8, 7, wood);
                Lattice(cv, 17, 5, 20, 7, wood);
            }
            TiledRoof(cv, 13f, 10, 3, 12.5f, 0.7f, roof, Shade(roof, 0.62f), true);
            cv.Rect(5, 13, 20, 19, plaster);
            cv.Rect(5, 13, 5, 19, wood);
            cv.Rect(20, 13, 20, 19, wood);
            Lattice(cv, 8, 16, 11, 18, wood);
            Lattice(cv, 14, 16, 17, 18, wood);
            cv.Rect(4, 14, 21, 14, Shade(wood, 0.85f)); // balcony rail
            for (int x = 4; x <= 21; x += 2) cv.Set(x, 13, wood);
            TiledRoof(cv, 13f, 20, 9, 11.5f, 1.25f, roof, Shade(roof, 0.5f), true);
            if (shop)
                foreach (int lx in new[] { 2, 23 })
                {
                    cv.Rect(lx, 9, lx, 10, C(220, 56, 40));
                    cv.Set(lx, 11, C(80, 50, 30));
                }
            cv.Outline(0.45f);
            cv.Shadow(13f, 1.2f, 12f, 1.8f);
            return cv.ToSprite(-1, 0, roof);
        }

        // Phủ đệ of the capital: a white-walled compound, a red-pillared hall under glazed tiles and a gold ridge;
        // the pavilion kind has a double eave.
        static PixelSprite Mansion(Color32 glazed, bool pavilion)
        {
            var cv = new Canvas(26, 32);
            var gold = C(222, 178, 74);
            var red = C(186, 58, 44);
            var pillar = C(136, 36, 30);
            cv.Rect(4, 5, 21, 12, red);
            foreach (int px in new[] { 4, 9, 16, 21 }) cv.Rect(px, 5, px, 12, pillar);
            cv.Rect(11, 5, 14, 10, C(78, 40, 30));
            cv.Rect(11, 11, 14, 11, gold);
            if (pavilion)
            {
                TiledRoof(cv, 13f, 13, 3, 12.5f, 0.6f, glazed, Shade(glazed, 0.62f), true);
                cv.Rect(8, 16, 17, 18, red);
                cv.Rect(8, 16, 8, 18, pillar);
                cv.Rect(17, 16, 17, 18, pillar);
                TiledRoof(cv, 13f, 19, 7, 10f, 1.3f, glazed, gold, true);
            }
            else TiledRoof(cv, 13f, 13, 8, 12.5f, 1.4f, glazed, gold, true);
            // The compound wall in front, with its gate.
            var white = C(238, 234, 224);
            cv.Rect(0, 1, 25, 3, white);
            cv.Rect(0, 1, 25, 1, C(204, 198, 186));
            cv.Rect(0, 4, 25, 4, Shade(glazed, 0.6f));
            cv.Rect(11, 1, 14, 4, C(168, 40, 32));
            cv.Set(12, 2, gold);
            cv.Set(13, 2, gold);
            cv.Outline(0.45f);
            cv.Shadow(13f, 1f, 13f, 1.6f);
            return cv.ToSprite(-1, 0, glazed);
        }

        static PixelSprite Well()
        {
            var cv = new Canvas(10, 13);
            var stone = C(160, 156, 150);
            var wood = C(130, 90, 56);
            cv.Rect(1, 1, 8, 3, stone);
            cv.Rect(1, 3, 8, 3, C(196, 192, 186));
            cv.Rect(3, 3, 6, 3, C(60, 110, 170));
            cv.Rect(1, 4, 1, 8, wood);
            cv.Rect(8, 4, 8, 8, wood);
            cv.Rect(1, 8, 8, 8, wood);
            cv.Rect(5, 5, 5, 7, C(204, 184, 140));
            cv.Rect(4, 4, 5, 4, C(110, 74, 44));
            TiledRoof(cv, 5f, 9, 3, 5f, 1.4f, C(168, 124, 72), C(120, 86, 50), false);
            cv.Outline(0.5f);
            cv.Shadow(5f, 1f, 4.5f, 1.2f);
            return cv.ToSprite(-1, 0, stone);
        }

        // Miếu thổ địa: a small red temple on a stone platform, an incense burner before the steps.
        static PixelSprite Shrine(Color32 roof)
        {
            var cv = new Canvas(24, 27);
            var stone = C(178, 172, 162);
            var gold = C(222, 178, 74);
            cv.Rect(2, 1, 21, 2, stone);
            cv.Rect(2, 1, 21, 1, Shade(stone, 0.8f));
            cv.Rect(10, 0, 13, 2, C(206, 200, 190));
            cv.Rect(5, 3, 18, 10, C(186, 58, 44));
            foreach (int px in new[] { 5, 9, 14, 18 }) cv.Rect(px, 3, px, 10, C(136, 36, 30));
            cv.Rect(10, 3, 13, 8, C(70, 40, 30));
            cv.Rect(10, 9, 13, 9, gold);
            cv.Rect(11, 1, 12, 3, C(150, 110, 50));
            cv.Set(11, 4, C(200, 200, 200));
            TiledRoof(cv, 12f, 11, 7, 10.5f, 1.3f, roof, gold, true);
            cv.Rect(11, 18, 12, 19, gold);
            cv.Outline(0.45f);
            cv.Shadow(12f, 1f, 11f, 1.4f);
            return cv.ToSprite(0, 0, roof);
        }

        // Chợ: three stalls under striped awnings, their wares on the counters.
        static PixelSprite Market()
        {
            var cv = new Canvas(24, 15);
            var wood = C(150, 104, 66);
            var awnings = new[] { C(206, 60, 48), C(64, 112, 204), C(226, 184, 60) };
            var goods = new[] { C(220, 70, 50), C(240, 200, 80), C(110, 180, 80), C(240, 236, 226), C(150, 96, 56) };
            for (int k = 0; k < 3; k++)
            {
                int x0 = 1 + k * 8;
                cv.Rect(x0, 1, x0 + 6, 3, wood);
                cv.Rect(x0, 3, x0 + 6, 3, Shade(wood, 1.15f));
                for (int x = x0 + 1; x <= x0 + 5; x++) cv.Set(x, 4, goods[(x + k) % goods.Length]);
                cv.Rect(x0, 4, x0, 8, Shade(wood, 0.8f));
                cv.Rect(x0 + 6, 4, x0 + 6, 8, Shade(wood, 0.8f));
                for (int x = x0 - 1; x <= x0 + 7; x++)
                for (int y = 9; y <= 11; y++)
                {
                    var c = ((x - x0) / 2 & 1) == 0 ? awnings[k] : C(240, 236, 226);
                    cv.Set(x, y, y == 9 ? Shade(c, 0.78f) : c);
                }
            }
            cv.Outline(0.45f);
            cv.Shadow(12f, 1f, 11.5f, 1.2f);
            return cv.ToSprite(0, 0, awnings[0]);
        }

        // Tháp canh at a corner of the walls: a stone base, a wooden lookout, a red banner.
        static PixelSprite Watchtower()
        {
            var cv = new Canvas(18, 32);
            var stone = C(150, 144, 134);
            for (int y = 1; y <= 13; y++)
            for (int x = 2; x <= 15; x++)
            {
                var c = x < 9 ? stone : Shade(stone, 0.86f);
                if (y % 3 == 0 || (x + (y / 3) * 3) % 6 == 0) c = Shade(c, 0.82f); // block courses
                cv.Set(x, y, c);
            }
            var wood = C(140, 96, 60);
            cv.Rect(1, 14, 16, 20, wood);
            cv.Rect(1, 20, 16, 20, Shade(wood, 0.8f));
            foreach (int px in new[] { 4, 8, 12 }) cv.Rect(px, 16, px, 18, C(40, 28, 20));
            TiledRoof(cv, 9f, 21, 6, 9.5f, 1.6f, C(70, 74, 88), C(48, 50, 60), true);
            cv.Rect(9, 27, 9, 31, C(60, 44, 30));
            cv.Rect(10, 29, 13, 31, C(204, 48, 40));
            cv.Outline(0.45f);
            cv.Shadow(9f, 1f, 8f, 1.4f);
            return cv.ToSprite(-1, 0, stone);
        }

        // Bảo tháp: five tiers of upturned eaves and a golden spire.
        static PixelSprite Pagoda(Color32 roof)
        {
            var cv = new Canvas(20, 44);
            var plaster = C(226, 206, 164);
            var pillar = C(150, 50, 40);
            cv.Rect(2, 1, 17, 3, C(178, 172, 162));
            cv.Rect(2, 1, 17, 1, C(140, 136, 128));
            int yb = 4;
            for (int k = 0; k < 5; k++)
            {
                float hw = 5.5f - k * 0.8f;
                int x0 = Mathf.CeilToInt(10f - hw - 0.5f), x1 = Mathf.FloorToInt(10f + hw - 0.5f);
                cv.Rect(x0, yb, x1, yb + 3, plaster);
                cv.Rect(x0, yb, x0, yb + 3, pillar);
                cv.Rect(x1, yb, x1, yb + 3, pillar);
                cv.Rect(9, yb, 10, yb + 2, C(70, 44, 32));
                TiledRoof(cv, 10f, yb + 4, 2, hw + 2.5f, 1f, roof, Shade(roof, 0.6f), true);
                yb += 6;
            }
            var gold = C(226, 184, 70);
            cv.Rect(9, yb, 10, yb + 5, gold);
            cv.Rect(10, yb, 10, yb + 5, Shade(gold, 0.8f));
            cv.Set(9, yb + 6, C(250, 224, 130));
            cv.Outline(0.45f);
            cv.Shadow(10f, 1f, 8.5f, 1.4f);
            return cv.ToSprite(-2, 0, roof);
        }

        // Hoàng cung: a palace on a three-step marble terrace, a red hall under double golden roofs, two wings.
        static PixelSprite Palace()
        {
            var cv = new Canvas(60, 62);
            var marble = C(228, 226, 218);
            var red = C(186, 52, 40);
            var pillar = C(136, 34, 28);
            var imperial = C(232, 180, 56);
            var gold = C(214, 170, 70);
            var goldLight = C(250, 222, 120);

            cv.Rect(1, 1, 58, 3, marble);
            cv.Rect(1, 1, 58, 1, Shade(marble, 0.8f));
            cv.Rect(4, 4, 55, 6, marble);
            cv.Rect(4, 4, 55, 4, Shade(marble, 0.84f));
            cv.Rect(7, 7, 52, 8, marble);
            cv.Rect(7, 7, 52, 7, Shade(marble, 0.88f));
            for (int y = 1; y <= 8; y++) cv.Rect(26, y, 33, y, y % 2 == 0 ? Shade(marble, 0.86f) : C(240, 238, 232)); // the great stair

            // Wings.
            cv.Rect(0, 9, 9, 15, red);
            cv.Rect(50, 9, 59, 15, red);
            foreach (int px in new[] { 0, 4, 9, 50, 55, 59 }) cv.Rect(px, 9, px, 15, pillar);
            TiledRoof(cv, 5f, 16, 4, 6f, 1.4f, imperial, Shade(imperial, 0.6f), true);
            TiledRoof(cv, 55f, 16, 4, 6f, 1.4f, imperial, Shade(imperial, 0.6f), true);

            // The great hall.
            cv.Rect(10, 9, 49, 20, red);
            for (int px = 10; px <= 49; px += 5) cv.Rect(px, 9, px, 20, pillar);
            cv.Rect(27, 9, 32, 16, C(80, 40, 30));
            cv.Rect(17, 9, 20, 14, C(80, 40, 30));
            cv.Rect(39, 9, 42, 14, C(80, 40, 30));
            cv.Rect(26, 17, 33, 17, gold);
            cv.Rect(10, 20, 49, 20, gold);
            TiledRoof(cv, 30f, 21, 4, 26.5f, 0.7f, imperial, Shade(imperial, 0.62f), true);
            cv.Rect(16, 25, 43, 29, red);
            for (int px = 16; px <= 43; px += 6) cv.Rect(px, 25, px, 29, pillar);
            cv.Rect(16, 29, 43, 29, gold);
            TiledRoof(cv, 30f, 30, 12, 24.5f, 1.9f, imperial, goldLight, true);
            // Ridge beasts at both ends of the main ridge.
            for (int y = 61; y >= 30; y--)
            {
                int l = -1, r = -1;
                for (int x = 0; x < 60; x++)
                    if (cv.P[y * 60 + x].a == 255) { if (l < 0) l = x; r = x; }
                if (l < 0) continue;
                cv.Set(l, y + 1, C(90, 60, 30));
                cv.Set(r, y + 1, C(90, 60, 30));
                break;
            }
            cv.Outline(0.45f);
            cv.Shadow(30f, 1f, 29.5f, 1.8f);
            return cv.ToSprite(-2, 0, imperial);
        }

        // ---------------------------------------------------------------- bí cảnh

        // A rounded heap of rock, lit from the top-left (for caves and mounds).
        static void RockMound(Canvas cv, float cx, float cy, float rx, float ry, Color32 hi, Color32 mid, Color32 dark, uint seed)
        {
            for (int y = 0; y < cv.H; y++)
            for (int x = 0; x < cv.W; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                if (dx * dx + dy * dy > 1f || y < 1) continue;
                float light = -dx + dy;
                var c = light > 0.45f ? hi : light < -0.35f ? dark : mid;
                if (Hash.U32(seed, x, y) % 9 == 0) c = Shade(c, 0.85f);
                cv.Set(x, y, c);
            }
        }

        // Động phủ: a cave in a rock outcrop; sealed by a stone door with a glowing talisman, or gaping open.
        static PixelSprite RelicCave(bool plundered)
        {
            var cv = new Canvas(18, 20);
            RockMound(cv, 9f, 3f, 8.5f, 15f, C(156, 150, 142), C(118, 112, 106), C(84, 80, 78), 71);
            if (plundered)
            {
                cv.Rect(6, 1, 11, 8, C(26, 22, 22));
                cv.Rect(7, 9, 10, 9, C(26, 22, 22));
                cv.Set(4, 1, C(140, 134, 126));
                cv.Set(13, 1, C(110, 104, 98));
                cv.Set(12, 2, C(140, 134, 126));
            }
            else
            {
                cv.Rect(6, 1, 11, 9, C(96, 92, 90));
                cv.Rect(6, 1, 6, 9, C(70, 66, 64));
                cv.Rect(8, 3, 9, 7, C(240, 210, 90));   // the seal's talisman
                cv.Set(8, 5, C(200, 60, 40));
                cv.Set(9, 5, C(200, 60, 40));
            }
            cv.Outline(0.45f);
            cv.Shadow(9f, 1f, 8.5f, 1.4f);
            return cv.ToSprite(-1, 0, C(118, 112, 106));
        }

        // Di tích of a fallen sect: broken red pillars, a collapsed roof, steps leading nowhere.
        static PixelSprite RelicRuins(bool plundered)
        {
            var cv = new Canvas(26, 22);
            var stone = C(170, 164, 154);
            var red = plundered ? C(120, 60, 52) : C(150, 64, 52);
            cv.Rect(2, 1, 23, 3, stone);
            cv.Rect(2, 1, 23, 1, Shade(stone, 0.78f));
            cv.Rect(10, 0, 15, 3, Shade(stone, 1.1f));
            int[] tops = plundered ? new[] { 8, 5, 4, 9 } : new[] { 14, 10, 15, 8 };
            int[] xs = { 4, 9, 16, 21 };
            for (int k = 0; k < 4; k++)
            {
                cv.Rect(xs[k], 4, xs[k] + 1, tops[k], red);
                cv.Rect(xs[k] + 1, 4, xs[k] + 1, tops[k], Shade(red, 0.75f));
                cv.Set(xs[k] + (k % 2), tops[k] + 1, Shade(red, 0.85f)); // the broken top
            }
            // What is left of the roof: a slab of tiles slid down at an angle.
            var tile = plundered ? C(70, 76, 70) : C(52, 110, 96);
            for (int x = 3; x <= 14; x++) cv.Set(x, 15 - (x - 3) / 3, tile);
            for (int x = 4; x <= 13; x++) cv.Set(x, 16 - (x - 3) / 3, Shade(tile, 1.2f));
            if (!plundered) cv.Rect(18, 4, 19, 5, C(222, 178, 74)); // a gilded plaque in the rubble
            for (int k = 0; k < 6; k++) cv.Set(3 + k * 4, 4, Shade(stone, 0.7f));
            cv.Outline(0.45f);
            cv.Shadow(13f, 1f, 12.5f, 1.4f);
            return cv.ToSprite(-1, 0, red);
        }

        // Cổ mộ: a grassed mound with a stone stele and two guardian statues; ghost-fire hovers while it is sealed.
        static PixelSprite RelicTomb(bool plundered)
        {
            var cv = new Canvas(26, 22);
            RockMound(cv, 13f, 5f, 11f, 10f, C(118, 140, 92), C(90, 112, 72), C(64, 82, 56), 73);
            var stone = C(176, 172, 164);
            cv.Rect(11, 2, 14, 11, stone);
            cv.Rect(14, 2, 14, 11, Shade(stone, 0.8f));
            cv.Rect(10, 12, 15, 12, Shade(stone, 0.9f));
            for (int y = 4; y <= 9; y += 2) cv.Rect(12, y, 13, y, C(90, 86, 82)); // carved lines
            foreach (int gx in new[] { 3, 20 })
            {
                cv.Rect(gx, 1, gx + 2, 3, Shade(stone, 0.85f));
                cv.Rect(gx, 4, gx + 2, 6, stone);
                cv.Set(gx + 1, 7, stone);
            }
            if (plundered)
            {
                cv.Rect(6, 6, 9, 9, C(30, 26, 24)); // dug open
                cv.Set(5, 5, C(110, 96, 76));
                cv.Set(10, 5, C(110, 96, 76));
            }
            else
            {
                var ghost = C(120, 250, 200);
                cv.Set(6, 15, ghost);
                cv.Set(7, 16, ghost);
                cv.Set(19, 17, ghost);
                cv.Set(18, 16, Shade(ghost, 0.8f));
            }
            cv.Outline(0.45f);
            cv.Shadow(13f, 1f, 12.5f, 1.4f);
            return cv.ToSprite(-1, 0, C(90, 112, 72));
        }

        // Thượng cổ di tích: a colossal stone gate on a cracked plaza; its runes glow while the seal holds.
        static PixelSprite RelicAncient(bool plundered)
        {
            var cv = new Canvas(34, 36);
            var stone = C(150, 146, 156);
            var dark = C(100, 96, 110);
            for (int y = 1; y <= 4; y++)
            for (int x = 1; x <= 32; x++)
                cv.Set(x, y, (x * 7 + y * 3) % 11 == 0 ? Shade(stone, 0.75f) : (y == 1 ? Shade(stone, 0.8f) : stone));
            foreach (int px in new[] { 6, 24 })
            {
                cv.Rect(px, 5, px + 3, 27, stone);
                cv.Rect(px + 3, 5, px + 3, 27, dark);
                for (int y = 8; y <= 26; y += 4) cv.Rect(px, y, px + 3, y, Shade(stone, 0.86f));
            }
            var rune = plundered ? C(80, 76, 90) : C(190, 140, 255);
            if (plundered)
            {
                // The lintel lies broken across the plaza.
                cv.Rect(10, 5, 23, 7, dark);
                cv.Rect(10, 7, 23, 7, stone);
                cv.Rect(3, 28, 9, 30, stone);
            }
            else
            {
                cv.Rect(3, 28, 30, 31, stone);
                cv.Rect(3, 28, 30, 28, dark);
                cv.Rect(5, 32, 28, 32, Shade(stone, 1.1f));
                for (int x = 6; x <= 27; x += 3) cv.Set(x, 30, rune);
                // The portal itself: a faint violet shimmer between the pillars.
                for (int y = 5; y <= 27; y++)
                for (int x = 10; x <= 23; x++)
                    if (((x + y) & 3) == 0) cv.Set(x, y, C(120, 90, 200));
            }
            foreach (int px in new[] { 7, 25 })
                for (int y = 10; y <= 24; y += 5) cv.Set(px + 1, y, rune);
            cv.Outline(0.45f);
            cv.Shadow(17f, 1f, 16.5f, 1.6f);
            return cv.ToSprite(-1, 0, rune);
        }

        // Thiên địa linh vật on a rock altar: a glowing crystal lotus; once taken, only the empty altar stays.
        static PixelSprite RelicTreasure(bool plundered)
        {
            var cv = new Canvas(18, 22);
            var stone = C(150, 144, 136);
            cv.Rect(2, 1, 15, 4, stone);
            cv.Rect(2, 1, 15, 1, Shade(stone, 0.75f));
            cv.Rect(4, 5, 13, 6, Shade(stone, 1.12f));
            cv.Rect(4, 5, 13, 5, Shade(stone, 0.9f));
            if (!plundered)
            {
                var glow = C(255, 236, 140);
                var petal = C(250, 170, 210);
                var leaf = C(110, 200, 120);
                cv.Rect(3, 7, 6, 8, leaf);
                cv.Rect(11, 7, 14, 8, leaf);
                cv.Rect(8, 7, 9, 16, glow);
                cv.Rect(5, 9, 7, 12, petal);
                cv.Rect(10, 9, 12, 12, petal);
                cv.Rect(6, 13, 7, 14, Shade(petal, 1.1f));
                cv.Rect(10, 13, 11, 14, Shade(petal, 1.1f));
                cv.Set(4, 10, Shade(petal, 0.85f));
                cv.Set(13, 10, Shade(petal, 0.85f));
                cv.Set(8, 17, C(255, 255, 230));
                cv.Set(9, 18, C(255, 255, 230));
                cv.Set(8, 19, C(255, 250, 200));
            }
            cv.Outline(0.45f);
            cv.Shadow(9f, 1f, 7.5f, 1.2f);
            return cv.ToSprite(-5, 0, plundered ? stone : C(255, 236, 140));
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
