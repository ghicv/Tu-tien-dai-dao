using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    // What a sơn môn is made of (devlog 38), in the order it is built.
    public enum SectPart : byte { Hall, Gate, Dorm, InnerWall, Scripture, Training, Alchemy, Field, Formation, Pagoda, Elder, OuterWall, Pond }

    public readonly struct SectModule
    {
        public readonly SectPart Part;
        public readonly int X0, Y0, X1, Y1; // cells round the main hall at (0, 0), north up, inclusive
        public readonly string Name;
        public SectModule(SectPart part, int x0, int y0, int x1, int y1, string name) { Part = part; X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; Name = name; }
        public int W => X1 - X0 + 1;
        public int H => Y1 - Y0 + 1;
    }

    // A sect that is gone leaves its sơn môn standing as a ruin for the ages after (until it crumbles away).
    public sealed class SectRuin
    {
        public int X, Y;
        public uint Built;
        public byte Tier;
        public bool Demonic;
        public string Name;
        public long Since;
    }

    // Sơn môn (devlog 38): a sect is not a hall and a few huts but a planned complex on one north–south axis, built
    // module by module as it grows and left to decay as it fails. Sects are ranked each year by their power: one or
    // two thánh địa, up to five đại tông, the rest tiểu môn phái; the rank caps how far the complex can grow. Every
    // module is a function of the sim (dorms for the disciples, a scripture hall for the methods, an alchemy hall, the
    // spirit fields…); the renderer draws it, the stand-ins walk it.
    public sealed partial class FactionSystem
    {
        public const byte TierMinor = 0, TierGreat = 1, TierHoly = 2;
        public static readonly string[] TierNames = { "Tiểu môn phái", "Đại tông", "Thánh địa" };
        static readonly int[] MaxModules = { 5, 12, 17 };

        // The whole plan, the main hall at the centre. Mirrored pairs are separate modules, built one after the other.
        public static readonly SectModule[] Layout =
        {
            new SectModule(SectPart.Hall, -6, -6, 6, 4, "chính điện"),
            new SectModule(SectPart.Gate, -3, -15, 3, -7, "sơn môn"),
            new SectModule(SectPart.Dorm, -15, -10, -8, -2, "đệ tử xá phía tây"),
            new SectModule(SectPart.Dorm, 8, -10, 15, -2, "đệ tử xá phía đông"),
            new SectModule(SectPart.InnerWall, -17, -16, 17, 10, "tường nội môn"),
            new SectModule(SectPart.Scripture, -5, 5, 5, 9, "Tàng Kinh Các"),
            new SectModule(SectPart.Training, -15, 1, -8, 8, "Diễn Võ Trường"),
            new SectModule(SectPart.Alchemy, 8, 1, 15, 8, "Luyện Đan Phòng"),
            new SectModule(SectPart.Field, -26, -13, -19, 4, "linh điền phía tây"),
            new SectModule(SectPart.Field, 19, -13, 26, 4, "linh điền phía đông"),
            new SectModule(SectPart.Formation, -25, 8, -19, 14, "hộ sơn trận đài phía tây"),
            new SectModule(SectPart.Formation, 19, 8, 25, 14, "hộ sơn trận đài phía đông"),
            new SectModule(SectPart.Pagoda, -2, 11, 2, 18, "bảo tháp trấn sơn"),
            new SectModule(SectPart.Elder, -13, 12, -6, 17, "điện trưởng lão phía tây"),
            new SectModule(SectPart.Elder, 6, 12, 13, 17, "điện trưởng lão phía đông"),
            new SectModule(SectPart.OuterWall, -28, -19, 28, 20, "tường ngoại môn"),
            new SectModule(SectPart.Pond, -7, -13, 7, -8, "linh trì"),
        };
        public const int LayoutX0 = -28, LayoutY0 = -19, LayoutX1 = 28, LayoutY1 = 20;

        public readonly List<SectRuin> Ruins = new List<SectRuin>();
        const int RuinYears = 300;

        public static bool Has(uint mask, int module) => (mask & (1u << module)) != 0;

        // At war with anyone (the sơn môn's formations burn bright).
        public bool AtWar(int id)
        {
            foreach (var r in Relations)
                if ((r.A == id || r.B == id) && r.Stance == Stance.War) return true;
            return false;
        }

        // ---------------------------------------------------------------- yearly: rank, grow, decay

        void SectComplexYearly(long tick)
        {
            // Rank by power: the first one or two thánh địa, the next five đại tông.
            var ranked = new List<Faction>();
            foreach (var f in All) if (f.Alive) ranked.Add(f);
            ranked.Sort((a, b) => b.Power != a.Power ? b.Power.CompareTo(a.Power) : a.Id.CompareTo(b.Id));
            int holy = ranked.Count >= 8 ? 2 : ranked.Count >= 3 ? 1 : 0;
            for (int k = 0; k < ranked.Count; k++)
            {
                var f = ranked[k];
                byte tier = k < holy ? TierHoly : k < holy + 5 && ranked.Count > 1 ? TierGreat : TierMinor;
                // A name once won is not lost for slipping one place: a thánh địa stays one while it is within one of the
                // thánh địa ranks, a đại tông while within one of the đại tông.
                if (f.Tier == TierHoly && tier < TierHoly && k <= holy) tier = TierHoly;
                else if (f.Tier >= TierGreat && tier == TierMinor && k <= holy + 5) tier = TierGreat;
                if (tier > f.PeakTier && tick >= 3L * SimClock.DaysPerYear) // told the first time it rises so high
                    _sim.Events.Add(tick, EventKind.Founding, tier == TierHoly ? 3 : 2,
                        $"{NameOf(f.Id)} thế lực ngày càng lớn, được thiên hạ tôn là {TierNames[tier].ToLower()}.", SeatX(f), SeatY(f), Fx.LightPillar, -1, -1, f.Id);
                f.Tier = tier;
                if (tier > f.PeakTier) f.PeakTier = tier;
                Grow(f, tick);
            }
            for (int k = Ruins.Count - 1; k >= 0; k--)
                if (tick - Ruins[k].Since > RuinYears * (long)SimClock.DaysPerYear) Ruins.RemoveAt(k);
        }

        float SeatX(Faction f) => _sim.Settlements.All[f.Id].X + 0.5f;
        float SeatY(Faction f) => _sim.Settlements.All[f.Id].Y + 0.5f;

        // How far the sơn môn should stand: its disciples, its elders, its lão tổ and its coffers.
        int WantedModules(Faction f)
        {
            int elders = 0, ancients = 0;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.SectId != f.Id) continue;
                if (c.Realm >= Realm.NguyenAnh) ancients++;
                else if (c.Realm >= Realm.KetDan) elders++;
            }
            float score = f.Members + 4f * elders + 15f * ancients + f.Treasury / 300f;
            return Mathf.Clamp(1 + (int)(score / 4f), 1, MaxModules[f.Tier]);
        }

        // One module a year while it has the linh thạch; a sect in decline lets the last one fall to ruin.
        void Grow(Faction f, long tick)
        {
            int built = 0, ruined = 0;
            for (int m = 0; m < Layout.Length; m++)
            {
                if (Has(f.Built, m)) built++;
                if (Has(f.Ruined, m)) ruined++;
            }
            int standing = built - ruined, want = WantedModules(f);
            var s = _sim.Settlements.All[f.Id];
            if (want > standing)
            {
                // Mend a ruined module before building a new one.
                for (int m = 0; m < Layout.Length; m++)
                    if (Has(f.Ruined, m) && f.Treasury >= Cost(m))
                    {
                        f.Treasury -= Cost(m);
                        f.Ruined &= ~(1u << m);
                        _sim.Events.Add(tick, EventKind.Founding, 1, $"{NameOf(f.Id)} trùng tu {Layout[m].Name}.", s.X + 0.5f, s.Y + 0.5f, Fx.None, -1, -1, f.Id);
                        return;
                    }
                for (int m = 0; m < Layout.Length; m++)
                {
                    if (Has(f.Built, m) || Has(f.Skipped, m)) continue;
                    if (f.Treasury < Cost(m)) return;
                    if (!Fits(s, Layout[m])) { f.Skipped |= 1u << m; continue; } // the mountain will not take it
                    f.Treasury -= Cost(m);
                    f.Built |= 1u << m;
                    Clear(s, Layout[m]);
                    var mod = Layout[m];
                    float mx = s.X + 0.5f + (mod.X0 + mod.X1) * 0.5f, my = s.Y + 0.5f + (mod.Y0 + mod.Y1) * 0.5f;
                    bool great = mod.Part == SectPart.Pagoda || mod.Part == SectPart.OuterWall || mod.Part == SectPart.Scripture;
                    _sim.Events.Add(tick, EventKind.Founding, great ? 2 : 1, $"{NameOf(f.Id)} xây xong {mod.Name}.", mx, my, Fx.LightPillar, -1, -1, f.Id);
                    return;
                }
            }
            else if (want < standing - 2)
            {
                for (int m = Layout.Length - 1; m >= 0; m--)
                    if (Has(f.Built, m) && !Has(f.Ruined, m))
                    {
                        f.Ruined |= 1u << m;
                        _sim.Events.Add(tick, EventKind.Destruction, 1, $"{NameOf(f.Id)} sa sút, {Layout[m].Name} bỏ hoang, cỏ mọc đầy sân.", s.X + 0.5f, s.Y + 0.5f, Fx.None, -1, -1, f.Id);
                        return;
                    }
            }
        }

        static float Cost(int module) => 40f + 25f * module;

        // Mostly dry, open ground, and no one else's town on it.
        bool Fits(Settlement s, SectModule m)
        {
            int ok = 0, total = 0;
            for (int y = s.Y + m.Y0; y <= s.Y + m.Y1; y++)
            for (int x = s.X + m.X0; x <= s.X + m.X1; x++)
            {
                if (m.Part == SectPart.InnerWall || m.Part == SectPart.OuterWall)
                    if (x != s.X + m.X0 && x != s.X + m.X1 && y != s.Y + m.Y0 && y != s.Y + m.Y1) continue; // only the ring matters
                total++;
                if (!_w.InBounds(x, y)) continue;
                int i = _w.Idx(x, y);
                if (!TerrainInfo.IsWalkable(_w.Terrain[i])) continue;
                if (_w.Owner[i] != 0 && _w.Owner[i] != s.Id + 1) continue;
                ok++;
            }
            return total > 0 && ok >= total * 0.8f;
        }

        // The trees and stones on the ground the module stands on are cleared (a wall only clears its ring).
        void Clear(Settlement s, SectModule m)
        {
            var objects = _w.Objects;
            for (int y = s.Y + m.Y0; y <= s.Y + m.Y1; y++)
            for (int x = s.X + m.X0; x <= s.X + m.X1; x++)
            {
                if (!_w.InBounds(x, y)) continue;
                bool ring = m.Part == SectPart.InnerWall || m.Part == SectPart.OuterWall;
                if (ring && x != s.X + m.X0 && x != s.X + m.X1 && y != s.Y + m.Y0 && y != s.Y + m.Y1) continue;
                int id = objects.CellObject[_w.Idx(x, y)];
                if (id < 0) continue;
                var t = objects.Get(id).Type;
                if (!ObjectInfo.IsBuilding(t)) objects.Remove(id);
            }
        }

        // The sect is gone: its sơn môn stands empty, then falls to ruin over three centuries.
        void LeaveRuin(Faction f, long tick)
        {
            if (f.Built == 0) return;
            var s = _sim.Settlements.All[f.Id];
            Ruins.Add(new SectRuin { X = s.X, Y = s.Y, Built = f.Built, Tier = f.Tier, Demonic = f.Demonic, Name = NameOf(f.Id), Since = tick });
        }

        // Where in its sơn môn a sect member might be found: a random point of one of the standing modules that fit
        // what they are (disciples in the dorms, the training ground, the fields; elders in their halls, the
        // scripture hall, the alchemy hall). False if the sect has no sơn môn yet.
        public bool SpotIn(Faction f, bool elder, float r01, float rx, float ry, out float x, out float y)
        {
            x = y = 0f;
            var s = _sim.Settlements.All[f.Id];
            int pick = -1, count = 0;
            for (int m = 0; m < Layout.Length; m++)
            {
                if (!Has(f.Built, m) || Has(f.Ruined, m)) continue;
                var part = Layout[m].Part;
                bool fits = elder ? part == SectPart.Elder || part == SectPart.Scripture || part == SectPart.Alchemy || part == SectPart.Hall || part == SectPart.Pagoda
                                  : part == SectPart.Dorm || part == SectPart.Training || part == SectPart.Field || part == SectPart.Hall || part == SectPart.Gate || part == SectPart.Pond;
                if (!fits) continue;
                count++;
                if (r01 * count < 1f) pick = m; // reservoir pick: each fitting module as likely
            }
            if (pick < 0) return false;
            var mod = Layout[pick];
            // In front of a building (its lower edge), anywhere on open ground.
            bool building = mod.Part == SectPart.Hall || mod.Part == SectPart.Scripture || mod.Part == SectPart.Elder || mod.Part == SectPart.Alchemy || mod.Part == SectPart.Pagoda;
            x = s.X + 0.5f + Mathf.Lerp(mod.X0 + 0.5f, mod.X1 + 0.5f, rx);
            y = building ? s.Y + mod.Y0 + 0.3f + ry * 1.2f : s.Y + 0.5f + Mathf.Lerp(mod.Y0 + 0.5f, mod.Y1 + 0.5f, ry);
            return true;
        }

        void HashComplexes(ref ulong h)
        {
            foreach (var f in All) StateHash.Add(ref h, f.Built ^ ((long)f.Ruined << 20) ^ ((long)f.Skipped << 40) ^ ((long)f.Tier << 60));
            StateHash.Add(ref h, Ruins.Count);
        }
    }
}
