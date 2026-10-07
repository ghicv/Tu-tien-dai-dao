using System.Collections.Generic;
using UnityEngine;

namespace ThienDao.Render
{
    // Yêu thú drawn by hand, a character per pixel ('.' is empty), facing right; the outline is added afterwards.
    // Drawn tall and upright, chests high, heads raised, maws open: beasts to fear, not cattle. On screen a
    // higher grade is drawn larger, up to three times (SpriteLibrary.BeastScale). Shapes after the old bestiaries:
    // Cùng Kỳ for the tiger, Thao Thiết for the ox, Đào Ngột for the ape, Hỗn Độn, a winged long, a Huyền Quy.
    static class BeastArt
    {
        public sealed class Look
        {
            public string[][] Frames;              // one or two frames; fliers beat their wings in the second
            public Dictionary<char, Color32> Palette;
        }

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        static Dictionary<char, Color32> P(params (char k, Color32 c)[] entries)
        {
            var d = new Dictionary<char, Color32>
            {
                ['E'] = C(255, 70, 50), ['W'] = C(250, 248, 236), ['Y'] = C(255, 214, 80), ['K'] = C(30, 22, 22)
            };
            foreach (var (k, c) in entries) d[k] = c;
            return d;
        }

        // Sim.BeastKind order.
        public static readonly Look[] Kinds =
        {
            // Yêu lang: chest thrown out, head up, fangs bared
            new Look
            {
                Palette = P(('A', C(130, 132, 146)), ('B', C(206, 206, 216)), ('C', C(80, 80, 94))),
                Frames = new[] { new[]
                {
                    "..............AA..A........",
                    ".............AAAAAA........",
                    "............AAAAAAAA.......",
                    "...........AAAAAAEAAA......",
                    "...........AAAAAAAAAAWW....",
                    "..........AAAAAAAAAWWW.....",
                    ".........AAAAAAAABBB.......",
                    "........AAAAAAAAABBB.......",
                    ".......AAAAAAAAAABBB.......",
                    "......AAAAAAAAAAABBBB......",
                    "....AAAAAAAAAAAAAABBB......",
                    "..AAAAAAAAAAAAAAAAABB......",
                    ".AAA.AAAAAAAAAAAAAAAB......",
                    "AAA..AAAAAAAAAAAAAAAA......",
                    "AA...AAAAAAA..AAAAAAC......",
                    "A....AAAAAA....CAAAC.......",
                    ".....CAAAC.....CAAC........",
                    ".....CAAC......CAC.........",
                    "....CCAC.......CAC.........",
                    "....CC.C......CC.C.........",
                } }
            },
            // Cùng Kỳ: a red tiger rearing up, great bat wings raised behind it
            new Look
            {
                Palette = P(('A', C(226, 100, 46)), ('B', C(250, 216, 168)), ('C', C(104, 30, 26)), ('D', C(150, 34, 44))),
                Frames = new[] { new[]
                {
                    ".DD......................",
                    ".DDD.......DD............",
                    ".DDDD.....DDDD...........",
                    "..DDDD...DDDDD...........",
                    "..DDDDD.DDDDDD....CC..CC.",
                    "...DDDDDDDDDDD...CAACCAAC",
                    "....DDDDDDDDD...CAAAAAAAC",
                    ".....DDDDDDDD..CAAAAAAEAA",
                    "......DDDDDDD.CAACAAAAAAWW",
                    ".......DDDDDDAAAACAAAAWWW.",
                    "........AAAACAAAACAABBB...",
                    "......AAACAAAACAAAABBB....",
                    ".....AACAAAACAAAACABB.....",
                    "....ACAAAACAAAACAAAB......",
                    "...AAAACAAAACAAAAAAB......",
                    "..CC.AAAAAAAAAAACAAA......",
                    ".CC..AACAA....CAACAA......",
                    ".C...AACA......CAAC.......",
                    ".....CCAA......CAAA.......",
                    ".....CC.CC.....CC.CC......",
                } }
            },
            // Yêu hùng: up on its hind legs, claws raised
            new Look
            {
                Palette = P(('A', C(100, 68, 48)), ('B', C(156, 116, 86)), ('C', C(56, 38, 28))),
                Frames = new[] { new[]
                {
                    ".....AA......AA.........",
                    "....AAAA....AAAA........",
                    "....AAAAAAAAAAAA........",
                    "...AAAEAAAAAAEAAA.......",
                    "...AAAAAAAAAAAAAA.......",
                    "...AAAWKKKKKKWAAA.......",
                    "...AAAWKKKKKKWAAA.......",
                    "W...AAAWWWWWWAAA....W...",
                    "WW.AAAAAAAAAAAAAA..WW...",
                    ".WAAAAAAAAAAAAAAAAAW....",
                    "..AAAAABBBBBBBAAAAA.....",
                    "...AAAABBBBBBBBAAA......",
                    "....AAABBBBBBBBAA.......",
                    "....AAAABBBBBBAAA.......",
                    "....AAAAAAAAAAAAA.......",
                    "...AAAAAA...AAAAAA......",
                    "...AAAAA.....AAAAA......",
                    "..CCCCCC.....CCCCCC.....",
                } }
            },
            // Cự mãng: reared up like a cobra, hood spread
            new Look
            {
                Palette = P(('A', C(60, 128, 62)), ('B', C(168, 208, 112))),
                Frames = new[] { new[]
                {
                    "..........AAAAAA........",
                    ".........AAAAAAAA.......",
                    "........AAEAAAAEAA......",
                    "........AAAAAAAAAA......",
                    "........AAWWAAWWAA......",
                    ".........AAAAAAAA.......",
                    "..........ABBBBA........",
                    "..........ABBBBA........",
                    ".........AABBBBAA.......",
                    "........AAABBBBAAA......",
                    ".........AABBBBAA.......",
                    "..........ABBBBA........",
                    "...........ABBA.........",
                    "..........AABBA.........",
                    ".........AAABA..........",
                    "........AAAAA...........",
                    ".......AAAAA......AAA...",
                    "......AAAAAAAAAAAAAAAA..",
                    ".....AAABBBBBBBBBBBAAA..",
                    "......AAAAAAAAAAAAAA....",
                } }
            },
            // Độc hạt: the tail raised high and curled, its sting poised over the pincers
            new Look
            {
                Palette = P(('A', C(186, 106, 54)), ('B', C(232, 168, 100)), ('C', C(104, 56, 30)), ('D', C(255, 60, 60))),
                Frames = new[] { new[]
                {
                    ".......AAAA..............",
                    "......AA..AA.............",
                    ".....AA....AD............",
                    ".....AA.....D............",
                    ".....AA..................",
                    "......AA.................",
                    "......AA.................",
                    ".......AA................",
                    ".......AA..........AA.AA.",
                    "........AAAAAAAAA..AAAAA.",
                    "......AAAAAAAAAAAAAA.AA..",
                    ".....AAAAAAAAAAAAAEAAA...",
                    ".....ABBBBBBBBBBBBAAAA...",
                    "....C.C.C.C.C.C.C.AA.AA..",
                    "...C.C.C.C.C.C.C.........",
                } }
            },
            // Kim sí đại bằng
            new Look
            {
                Palette = P(('A', C(176, 124, 56)), ('B', C(244, 240, 228)), ('C', C(90, 60, 30))),
                Frames = new[]
                {
                    new[]
                    {
                        "AA......................AA",
                        "AAA....................AAA",
                        ".AAA..................AAA.",
                        ".AAAA................AAAA.",
                        "..AAAA......BBB.....AAAA..",
                        "..AAAAA....BBBBE...AAAAA..",
                        "...AAAAAA.BBBBBYY.AAAAA...",
                        "....AAAAAAAAAAAAAAAAAA....",
                        ".....AAAAAAAAAAAAAAAA.....",
                        ".........AAAAAAAAA........",
                        "..........C.....C.........",
                        "..........CC...CC.........",
                    },
                    new[]
                    {
                        "............BBB...........",
                        "...........BBBBE..........",
                        "..........BBBBBYY.........",
                        "....AAAAAAAAAAAAAAAAAA....",
                        "..AAAAAAAAAAAAAAAAAAAAAA..",
                        ".AAAAAA..AAAAAAAAA..AAAAAA",
                        "AAAAA.....AAAAAAA.....AAAA",
                        "AAA........A...A........AA",
                        "AA.........C...C.........A",
                        "..........CC...CC.........",
                    }
                }
            },
            // Cửu vĩ hồ: sitting tall, nine tails fanned up behind
            new Look
            {
                Palette = P(('A', C(232, 116, 52)), ('B', C(252, 238, 222)), ('C', C(140, 60, 28))),
                Frames = new[] { new[]
                {
                    ".B.B.B.B.B...............",
                    "BAABAABAABA..............",
                    ".AAAAAAAAAA........A.A...",
                    "..AAAAAAAAAA......AAAA...",
                    "...AAAAAAAAA.....AAAAEA..",
                    "....AAAAAAAA....AAAAAAWW.",
                    ".....AAAAAAA...AAAAAA....",
                    "......AAAAAAAAAAAAAB.....",
                    ".......AAAAAAAAAAAABB....",
                    "........AAAAAAAAAAABB....",
                    "........AAAAAAAAAABBB....",
                    "........AAAAAAAAABBB.....",
                    ".........AAAAAAAAABB.....",
                    ".........AAC..AAC.AAC....",
                    ".........CCC..CCC.CCC....",
                } }
            },
            // Thao Thiết: face on, all head and maw, horns sweeping up, a white mane
            new Look
            {
                Palette = P(('A', C(84, 144, 76)), ('B', C(150, 200, 120)), ('C', C(38, 66, 38)), ('D', C(176, 40, 40)), ('F', C(236, 236, 220))),
                Frames = new[] { new[]
                {
                    "...DD...............DD.....",
                    "..DD.................DD....",
                    ".DD...FFFFFFFFFF......DD...",
                    ".DD.FFFAAAAAAAAAFFF...DD...",
                    "..DDFAAAAAAAAAAAAAFFDD.....",
                    "...FAAAAEAAAAAAEAAAAF......",
                    "..FAAAAAAAAAAAAAAAAAAF.....",
                    "..FAAWWWWWWWWWWWWWWAAF.....",
                    "..FAAW.K.K.K.K.K..WAAF.....",
                    "..FAAK............KAAF.....",
                    "..FAAW.K.K.K.K.K..WAAF.....",
                    "..FAAWWWWWWWWWWWWWWAAF.....",
                    "...FAAAAAAAAAAAAAAAAF......",
                    "....AAAAAAAAAAAAAAAA.......",
                    "...AAABBBBBBBBBBBBAAA......",
                    "..AAAA.AAA....AAA.AAAA.....",
                    "..AAC..AAC....AAC..CAA.....",
                    "..CCC..CCC....CCC..CCC.....",
                } }
            },
            // Đào Ngột: a mountain of wild blue-black mane, eyes and fangs glinting out of it
            new Look
            {
                Palette = P(('A', C(38, 56, 104)), ('B', C(70, 150, 176)), ('C', C(150, 30, 40))),
                Frames = new[] { new[]
                {
                    "...B..B..B..B..............",
                    "..BAB.BAB.BAB.B............",
                    ".BAAABAAABAAABAB...........",
                    "BAAAAAAAAAAAAAAAB..........",
                    "BAAAAAAAAAAAAAAAAB.........",
                    "BAAAAAAAAAAAAAAAAAB........",
                    ".BAAAAAAAAAAAAAAAAAB.......",
                    ".BAAAAAAAAAAAAAAYAAAB......",
                    "..BAAAAAAAAAAAAAAAAWAB.....",
                    "..BAAAAAAAAAAAAAAAWWW......",
                    "...AAAAAAAAAAAAAAAAAW......",
                    "...AAAAAAAAAAAAAAAAA.......",
                    "...AAAAAAAAAAAAAAAAA.......",
                    "...AAAAC..AAAAC..AAAAC.....",
                    "...AAAC...AAAC....AAAC.....",
                    "..CCCCC..CCCCC...CCCCC.....",
                } }
            },
            // Phi long: standing tall, neck raised, wings spread high
            new Look
            {
                Palette = P(('A', C(94, 82, 168)), ('B', C(172, 152, 222)), ('C', C(58, 48, 108)), ('D', C(150, 40, 110)), ('F', C(236, 140, 190))),
                Frames = new[]
                {
                    new[]
                    {
                        "..DD...................DD.",
                        "..DDD.................DDD.",
                        "..DDFD...............DFDD.",
                        "..DDFFD.....AA......DFFDD.",
                        "..DDFFFD...AAAA....DFFFDD.",
                        "...DDFFFD.AAEAAA..DFFFDD..",
                        "....DDFFFDAAAAAWWDFFFDD...",
                        ".....DDFFFAAAAWW.FFFDD....",
                        "......DDDAAAAA..DDDD......",
                        ".........AAABA............",
                        "........AAAABBA...........",
                        ".......AAAAABBA...........",
                        "......AAAAAABBA...........",
                        ".....AAAAAAABBAA..........",
                        "...AAAAAAAAAAAAAA.........",
                        "..AA.AAC....AAC...........",
                        ".AA..AC.....AC............",
                        "AA..CC.....CC.............",
                    },
                    new[]
                    {
                        "............AA............",
                        "...........AAAA...........",
                        "..........AAEAAA..........",
                        "..........AAAAAWW.........",
                        "..........AAAAWW..........",
                        ".........AAAAA............",
                        ".........AAABA............",
                        "...DDDDDAAAABBADDDDD......",
                        "..DDFFFFAAAAABBAFFFFDD....",
                        ".DDFFFFAAAAAABBAAFFFFDD...",
                        "DDFFFF.AAAAAAABBAA.FFFFDD.",
                        "DDD...AAAAAAAAAAAAA...DDD.",
                        ".....AA.AAC....AAC........",
                        "....AA..AC.....AC.........",
                        "...AA..CC.....CC..........",
                    }
                }
            },
            // Hỏa kỳ lân: rearing, a mane and tail of fire, one horn
            new Look
            {
                Palette = P(('A', C(232, 176, 60)), ('B', C(252, 228, 160)), ('C', C(140, 70, 30)), ('D', C(255, 244, 210)), ('F', C(236, 76, 40))),
                Frames = new[] { new[]
                {
                    "..........D..............",
                    ".........FD..............",
                    "........FYAAA............",
                    ".......FYAAAAE...........",
                    ".......YAAAAAAWW.........",
                    "......FYAAAAA............",
                    ".....FYAAAAA.............",
                    ".....YAAAAAB.............",
                    "....FYAAAAABB............",
                    "....YAAAAAABB............",
                    "...FAAAAAAAABB...........",
                    "..FYAAAAAAAAAB...........",
                    ".FY.AAAAAAAAAAA..........",
                    "FY..AAAAAAAAAAAA.........",
                    "....AAAAA..AAAAA.........",
                    "....AC.AC...AC.AC........",
                    "....C..C....C..C.........",
                    "...CC.CC...CC.CC.........",
                } }
            },
            // Huyết bức
            new Look
            {
                Palette = P(('A', C(90, 24, 34)), ('C', C(60, 16, 24)), ('D', C(140, 34, 50))),
                Frames = new[]
                {
                    new[]
                    {
                        "DD.........AA.........DD",
                        "DDD.......AAAA.......DDD",
                        "DDDD.....AYAAYA.....DDDD",
                        ".DDDD....AAWWAA....DDDD.",
                        ".DDDDD....AAAA....DDDDD.",
                        "..DDDDDDDDAAAADDDDDDDD..",
                        "...DDD.DDDAAAADDD.DDD...",
                        ".........C....C.........",
                    },
                    new[]
                    {
                        ".........AA.AA..........",
                        ".........AYAAYA.........",
                        ".........AAWWAA.........",
                        "..DDDDDDDDAAAADDDDDDDD..",
                        ".DDDDDDDDDAAAADDDDDDDDD.",
                        "DDDD.DDDD.AAAA.DDDD.DDDD",
                        "DD....DD...CC...DD....DD",
                    }
                }
            },
            // Huyền quy: a domed, spiked shell glowing at the seams, the neck raised high
            new Look
            {
                Palette = P(('A', C(68, 62, 58)), ('B', C(112, 102, 94)), ('C', C(94, 110, 94)), ('D', C(204, 200, 184)), ('F', C(80, 220, 200))),
                Frames = new[] { new[]
                {
                    "................CCC.......",
                    "...............CCCCE......",
                    "..............CCCCCCW.....",
                    ".....D.D.D.D..CCCC........",
                    "....AAAAAAAAA.CCC.........",
                    "...AABBBAABBBACC..........",
                    "..AABBBBBABBBBACC.........",
                    ".AAABBBAAABBBAAA..........",
                    ".AABBBBBABBBBBAA..........",
                    "AAABBBAAABBBAAAA..........",
                    "AAAAAAAAAAAAAAAA..........",
                    "FAAAFAAAFAAAFAAA..........",
                    ".AAAAAAAAAAAAAAC..........",
                    "..CCCC.CCCC.CCCC..........",
                    ".CCCCC.CCCC.CCCCC.........",
                } }
            },
            // Hỗn Độn: a tall white bulk without a face, wings raised, six legs
            new Look
            {
                Palette = P(('A', C(240, 236, 236)), ('B', C(200, 182, 190)), ('F', C(212, 224, 240))),
                Frames = new[] { new[]
                {
                    "FF.......AAAA.......FF",
                    "FFF....AAAAAAAA....FFF",
                    ".FFF..AAAAAAAAAA..FFF.",
                    ".FFFFAAAAAAAAAAAAFFFF.",
                    "..FFFAAAAAAAAAAAAFFF..",
                    "....AAAAAAAAAAAAAA....",
                    "...AAAAAAAAAAAAAAAA...",
                    "...AAAAAAAAAAAAAAAA...",
                    "...ABAAAAAAAAAAAAABA..",
                    "...ABBAAAAAAAAAAABBA..",
                    "....ABBBAAAAAAABBBA...",
                    ".....AAABBBBBBBAAA....",
                    ".....AA.AA.AA.AA.AA...",
                    ".....BB.BB.BB.BB.BB...",
                } }
            },
        };
    }
}
