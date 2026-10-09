using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    // A war between two yêu tộc (devlog 36).
    public sealed class BeastWar
    {
        public int A, B;             // the two kings (beast indices); A went to war
        public long Start, Until;
        public float FieldX, FieldY; // where their hosts meet: on B's side of the line between the lairs
        public string Place;         // the land fought over
        public int FallenA, FallenB;
        public bool KingsOut;        // the kings themselves have come to the field
        public bool Over;
    }

    // Yêu tộc tranh bá (devlog 36): every yêu tộc holds the land round its king's lair, the wider the more beasts
    // kneel to it. Where two such lands overlap, the stronger may go to war for the ley lines: both send their
    // beasts to a field between the lairs, where they meet and tear at each other day by day (the fights are played
    // on the map); halfway through, the kings come out themselves. When it is over the beaten tộc either loses its
    // king and is swallowed by the victor, or gives up its lair and moves off. Where many fell the land remembers:
    // a cổ chiến trường. All of it plays out in a world of beasts alone, before men ever come.
    public sealed partial class BeastSystem
    {
        public readonly List<BeastWar> Wars = new List<BeastWar>();
        const int Warriors = 20;           // beasts each side sends
        const float BrawlReach = 1.8f;
        const float WarChance = 0.3f;      // a year, for two tộc whose lands overlap

        // The land a yêu tộc holds, in cells round its king's lair.
        public static float Territory(Beast king) => Mathf.Min(170f, 40f + 5f * king.ClanCount + (king.ChanLinh ? 40f : 0f));

        public BeastWar WarOf(Beast b) => b.War >= 0 && b.War < Wars.Count && !Wars[b.War].Over ? Wars[b.War] : null;

        bool AtWar(Beast king)
        {
            foreach (var w in Wars)
                if (!w.Over && (w.A == king.Index || w.B == king.Index)) return true;
            return false;
        }

        // The great regions are the only names a world of beasts has.
        string Land(float x, float y)
        {
            int cx = Mathf.Clamp((int)x, 0, _w.W - 1), cy = Mathf.Clamp((int)y, 0, _w.H - 1);
            return _w.Lore.RegionNames[_w.Region[_w.Idx(cx, cy)]];
        }

        float ClanPower(Beast king)
        {
            float p = Strength(king);
            foreach (var o in _living)
                if (o.Alive && o != king && o.Clan == king.Index) p += Strength(o);
            return p;
        }

        // ---------------------------------------------------------------- yearly: who holds what, who goes to war

        void WarsYearly(long tick)
        {
            _living ??= All.FindAll(o => o.Alive);
            var kings = new List<Beast>();
            foreach (var b in _living)
                if (b.Alive && b.IsKing && !b.Rampage) { b.ClanCount = ClanSize(b); kings.Add(b); }
            var rng = RngFor(tick, 960000);
            Breed(kings, tick, ref rng);
            for (int i = 0; i < kings.Count; i++)
            for (int j = i + 1; j < kings.Count; j++)
            {
                Beast a = kings[i], b = kings[j];
                if (tick < a.PeaceUntil || tick < b.PeaceUntil || AtWar(a) || AtWar(b)) continue;
                float dx = a.HomeX - b.HomeX, dy = a.HomeY - b.HomeY, reach = (Territory(a) + Territory(b)) * 1.25f; // their borders touch, or nearly
                // Two chân linh go to war only now and then: such a war shakes heaven and earth.
                if (dx * dx + dy * dy > reach * reach || rng.NextFloat() >= (a.ChanLinh && b.ChanLinh ? WarChance * 0.3f : WarChance)) continue;
                float pa = ClanPower(a), pb = ClanPower(b);
                if (pa < pb) (a, b) = (b, a); // the stronger goes to war
                Declare(a, b, tick, ref rng);
            }
        }

        // Each tộc breeds: young ones born in its land each year swell its ranks again after a war. In the hồng hoang
        // only (once men spread, beasts are hunted back, and only the old awakenings make new ones).
        void Breed(List<Beast> kings, long tick, ref DetRandom rng)
        {
            if (!_w.Primordial || _sim.Eras.SectsSince >= 0) return;
            foreach (var king in kings)
            {
                if (AliveCount >= MaxAlive) return;
                if (king.ClanCount >= 45) continue;
                int young = rng.Range(1, 4);
                float r = Territory(king) * 0.6f;
                for (int k = 0; k < young && AliveCount < MaxAlive; k++)
                {
                    float x = king.HomeX + rng.Range(-r, r), y = king.HomeY + rng.Range(-r, r);
                    if (!_w.InBounds((int)x, (int)y) || !_w.IsWalkable(x, y) || NearPeople(x, y, 30f)) continue;
                    var from = rng.NextFloat() < 0.5f ? Species.Wolf : rng.NextFloat() < 0.6f ? Species.Deer : Species.Rabbit;
                    var b = Spawn(from, rng.Range(1, 3), x, y, tick, ref rng);
                    b.BirthTick = tick;
                    b.Clan = king.Index;
                }
            }
        }

        void Declare(Beast a, Beast b, long tick, ref DetRandom rng)
        {
            var war = new BeastWar { A = a.Index, B = b.Index, Start = tick, Until = tick + rng.Range(120, 361) };
            // The field: on the defender's side, at walkable ground near the point 60% of the way to its lair.
            float fx = Mathf.Lerp(a.HomeX, b.HomeX, 0.6f), fy = Mathf.Lerp(a.HomeY, b.HomeY, 0.6f);
            for (int k = 0; k < 12 && !_w.IsWalkable(fx, fy); k++) { fx += rng.Range(-6f, 6f); fy += rng.Range(-6f, 6f); }
            war.FieldX = Mathf.Clamp(fx, 2f, _w.W - 3f);
            war.FieldY = Mathf.Clamp(fy, 2f, _w.H - 3f);
            war.Place = Land(war.FieldX, war.FieldY);
            int index = Wars.Count;
            Wars.Add(war);
            int sentA = Muster(a, index, war, ref rng), sentB = Muster(b, index, war, ref rng);
            bool great = a.ChanLinh || b.ChanLinh;
            _sim.Events.Add(tick, EventKind.Beast, great ? 3 : 2,
                $"{a.ClanName} khai chiến với {b.ClanName}, tranh đoạt linh mạch ở {war.Place}: {a.Title} xua {sentA} yêu thú tiến đánh, {b.Title} dẫn {sentB} yêu thú nghênh chiến.",
                war.FieldX, war.FieldY, Fx.Stampede);
        }

        // Up to Warriors of the tộc's beasts, the strongest first, go to the field.
        int Muster(Beast king, int index, BeastWar war, ref DetRandom rng)
        {
            var band = new List<Beast>();
            foreach (var o in _living)
                if (o.Alive && o != king && o.Clan == king.Index && !o.Rampage && o.ChaseEntity < 0 && o.War < 0) band.Add(o);
            band.Sort((p, q) => q.Grade != p.Grade ? q.Grade.CompareTo(p.Grade) : p.Index.CompareTo(q.Index));
            int n = Mathf.Min(Warriors, band.Count);
            for (int k = 0; k < n; k++)
            {
                var o = band[k];
                o.War = index;
                o.HuntUntil = 0;
                MarchTo(o, war.FieldX, war.FieldY, 5f, ref rng);
            }
            return n;
        }

        void MarchTo(Beast b, float x, float y, float spread, ref DetRandom rng)
        {
            float tx = x + rng.Range(-spread, spread), ty = y + rng.Range(-spread, spread);
            if (!Flies(b.Kind) && !_w.IsWalkable(tx, ty)) { tx = x; ty = y; }
            _e.TX[b.Entity] = tx;
            _e.TY[b.Entity] = ty;
        }

        // ---------------------------------------------------------------- daily: the hosts meet

        [System.NonSerialized] readonly HashSet<int> _foughtToday = new HashSet<int>();
        [System.NonSerialized] readonly List<Beast> _sideA = new List<Beast>(), _sideB = new List<Beast>();

        void WarsDaily(long tick)
        {
            for (int w = 0; w < Wars.Count; w++)
            {
                var war = Wars[w];
                if (war.Over) continue;
                Beast a = All[war.A], b = All[war.B];
                _sideA.Clear();
                _sideB.Clear();
                foreach (var o in _living)
                {
                    if (!o.Alive || o.War != w) continue;
                    (o.Clan == war.A ? _sideA : _sideB).Add(o);
                }
                bool kingDown = !a.Alive || !b.Alive;
                if (kingDown || tick >= war.Until || _sideA.Count == 0 || _sideB.Count == 0) { End(war, w, tick); continue; }
                var rng = RngFor(tick, 970000 + w);
                // Halfway through, the kings come out themselves.
                if (!war.KingsOut && tick >= (war.Start + war.Until) / 2)
                {
                    war.KingsOut = true;
                    a.War = b.War = w;
                    MarchTo(a, war.FieldX, war.FieldY, 2f, ref rng);
                    MarchTo(b, war.FieldX, war.FieldY, 2f, ref rng);
                    _sim.Events.Add(tick, EventKind.Beast, a.ChanLinh || b.ChanLinh ? 3 : 2,
                        $"{a.Title} và {b.Title} đích thân xuất trận ở {war.Place}, yêu khí hai tộc cuộn trào che kín bầu trời.", war.FieldX, war.FieldY, Fx.Stampede);
                }
                _foughtToday.Clear();
                foreach (var x in _sideA)
                {
                    if (!x.Alive || _foughtToday.Contains(x.Index)) continue;
                    Beast foe = null;
                    float best = float.MaxValue;
                    foreach (var y in _sideB)
                    {
                        if (!y.Alive || _foughtToday.Contains(y.Index)) continue;
                        float dx = _e.X[y.Entity] - _e.X[x.Entity], dy = _e.Y[y.Entity] - _e.Y[x.Entity], d = dx * dx + dy * dy;
                        if (d < best) { best = d; foe = y; }
                    }
                    if (foe == null) break;
                    if (best <= BrawlReach * BrawlReach)
                    {
                        _foughtToday.Add(x.Index);
                        _foughtToday.Add(foe.Index);
                        Brawl(x, foe, war, tick, ref rng);
                    }
                    else if (best < 20f * 20f) // in sight: they charge each other
                    {
                        _e.TX[x.Entity] = _e.X[foe.Entity];
                        _e.TY[x.Entity] = _e.Y[foe.Entity];
                        _e.TX[foe.Entity] = _e.X[x.Entity];
                        _e.TY[foe.Entity] = _e.Y[x.Entity];
                    }
                    else if ((tick + x.Index) % 10 == 0) MarchTo(x, war.FieldX, war.FieldY, 5f, ref rng); // still gathering
                }
            }
        }

        // Two beasts of the warring tộc meet: the weaker is torn apart or runs home wounded.
        void Brawl(Beast x, Beast y, BeastWar war, long tick, ref DetRandom rng)
        {
            float sx = Strength(x) * rng.Range(0.6f, 1.4f), sy = Strength(y) * rng.Range(0.6f, 1.4f);
            Beast win = sx >= sy ? x : y, lose = win == x ? y : x;
            float sw = Mathf.Max(sx, sy), sl = Mathf.Max(0.01f, Mathf.Min(sx, sy));
            Hurt(win, Mathf.Clamp(0.35f * sl / sw, 0.05f, 0.6f));
            float px = (_e.X[x.Entity] + _e.X[y.Entity]) * 0.5f, py = (_e.Y[x.Entity] + _e.Y[y.Entity]) * 0.5f;
            // A king seldom dies on the field: beaten, it is carried off by its own; a chân linh more seldom still.
            float kill = Mathf.Clamp(0.45f * sw / sl, 0.3f, 0.9f) * (lose.ChanLinh ? 0.25f : lose.IsKing ? 0.45f : 1f);
            bool dies = rng.NextFloat() < kill;
            // The fight itself, for the map (importance 0: the ticker only; its A and B are beasts, not cultivators).
            _sim.Events.Add(tick, EventKind.Beast, 0,
                dies ? $"{win.Title} xé xác {lose.Title} giữa trận chiến ở {war.Place}." : $"{lose.Title} bị {win.Title} đánh trọng thương, tháo chạy khỏi {war.Place}.",
                px, py, Fx.BeastBrawl, win.Index, lose.Index);
            win.Kills++;
            if (lose.Clan == war.A) war.FallenA++; else war.FallenB++;
            if (dies)
            {
                bool great = lose.IsKing || lose.ChanLinh;
                Die(lose, tick, great ? $"{lose.Title} tử trận dưới tay {win.Title} ở {war.Place}!" : "", great ? 3 : 0, null, Fx.None, !great);
                return;
            }
            Hurt(lose, 0.6f);
            lose.War = -1; // out of the war, back to its lair to lick its wounds
            _e.TX[lose.Entity] = lose.HomeX;
            _e.TY[lose.Entity] = lose.HomeY;
        }

        // ---------------------------------------------------------------- the end of it

        void End(BeastWar war, int index, long tick)
        {
            war.Over = true;
            Beast a = All[war.A], b = All[war.B];
            float pa = a.Alive ? Strength(a) : 0f, pb = b.Alive ? Strength(b) : 0f;
            foreach (var o in _living)
            {
                if (!o.Alive || o.War != index) continue;
                if (o.Clan == war.A) pa += Strength(o); else pb += Strength(o);
            }
            Beast win = pa >= pb ? a : b, lose = win == a ? b : a;
            if (!win.Alive) (win, lose) = (lose, win); // a dead king wins nothing
            foreach (var o in _living) // the hosts go home
            {
                if (!o.Alive || o.War != index) continue;
                o.War = -1;
                _e.TX[o.Entity] = o.HomeX;
                _e.TY[o.Entity] = o.HomeY;
            }
            var rng = RngFor(tick, 980000 + index);
            int fallen = war.FallenA + war.FallenB;
            bool great = a.ChanLinh || b.ChanLinh;
            if (!win.Alive)
                _sim.Events.Add(tick, EventKind.Beast, 2, $"Cuộc chiến giữa {a.ClanName} và {b.ClanName} ở {war.Place} tàn lụi, cả hai vương đều đã ngã xuống; {fallen} yêu thú bỏ mạng.", war.FieldX, war.FieldY);
            else if (!lose.Alive)
            {
                // The beaten tộc lost its king: its beasts kneel to the victor.
                int sworn = 0;
                foreach (var o in _living)
                    if (o.Alive && o.Clan < 0 && Near(o, lose, Territory(lose))) { o.Clan = win.Index; sworn++; }
                _sim.Events.Add(tick, EventKind.Beast, great ? 3 : 2,
                    $"{win.ClanName} đại thắng ở {war.Place}: {lose.ClanName} mất vương, {sworn} yêu thú quỳ phục {win.Title}. {fallen} yêu thú tử trận.", war.FieldX, war.FieldY, Fx.Stampede);
            }
            else if (pa == pb || Mathf.Abs(pa - pb) < 0.15f * Mathf.Max(pa, pb))
                _sim.Events.Add(tick, EventKind.Beast, great ? 2 : 1,
                    $"{a.ClanName} và {b.ClanName} giằng co ở {war.Place} bất phân thắng bại, đành lui binh; {fallen} yêu thú tử trận.", war.FieldX, war.FieldY);
            else
            {
                // Beaten but alive: it gives up its lair and moves off, away from the victor.
                float dx = lose.HomeX - win.HomeX, dy = lose.HomeY - win.HomeY, d = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
                for (int k = 0; k < 10; k++)
                {
                    float nx = lose.HomeX + dx / d * rng.Range(40f, 80f) + rng.Range(-15f, 15f), ny = lose.HomeY + dy / d * rng.Range(40f, 80f) + rng.Range(-15f, 15f);
                    if (!_w.InBounds((int)nx, (int)ny) || !_w.IsWalkable(nx, ny)) continue;
                    lose.HomeX = nx;
                    lose.HomeY = ny;
                    break;
                }
                _e.TX[lose.Entity] = lose.HomeX;
                _e.TY[lose.Entity] = lose.HomeY;
                _sim.Events.Add(tick, EventKind.Beast, great ? 3 : 2,
                    $"{win.ClanName} thắng trận ở {war.Place}; {lose.Title} bại lui, dẫn {lose.ClanName} bỏ linh mạch, dời hang ổ về {Land(lose.HomeX, lose.HomeY)}. {fallen} yêu thú tử trận.",
                    war.FieldX, war.FieldY, Fx.Stampede);
            }
            long peace = tick + (long)(rng.Range(3f, 7f) * SimClock.DaysPerYear);
            a.PeaceUntil = b.PeaceUntil = peace;
            if (fallen >= 3) _sim.Disasters.MarkBattlefield((int)war.FieldX, (int)war.FieldY, 6, fallen, tick, $"{a.ClanName} đại chiến {b.ClanName} năm {tick / SimClock.DaysPerYear + 1}");
        }

        void HashWars(ref ulong h)
        {
            StateHash.Add(ref h, Wars.Count);
            foreach (var w in Wars) StateHash.Add(ref h, w.A ^ ((long)w.B << 16) ^ (w.Until << 32) ^ (w.Over ? 1L << 62 : 0) ^ ((long)(w.FallenA + w.FallenB) << 48));
        }
    }
}
