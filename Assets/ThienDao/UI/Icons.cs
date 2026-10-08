using System.Collections.Generic;
using ThienDao.Render;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;
using Unit = ThienDao.Render.SpriteLibrary.Unit;

namespace ThienDao.UI
{
    // Pixel icons for the toolbar, reusing the game's own pixel art where it exists.
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Sprite Make(string key, int w, int h, Color32[] px)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Icon_" + key };
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            Cache[key] = s;
            return s;
        }

        // A world object's sprite, centred on a square canvas.
        public static Sprite Object(ObjectType type, int variant = 0)
        {
            var ps = SpriteLibrary.Get(type, variant);
            return FromPixelSprite("obj" + type + variant, ps);
        }

        public static Sprite Unit(Unit unit)
        {
            var ps = SpriteLibrary.UnitSprite(unit, 0);
            return FromPixelSprite("unit" + unit, ps);
        }

        static Sprite FromPixelSprite(string key, PixelSprite ps)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int size = Mathf.Max(ps.W, ps.H);
            var px = new Color32[size * size];
            int ox = (size - ps.W) / 2, oy = (size - ps.H) / 2;
            for (int y = 0; y < ps.H; y++)
            for (int x = 0; x < ps.W; x++)
            {
                var c = ps.Px[y * ps.W + x];
                if (c.a > 0 && c.a < 255) c = new Color32(0, 0, 0, 90); // shadows read as soft shadow on buttons too
                px[(y + oy) * size + x + ox] = c;
            }
            return Make(key, size, size, px);
        }

        // A bevelled 12×12 tile of terrain, like the map's own cells.
        public static Sprite TerrainTile(Terrain t)
        {
            const int s = 12;
            var baseC = TerrainInfo.Colors[(int)t];
            var px = new Color32[s * s];
            var rng = new System.Random((int)t * 977);
            bool water = TerrainInfo.IsWater(t);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float f = 1f + ((float)rng.NextDouble() - 0.5f) * 0.12f;
                if (!water && y >= s - 1) f *= 1.15f;
                if (!water && y <= 1) f *= 0.7f;
                if (water && (y + x / 3) % 4 == 0) f *= 1.12f;
                px[y * s + x] = SpriteLibrary.Shade(baseC, f);
            }
            if (t == Terrain.Mountain || t == Terrain.Hills || t == Terrain.Peak)
            {
                var rock = t == Terrain.Hills ? new Color32(96, 132, 60, 255) : new Color32(150, 140, 130, 255);
                for (int y = 2; y < 10; y++)
                for (int x = 0; x < s; x++)
                    if (Mathf.Abs(x - 6) <= 9 - y) px[y * s + x] = x < 6 ? SpriteLibrary.Shade(rock, 1.15f) : rock;
                if (t != Terrain.Hills)
                    for (int y = 7; y < 10; y++)
                    for (int x = 0; x < s; x++)
                        if (Mathf.Abs(x - 6) <= 9 - y) px[y * s + x] = new Color32(240, 244, 250, 255);
            }
            return Make("terrain" + t, s, s, px);
        }

        // Horizontal gradient chip for an overlay mode.
        public static Sprite Gradient(string key, Color32 a, Color32 b)
        {
            const int s = 12;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool edge = x == 0 || y == 0 || x == s - 1 || y == s - 1;
                px[y * s + x] = edge ? new Color32(20, 20, 26, 255) : Color32.Lerp(a, b, x / (float)(s - 1));
            }
            return Make("grad" + key, s, s, px);
        }

        // Hand-drawn glyphs. Rows are top-to-bottom; each char maps to a palette colour, '.' is clear.
        static Sprite Glyph(string key, string[] rows, Dictionary<char, Color32> palette)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            int h = rows.Length, w = rows[0].Length;
            var px = new Color32[w * h];
            for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                char ch = rows[r][x];
                if (ch != '.' && palette.TryGetValue(ch, out var c)) px[(h - 1 - r) * w + x] = c;
            }
            return Make(key, w, h, px);
        }

        static readonly Dictionary<char, Color32> P = new Dictionary<char, Color32>
        {
            { 'k', new Color32(18, 18, 24, 255) },
            { 'w', new Color32(244, 244, 240, 255) },
            { 'y', new Color32(255, 220, 90, 255) },
            { 'o', new Color32(240, 150, 50, 255) },
            { 'c', new Color32(120, 230, 255, 255) },
            { 'b', new Color32(70, 130, 220, 255) },
            { 'p', new Color32(170, 110, 240, 255) },
            { 'r', new Color32(220, 60, 60, 255) },
            { 'g', new Color32(110, 200, 90, 255) },
            { 's', new Color32(150, 150, 160, 255) },
            { 'n', new Color32(150, 100, 60, 255) },
        };

        public static Sprite Eye => Glyph("eye", new[]
        {
            "............",
            "...kkkkkk...",
            "..kwwwwwwk..",
            ".kwwkbbkwwk.",
            "kwwkbbbbkwwk",
            "kwwkbkkbkwwk",
            "kwwkbbbbkwwk",
            ".kwwkbbkwwk.",
            "..kwwwwwwk..",
            "...kkkkkk...",
            "............",
            "............",
        }, P);

        public static Sprite Erase => Glyph("erase", new[]
        {
            "kk........kk",
            "krk......krk",
            ".krk....krk.",
            "..krk..krk..",
            "...krkkrk...",
            "....krrk....",
            "....krrk....",
            "...krkkrk...",
            "..krk..krk..",
            ".krk....krk.",
            "krk......krk",
            "kk........kk",
        }, P);

        public static Sprite Bolt => Glyph("bolt", new[]
        {
            "......kkkk..",
            ".....kyyyk..",
            "....kyyyk...",
            "...kyyyk....",
            "..kyyyykkk..",
            "..kyyyyyyk..",
            "...kkkyyk...",
            ".....kyyk...",
            "....kyyk....",
            "....kyk.....",
            "...kyk......",
            "...kk.......",
        }, P);

        public static Sprite Star => Glyph("star", new[]
        {
            ".....kk.....",
            ".....yy.....",
            "....kyyk....",
            "kkkkyyyykkkk",
            ".kyyyyyyyyk.",
            "..kyyyyyyk..",
            "...kyyyyk...",
            "..kyyyyyyk..",
            "..kyyk.kyyk.",
            ".kyk....kyk.",
            ".kk......kk.",
            "............",
        }, P);

        public static Sprite Orb => Glyph("orb", new[]
        {
            "....kkkk....",
            "..kkccccekk.".Replace('e', 'c'),
            ".kccwwccccck",
            ".kcwwcccccck",
            "kcccccccccck",
            "kcccccccccck",
            "kccccccccbck",
            "kcccccccbbck",
            ".kccccccbbk.",
            ".kkccccbbkk.",
            "...kkkkkk...",
            "............",
        }, P);

        public static Sprite LeyLine => Glyph("ley", new[]
        {
            "............",
            "..........cc",
            ".........cwc",
            "........cwc.",
            ".......cwc..",
            "..cc..cwc...",
            ".cwwccwc....",
            "cwc.cwc.....",
            "cc..cc......",
            "............",
            "............",
            "............",
        }, P);

        public static Sprite LeyBreak => Glyph("leybreak", new[]
        {
            "............",
            "..........cc",
            ".........cwc",
            "....r...cwc.",
            "....rr.cwc..",
            "..cc.rr.c...",
            ".cwwc.rr....",
            "cwc.....r...",
            "cc..........",
            "............",
            "............",
            "............",
        }, P);

        public static Sprite QiUp => Glyph("qiup", new[]
        {
            ".....kk.....",
            "....kcck....",
            "...kcwwck...",
            "..kcwccwck..",
            ".kkkcwwckkk.",
            "....cwwc....",
            "....cwwc....",
            "....cwwc....",
            "...cccccc...",
            "..cc.cc.cc..",
            ".cc..cc..cc.",
            "............",
        }, P);

        public static Sprite QiDown => Glyph("qidown", new[]
        {
            ".pp..pp..pp.",
            "..pp.pp.pp..",
            "...pppppp...",
            "....pwwp....",
            "....pwwp....",
            "....pwwp....",
            ".kkkpwwpkkk.",
            "..kpwppwpk..",
            "...kpwwpk...",
            "....kppk....",
            ".....kk.....",
            "............",
        }, P);

        public static Sprite Seed => Glyph("seed", new[]
        {
            "...y....y...",
            "....y..y....",
            ".y...kk...y.",
            "..y.kggk.y..",
            "....kggk....",
            "...kggggk...",
            "...kgwggk...",
            "...kggggk...",
            "....kggk....",
            "..y.knnk.y..",
            ".y...kk...y.",
            "....y..y....",
        }, P);

        public static Sprite Crown => Glyph("crown", new[]
        {
            "............",
            "k....k....k.",
            "ky..kyk..ky.",
            "kyy.kyk.kyy.",
            "kyyykyyykyy.",
            "kyyyyyyyyyy.",
            "kyyryyyryyy.",
            "kyyyyyyyyyy.",
            "kkkkkkkkkkk.",
            "kooooooooook".Substring(0, 12),
            "kkkkkkkkkkkk",
            "............",
        }, P);

        // Jade slip being written to: lưu / tải thế giới.
        public static Sprite SaveSlip => Glyph("saveslip", new[]
        {
            "............",
            "..kkkkkkkk..",
            "..kggggggk..",
            "..kgkkkkgk..",
            "..kggggggk..",
            "..kgkkkkgk..",
            "..kggggggk..",
            "..kgkkkkgk..",
            "..kggggggk..",
            "..kkkkkkkk..",
            "....kyyk....",
            ".....kk.....",
        }, P);

        // Bound history book: the Biên niên sử window.
        public static Sprite Book => Glyph("book", new[]
        {
            "............",
            ".kkkkkkkkkk.",
            ".knnnnnnnnk.",
            ".knyyyyyynk.",
            ".knykkkkynk.",
            ".knyyyyyynk.",
            ".knnnnnnnnk.",
            ".knnnyynnnk.",
            ".knnnnnnnnk.",
            ".kwwwwwwwwk.",
            ".kkkkkkkkkk.",
            "............",
        }, P);

        // Sect banner on a pole: the Thế lực window.
        public static Sprite Banner => Glyph("banner", new[]
        {
            ".kk.........",
            ".kskkkkkkk..",
            ".ksrrrrrrrk.",
            ".ksrryyrrrk.",
            ".ksrryyyrrk.",
            ".ksrrrryrrk.",
            ".ksrrrrrrk..",
            ".ksrrrrrk...",
            ".kskkkkk....",
            ".ks.........",
            ".ks.........",
            ".kk.........",
        }, P);

        public static Sprite Scroll => Glyph("scroll", new[]
        {
            ".kkkkkkkkkk.",
            "knnwwwwwwnnk",
            ".kwwwwwwwwk.",
            ".kwkkkkkkwk.",
            ".kwwwwwwwwk.",
            ".kwkkkkwwwk.",
            ".kwwwwwwwwk.",
            ".kwkkkkkkwk.",
            ".kwwwwwwwwk.",
            "knnwwwwwwnnk",
            ".kkkkkkkkkk.",
            "............",
        }, P);

        public static Sprite Chart => Glyph("chart", new[]
        {
            "............",
            "k.........y.",
            "k........yk.",
            "k.......y.k.",
            "k..g...y..k.",
            "k..g..gy..k.",
            "k..g.ggb..k.",
            "k.bg.ggb.bk.",
            "k.bgbggbbbk.",
            "k.bgbggbbbk.",
            "kkkkkkkkkkk.",
            "............",
        }, P);

        public static Sprite Globe => Glyph("globe", new[]
        {
            "...kkkkkk...",
            "..kbbggbbk..",
            ".kbbgggbbbk.",
            "kbggbbbggbbk",
            "kbgbbbbgggbk",
            "kbbbbbbbggbk",
            "kbbggbbbbbbk",
            "kbgggbbbbgbk",
            ".kbggbbbggk.",
            "..kbbbbbbk..",
            "...kkkkkk...",
            "............",
        }, P);

        public static Sprite Layers => Glyph("layers", new[]
        {
            "............",
            ".....kk.....",
            "...kkcckk...",
            ".kkccccccckk",
            "kpkkccccckkp",
            "kppkkcckkppk",
            ".kpppkkpppk.",
            "kgkpppppkkgk",
            "kggkkppkkggk",
            ".kgggkkgggk.",
            "...kkggkk...",
            ".....kk.....",
        }, P);

        // ---------------------------------------------------------------- M6: thiên kiếp, thiên tai

        public static Sprite Tribulation => Glyph("tribulation", new[]
        {
            "...kkkk.....",
            "..kssssk.kk.",
            ".kssssssksk.",
            "kssssssssssk",
            "kssssssssssk",
            ".kkkkpkkpkk.",
            "....kpk.kpk.",
            "...kppk.kpk.",
            "...kpk..kk..",
            "..kpk.......",
            "..kpk.......",
            "..kk........",
        }, P);

        public static Sprite Quake => Glyph("quake", new[]
        {
            "............",
            ".s........s.",
            "..s..ss..s..",
            "............",
            "nnnnnknnnnnn",
            "nnnnkknnnnnn",
            "nnnnnkknnnnn",
            "nnnnnnkknnnn",
            "nnnnnkknnnnn",
            "nnnnkknnnnnn",
            "nnnnnknnnnnn",
            "kkkkkkkkkkkk",
        }, P);

        public static Sprite Volcano => Glyph("volcano", new[]
        {
            "....o..y....",
            "...y.oo.o...",
            ".....rr.....",
            "....kook....",
            "....krrk....",
            "...knrrnk...",
            "...knnrnk...",
            "..knnnrnnk..",
            "..knnnnrnk..",
            ".knnnnnnnnk.",
            ".knnnnnnnnk.",
            "kkkkkkkkkkkk",
        }, P);

        public static Sprite Wave => Glyph("wave", new[]
        {
            "............",
            "..bb....bb..",
            ".bwwb..bwwb.",
            "bb..bbbb..bb",
            "............",
            "..bb....bb..",
            ".bccb..bccb.",
            "bb..bbbb..bb",
            "............",
            "..bb....bb..",
            ".bccb..bccb.",
            "bb..bbbb..bb",
        }, P);

        public static Sprite Sun => Glyph("sun", new[]
        {
            "y....y....y.",
            ".y..yyy..y..",
            "...yyyyy....",
            "..yyyoyyy...",
            "...yyyyy....",
            ".y..yyy..y..",
            "y....y....y.",
            "............",
            "oooookoooooo",
            "ooookkoooooo",
            "oooooookoooo",
            "kkkkkkkkkkkk",
        }, P);

        public static Sprite Skull => Glyph("skull", new[]
        {
            "...kkkkkk...",
            "..kwwwwwwk..",
            ".kwwwwwwwwk.",
            ".kwkkwwkkwk.",
            ".kwkkwwkkwk.",
            ".kwwwkkwwwk.",
            "..kwwwwwwk..",
            "...kwkwkk...",
            "...kkkkkk...",
            "..g..g...g..",
            ".g..g...g...",
            "g..g...g....",
        }, P);

        public static Sprite Paw => Glyph("paw", new[]
        {
            "............",
            "...kk..kk...",
            "..krrkkrrk..",
            "..krrkkrrk..",
            "kk.kk..kk.kk",
            "krrk....krrk",
            "krrk.kk.krrk",
            ".kk.krrk.kk.",
            "...krrrrk...",
            "..krrrrrrk..",
            "..krrrrrrk..",
            "...kkkkkk...",
        }, P);

        // ---------------------------------------------------------------- M6 phần 2: thời tiết, đại kiếp, quy luật, sinh tử

        public static Sprite RainCloud => Glyph("raincloud", new[]
        {
            "...kkkk.....",
            "..kwwwwk.kk.",
            ".kwwwwwwkwk.",
            "kwwwwwwwwwwk",
            "kwwwwwwwwwwk",
            ".kkkkkkkkkk.",
            "..b..b..b...",
            ".b..b..b....",
            "............",
            "...b..b..b..",
            "..b..b..b...",
            "............",
        }, P);

        public static Sprite Storm => Glyph("storm", new[]
        {
            "...ssssss...",
            "..s......s..",
            ".s..cccc..s.",
            "s..c....c...",
            "s.c..ss..c..",
            "s.c.s..s.c..",
            "s.c.s.....c.",
            "s.c..sss..c.",
            ".s.c.....c..",
            "..s.ccccc...",
            "...s........",
            "....ssss....",
        }, P);

        public static Sprite Snowflake => Glyph("snowflake", new[]
        {
            ".....c......",
            "...c.c.c....",
            "....ccc.....",
            ".c..ccc..c..",
            "..c..c..c...",
            "ccccccccccc.",
            "..c..c..c...",
            ".c..ccc..c..",
            "....ccc.....",
            "...c.c.c....",
            ".....c......",
            "............",
        }, P);

        public static Sprite Eclipse => Glyph("eclipse", new[]
        {
            "....o..o....",
            ".o..oooo..o.",
            "..ookkkkoo..",
            "..okkkkkko..",
            "ookkkkkkkkoo",
            ".okkkkkkkko.",
            ".okkkkkkkko.",
            "ookkkkkkkkoo",
            "..okkkkkko..",
            "..ookkkkoo..",
            ".o..oooo..o.",
            "....o..o....",
        }, P);

        public static Sprite Scales => Glyph("scales", new[]
        {
            ".....kk.....",
            "kkkkkyykkkkk",
            "ky...yy...yk",
            "ky...yy...yk",
            "yyy..yy..yyy",
            "kyyk.yy.kyyk",
            ".kk..yy..kk.",
            ".....yy.....",
            ".....yy.....",
            "...kkyykk...",
            "..kyyyyyyk..",
            "..kkkkkkkk..",
        }, P);

        public static Sprite Revive => Glyph("revive", new[]
        {
            "...kkkkkk...",
            "..kggggggk..",
            ".kgggwwgggk.",
            "kgggwwwwgggk",
            "kggwwwwwwggk",
            "kggggwwggggk",
            "kggggwwggggk",
            "kggggwwggggk",
            ".kgggwwgggk.",
            "..kggggggk..",
            "...kkkkkk...",
            "............",
        }, P);

        public static Sprite Wrath => Glyph("wrath", new[]
        {
            "rr........rr",
            ".rr......rr.",
            "..rr.kk.rr..",
            "...rrkkrr...",
            "....rrrr....",
            "...krrrrk...",
            "..kkrrrrkk..",
            ".kkrrkkrrkk.",
            "..rrssssrr..",
            ".rr.ssss.rr.",
            "rr..ssss..rr",
            "kkkkkkkkkkkk",
        }, P);

        // ---------------------------------------------------------------- UI: stat chips and seasons

        public static Sprite Hourglass => Glyph("hourglass", new[]
        {
            ".kkkkkkkkkk.",
            ".knnnnnnnnk.",
            "..kyyyyyyk..",
            "...kyyyyk...",
            "....kyyk....",
            ".....kk.....",
            ".....kk.....",
            "....k..k....",
            "...k.yy.k...",
            "..kyyyyyyk..",
            ".knnnnnnnnk.",
            ".kkkkkkkkkk.",
        }, P);

        public static Sprite Heart => Glyph("heart", new[]
        {
            "............",
            "..kk....kk..",
            ".krrk..krrk.",
            "krwrrkkrrrrk",
            "krrrrrrrrrrk",
            "krrrrrrrrrrk",
            ".krrrrrrrrk.",
            "..krrrrrrk..",
            "...krrrrk...",
            "....krrk....",
            ".....kk.....",
            "............",
        }, P);

        // Bất mãn, khởi nghĩa: a burning torch raised high.
        public static Sprite Torch => Glyph("torch", new[]
        {
            ".....r......",
            "....ryr.....",
            "...ryyor....",
            "...royyr....",
            "....rwor....",
            ".....kk.....",
            ".....nk.....",
            ".....nk.....",
            ".....nk.....",
            ".....nk.....",
            ".....nk.....",
            "......k.....",
        }, P);

        // Ngọc giản (công pháp): jade slips bound with a red cord, a glyph glowing on the top one.
        public static Sprite JadeSlip => Glyph("jadeslip", new[]
        {
            "............",
            "..kkkkkkkk..",
            ".kgggggggwk.",
            ".kgwgggyggk.",
            ".kkkkkkkkkk.",
            ".kgggggggwk.",
            ".rrrrrrrrrr.",
            ".kgggggggwk.",
            ".kkkkkkkkkk.",
            ".kgggggggwk.",
            "..kkkkkkkk..",
            "............",
        }, P);

        // Hương khói (cầu nguyện, tín ngưỡng): a bronze burner, three lit sticks, smoke curling up.
        public static Sprite Incense => Glyph("incense", new[]
        {
            "...w...w....",
            "..w...w...w.",
            "...w...w.w..",
            "....r.r.r...",
            "....n.n.n...",
            "....n.n.n...",
            ".kkkkkkkkkk.",
            "kooyoooyoook",
            ".koooooooook",
            "..koooooook.",
            "...kk...kk..",
            "............",
        }, P);

        // Tâm ma: a violet heart split by a black crack, a red demon eye in it.
        public static Sprite HeartDemon => Glyph("heartdemon", new[]
        {
            "............",
            "..kk....kk..",
            ".kppk..kppk.",
            "kpwpkkkpppk.",
            "kpppkppkpppk",
            "kppkrrkppppk",
            ".kpkrrkpppk.",
            "..kppkpppk..",
            "...kpkppk...",
            "....kkpk....",
            ".....kk.....",
            "............",
        }, P);

        // Phế tu vi: a golden đan cracked in two, its light spilling out.
        public static Sprite Cripple => Glyph("cripple", new[]
        {
            "....k..k....",
            "..k.kkkk.k..",
            "...kyyykk...",
            "..kyywykyk..",
            ".kyyywkyyyk.",
            ".kyyykyyyyk.",
            ".kyyyykyyyk.",
            ".kyyykyyyok.",
            "..kyyykyok..",
            "...kkokkk...",
            "..k..kk..k..",
            "............",
        }, P);

        // Sinh lực (máu): a drop of blood.
        public static Sprite Blood => Glyph("blood", new[]
        {
            ".....kk.....",
            ".....kk.....",
            "....krrk....",
            "....krrk....",
            "...krrrrk...",
            "..krwrrrrk..",
            "..krwrrrrk..",
            ".krrrrrrrrk.",
            ".krrrrrrrrk.",
            ".krrrrrrrrk.",
            "..krrrrrrk..",
            "...kkkkkk...",
        }, P);

        public static Sprite Sword => Glyph("sword", new[]
        {
            "..........kk",
            ".........kwk",
            "........kwk.",
            ".......kwk..",
            "......kwk...",
            "..k..kwk....",
            "...kkwk.....",
            "...kyk......",
            "..kykk......",
            ".kyk..k.....",
            "kyk.........",
            "kk..........",
        }, P);

        public static Sprite Gem => Glyph("gem", new[]
        {
            "............",
            "...kkkkkk...",
            "..kcwccccck.",
            ".kcwcccccbck",
            "kkkkkkkkkkkk",
            ".kcccccccbk.",
            "..kcccccbk..",
            "...kcccbk...",
            "....kcbk....",
            ".....kk.....",
            "............",
            "............",
        }, P);

        public static Sprite Pill => Glyph("pill", new[]
        {
            "............",
            "....kkkk....",
            "...kooook...",
            "..koowooook.",
            "..kowoooook.",
            "..koooooook.",
            "..koooooook.",
            "..koooooook.",
            "...koooook..",
            "....kkkkk...",
            "............",
            "............",
        }, P);

        public static Sprite Bowl => Glyph("bowl", new[]
        {
            "............",
            "............",
            "....wwww....",
            "..wwwwwwww..",
            ".kkkkkkkkkk.",
            ".knnnnnnnnk.",
            "..knnnnnnk..",
            "...knnnnk...",
            "....kkkk....",
            "............",
            "............",
            "............",
        }, P);

        public static Sprite Flower => Glyph("flower", new[]
        {
            "............",
            ".....rr.....",
            "....rrrr....",
            "..rr.rr.rr..",
            ".rrrryyrrrr.",
            ".rrrryyrrrr.",
            "..rr.rr.rr..",
            "....rrrr....",
            ".....rr.....",
            ".....gg.....",
            "....gg......",
            "............",
        }, P);

        public static Sprite SunSmall => Glyph("sunsmall", new[]
        {
            "............",
            ".y...y...y..",
            "..y..y..y...",
            "....yyy.....",
            "...yyyyy....",
            "yyyyyoyyyyy.",
            "...yyyyy....",
            "....yyy.....",
            "..y..y..y...",
            ".y...y...y..",
            "............",
            "............",
        }, P);

        public static Sprite Leaf => Glyph("leaf", new[]
        {
            "..........k.",
            ".......kkkk.",
            ".....kooook.",
            "....koooook.",
            "...kooooook.",
            "..koooonook.",
            "..kooonook..",
            "..koonook...",
            "..konook....",
            "..knook.....",
            ".knkk.......",
            "kn..........",
        }, P);

        public static Sprite Season(Core.Season s) =>
            s == Core.Season.Xuan ? Flower : s == Core.Season.Ha ? SunSmall : s == Core.Season.Thu ? Leaf : Snowflake;

        public static Sprite ForRealm(Sim.Realm r, bool demonic = false) =>
            Unit(demonic ? SpriteLibrary.Unit.CultivatorDemonic :
                r >= Sim.Realm.HoaThan ? SpriteLibrary.Unit.CultivatorHT : r == Sim.Realm.NguyenAnh ? SpriteLibrary.Unit.CultivatorNA :
                r == Sim.Realm.KetDan ? SpriteLibrary.Unit.CultivatorKD : r == Sim.Realm.TrucCo ? SpriteLibrary.Unit.CultivatorTC : SpriteLibrary.Unit.CultivatorLK);

        // A cultivator's figure by realm (or the demonic one), for cards and lists.
        public static Sprite Cultivator(Sim.Cultivator c) => ForRealm(c.Realm, c.Demonic);

        public static Sprite Person => Unit(SpriteLibrary.Unit.Villager0);

        // The picture for a line of news: by its effect first (what happened), then by its kind.
        public static Sprite ForEvent(Sim.EventKind kind, Sim.Fx fx)
        {
            switch (fx)
            {
                case Sim.Fx.Quake: return Quake;
                case Sim.Fx.Eruption: return Volcano;
                case Sim.Fx.Splash: return Wave;
                case Sim.Fx.Miasma: return Skull;
                case Sim.Fx.Stampede: return Paw;
                case Sim.Fx.Rain: return RainCloud;
                case Sim.Fx.Snow: return Snowflake;
                case Sim.Fx.Storm: return Storm;
                case Sim.Fx.Tribulation: return Tribulation;
                case Sim.Fx.DuelKill:
                case Sim.Fx.DuelFlee: return Sword;
                case Sim.Fx.BeastSlain:
                case Sim.Fx.BeastKill:
                case Sim.Fx.BeastFlee: return Paw;
            }
            switch (kind)
            {
                case Sim.EventKind.Breakthrough: return Star;
                case Sim.EventKind.Death: return Skull;
                case Sim.EventKind.Tribulation: return Tribulation;
                case Sim.EventKind.Divine: return Bolt;
                case Sim.EventKind.Legend: return Book;
                case Sim.EventKind.Calamity: return Eclipse;
                case Sim.EventKind.Disaster: return Wave;
                case Sim.EventKind.War:
                case Sim.EventKind.Battle:
                case Sim.EventKind.Duel:
                case Sim.EventKind.Vendetta: return Sword;
                case Sim.EventKind.Destruction: return Wrath;
                case Sim.EventKind.Founding:
                case Sim.EventKind.Schism:
                case Sim.EventKind.Succession:
                case Sim.EventKind.Alliance:
                case Sim.EventKind.Peace:
                case Sim.EventKind.Patronage: return Banner;
                case Sim.EventKind.Awakening: return Seed;
                case Sim.EventKind.Fortune: return Gem;
                case Sim.EventKind.Beast: return Paw;
                case Sim.EventKind.Relic: return Book;
                case Sim.EventKind.Era: return Globe;
                case Sim.EventKind.Faith: return Incense;
                case Sim.EventKind.Destiny: return Star;
                default: return Scroll;
            }
        }

        public static Sprite Dice => Glyph("dice", new[]
        {
            "............",
            ".kkkkkkkkkk.",
            ".kwwwwwwwwk.",
            ".kwkwwwwkwk.",
            ".kwwwwwwwwk.",
            ".kwwwkkwwwk.",
            ".kwwwkkwwwk.",
            ".kwwwwwwwwk.",
            ".kwkwwwwkwk.",
            ".kwwwwwwwwk.",
            ".kkkkkkkkkk.",
            "............",
        }, P);

        // Linh thảo: a sprig of spirit herb.
        public static Sprite Herb => Glyph("herb", new[]
        {
            "......g.....",
            ".....ggk....",
            "....gwgk.g..",
            "..g.ggk.ggk.",
            ".ggkgk.ggk..",
            "..gggkggk...",
            "....ggkk....",
            ".....nk.....",
            ".....nk.....",
            "....nnk.....",
            "............",
            "............",
        }, P);

        // Crosshair: the camera follows this one.
        public static Sprite Target => Glyph("target", new[]
        {
            ".....kk.....",
            "...kkwwkk...",
            "..kw.kk.wk..",
            ".kw..kk..wk.",
            ".k...kk...k.",
            "kwkkk..kkkwk",
            "kwkkk..kkkwk",
            ".k...kk...k.",
            ".kw..kk..wk.",
            "..kw.kk.wk..",
            "...kkwwkk...",
            ".....kk.....",
        }, P);

        // Bookmark ribbon: on the watch list.
        public static Sprite Bookmark => Glyph("bookmark", new[]
        {
            "..kkkkkkkk..",
            "..kyyyyyyk..",
            "..kyyyyyyk..",
            "..kyywwyyk..",
            "..kyyyyyyk..",
            "..kyyyyyyk..",
            "..kyyyyyyk..",
            "..kyyyyyyk..",
            "..kyykkyyk..",
            "..kyk..kyk..",
            "..kk....kk..",
            "............",
        }, P);

        // Name tags over the map.
        public static Sprite Tag => Glyph("tag", new[]
        {
            "............",
            "..kkkkkkkk..",
            ".kwwwwwwwwk.",
            "kwwkkkkkkwwk",
            "kwwwwwwwwwwk",
            "kwwkkkkwwwwk",
            ".kwwwwwwwwk.",
            "..kkkkkkkk..",
            ".....kk.....",
            ".....k......",
            "............",
            "............",
        }, P);

        // Circular arrow: back to how it was.
        public static Sprite Undo => Glyph("undo", new[]
        {
            "............",
            "...kwwwwk...",
            "..kwkkkkwk..",
            ".kwk....kwk.",
            "kwk......kwk",
            "kwk......kwk",
            "kwk......kwk",
            ".k......kwk.",
            "......kwwk..",
            "....kwwwk...",
            "....kwwk....",
            ".....kk.....",
        }, P);

        public static Sprite Pause => Glyph("pause", new[] { "............", "..kkk..kkk..", "..kwk..kwk..", "..kwk..kwk..", "..kwk..kwk..", "..kwk..kwk..", "..kwk..kwk..", "..kwk..kwk..", "..kkk..kkk..", "............" }, P);
        public static Sprite Play1 => Glyph("play1", new[] { "............", "...kk.......", "...kwk......", "...kwwk.....", "...kwwwk....", "...kwwwk....", "...kwwk.....", "...kwk......", "...kk.......", "............" }, P);
        public static Sprite Play2 => Glyph("play2", new[] { "............", ".kk...kk....", ".kwk..kwk...", ".kwwk.kwwk..", ".kwwwkkwwwk.", ".kwwwkkwwwk.", ".kwwk.kwwk..", ".kwk..kwk...", ".kk...kk....", "............" }, P);
        public static Sprite Play3 => Glyph("play3", new[] { "............", "kk..kk..kk..", "kwk.kwk.kwk.", "kwwkkwwkkwwk", "kwwkkwwkkwwk", "kwwkkwwkkwwk", "kwwkkwwkkwwk", "kwk.kwk.kwk.", "kk..kk..kk..", "............" }, P);
        public static Sprite Skip => Glyph("skip", new[] { "............", ".kk...kk.kk.", ".kwk..kwkkwk", ".kwwk.kwwkwk", ".kwwwkkwwkwk", ".kwwwkkwwkwk", ".kwwk.kwwkwk", ".kwk..kwkkwk", ".kk...kk.kk.", "............" }, P);
        public static Sprite Plus => Glyph("plus", new[] { "..........", "....kk....", "...kwwk...", "...kwwk...", ".kkkwwkkk.", ".kwwwwwwk.", ".kkkwwkkk.", "...kwwk...", "...kwwk...", "....kk...." }, P);
        public static Sprite Minus => Glyph("minus", new[] { "..........", "..........", "..........", "..........", ".kkkkkkkk.", ".kwwwwwwk.", ".kkkkkkkk.", "..........", "..........", ".........." }, P);
        public static Sprite Close => Glyph("close", new[] { "kk....kk", "kwk..kwk", ".kwkkwk.", "..kwwk..", "..kwwk..", ".kwkkwk.", "kwk..kwk", "kk....kk" }, P);
    }
}
