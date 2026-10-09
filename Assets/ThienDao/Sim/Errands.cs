using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // What a cultivator went out to do (devlog 33). Each errand is a little story with a beginning, a middle and an
    // end, played out on the map day by day: they set out for somewhere, do something there that can be watched, and
    // come home with something to show for it (or with nothing, or not at all). The steps are kept in their nhật ký.
    public enum Errand : byte { None, Herbs, Hunt, Market, Patrol, Ponder, Visit }

    public sealed partial class CultivationSystem
    {
        const int DiarySize = 6;
        const int ErrandDeadlineDays = 150; // a leg that has not arrived by then is given up (lost the way, cut off)

        // How often someone at home sets out on an errand, a month. Luyện Khí disciples run the sect's small jobs.
        static float ErrandChancePerMonth(Cultivator c) =>
            c.SectId >= 0 && c.Realm == Realm.LuyenKhi ? 1f / 5f : c.Realm >= Realm.KetDan ? 1f / 8f : 1f / 4f;

        // Who is out on an errand, in the order of All (scratch, rebuilt after a load), so a day's step is short.
        [System.NonSerialized] List<Cultivator> _busy;

        List<Cultivator> Busy()
        {
            if (_busy != null) return _busy;
            _busy = new List<Cultivator>();
            foreach (var c in All)
                if (c.Alive && c.Errand != Errand.None) _busy.Add(c);
            return _busy;
        }

        void AddBusy(Cultivator c)
        {
            var list = Busy();
            int at = list.Count;
            while (at > 0 && list[at - 1].Index > c.Index) at--; // kept in Index order: the same order after a load
            if (at > 0 && list[at - 1] == c) return;
            list.Insert(at, c);
        }

        // ---------------------------------------------------------------- nhật ký

        public void Note(Cultivator c, long tick, string text)
        {
            if (c.Diary == null || c.Diary.Length != DiarySize)
            {
                c.Diary = new string[DiarySize];
                c.DiaryTick = new long[DiarySize];
                c.DiaryNext = 0;
            }
            c.Diary[c.DiaryNext] = text;
            c.DiaryTick[c.DiaryNext] = tick;
            c.DiaryNext = (c.DiaryNext + 1) % DiarySize;
        }

        // Oldest first.
        public void DiaryOf(Cultivator c, List<(long tick, string text)> into)
        {
            into.Clear();
            if (c.Diary == null) return;
            for (int k = 0; k < DiarySize; k++)
            {
                int i = (c.DiaryNext + k) % DiarySize;
                if (c.Diary[i] != null) into.Add((c.DiaryTick[i], c.Diary[i]));
            }
        }

        // "ở Hoa Khê Thôn", "gần Hoa Khê Thôn", "phía bắc Hoa Khê Thôn": where something happened, by the nearest village.
        static readonly string[] Bearing = { "phía đông", "phía đông bắc", "phía bắc", "phía tây bắc", "phía tây", "phía tây nam", "phía nam", "phía đông nam" };

        string Near(float x, float y)
        {
            Settlement best = null;
            float bestD = 200f * 200f;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                float dx = s.X - x, dy = s.Y - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best == null) return "nơi thâm sơn cùng cốc";
            if (bestD < 25f) return $"ở {best.Name}";
            if (bestD < 400f) return $"gần {best.Name}";
            float a = Mathf.Atan2(y - best.Y, x - best.X) / (Mathf.PI * 2f) * 8f;
            return $"{Bearing[((Mathf.RoundToInt(a) % 8) + 8) % 8]} {best.Name}";
        }

        // ---------------------------------------------------------------- setting out

        bool StartErrand(Cultivator c, long tick, ref DetRandom rng)
        {
            bool walker = c.Realm < Realm.TrucCo;
            bool discipleLK = c.SectId >= 0 && walker;
            float reach = walker ? 28f : c.Realm >= Realm.KetDan ? 160f : 110f;
            // Weights by who they are; a choice with nowhere to go falls through to the next.
            float herbs = 40f, hunt = walker ? 0f : c.Demonic ? 45f : 25f, market = discipleLK ? 0f : 18f, patrol = c.SectId >= 0 ? (walker ? 45f : 15f) : 0f;
            float ponder = walker ? 0f : 18f, visit = c.Origin >= 0 && !c.Demonic && !discipleLK ? 12f : 0f;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float total = herbs + hunt + market + patrol + ponder + visit;
                if (total <= 0f) return false;
                float r = rng.NextFloat() * total;
                Errand pick = (r -= herbs) < 0f ? Errand.Herbs : (r -= hunt) < 0f ? Errand.Hunt : (r -= market) < 0f ? Errand.Market
                    : (r -= patrol) < 0f ? Errand.Patrol : (r -= ponder) < 0f ? Errand.Ponder : Errand.Visit;
                if (Begin(c, pick, reach, tick, ref rng)) return true;
                switch (pick) // nowhere for that: try something else
                {
                    case Errand.Herbs: herbs = 0f; break;
                    case Errand.Hunt: hunt = 0f; break;
                    case Errand.Market: market = 0f; break;
                    case Errand.Patrol: patrol = 0f; break;
                    case Errand.Ponder: ponder = 0f; break;
                    default: visit = 0f; break;
                }
            }
            return false;
        }

        bool Begin(Cultivator c, Errand e, float reach, long tick, ref DetRandom rng)
        {
            float x, y;
            string text, note;
            int target = -1, stop = -1;
            switch (e)
            {
                case Errand.Herbs:
                {
                    if (!HerbGround(c, reach, out x, out y)) return false;
                    string near = Near(x, y);
                    text = $"đang đi hái linh dược {near}";
                    string from = c.SectId >= 0 ? "sơn môn" : "động phủ";
                    note = Pick(ref rng, $"Rời {from}, đi hái linh dược {near}.", $"Nghe nói {near} linh khí dồi dào, xách giỏ đi tìm linh thảo.", $"Đan dược cạn, tự mình đi hái thuốc {near}.");
                    break;
                }
                case Errand.Hunt:
                {
                    var b = Prey(c, reach);
                    if (b == null) return false;
                    target = b.Index;
                    x = _e.X[b.Entity];
                    y = _e.Y[b.Entity];
                    text = $"đang đi săn {b.Name}";
                    note = $"Nghe nói có {b.Title} ({b.GradeText}) {Near(x, y)}, lên đường săn yêu đan.";
                    break;
                }
                case Errand.Market:
                {
                    var town = Town(c.HomeX, c.HomeY, Mathf.Max(reach, 80f));
                    if (town == null) return false;
                    target = town.Id;
                    x = town.X + 0.5f;
                    y = town.Y + 0.5f;
                    text = $"đang lên {town.Name}";
                    note = c.Pills == 0 && c.Stones >= PillPrice(c) ? $"Mang linh thạch lên {town.Name} mua {Lore.PillFor(c.Realm + 1)}." : $"Lên {town.Name} buôn bán, nghe ngóng tin tức.";
                    break;
                }
                case Errand.Patrol:
                {
                    if (!PatrolStops(c, reach, ref rng, out target, out stop)) return false;
                    var s = _sim.Settlements.All[target];
                    x = s.X + 0.5f;
                    y = s.Y + 0.5f;
                    text = $"đang tuần tra tới {s.Name}";
                    note = $"Nhận lệnh sư môn tuần tra các thôn quanh {SectName(c)}: {s.Name}" +
                           (stop >= 0 ? $", {_sim.Settlements.All[stop].Name}." : ".");
                    break;
                }
                case Errand.Ponder:
                {
                    if (!PonderPlace(c, reach, ref rng, out x, out y, out string place)) return false;
                    text = $"đang tới {place} ngộ đạo";
                    note = Pick(ref rng, $"Tâm cảnh chững lại, tìm tới {place} tĩnh tọa ngộ đạo.", $"Bình cảnh khó phá, lên {place} ngắm trời đất, cầu một chút cơ duyên.", $"Muốn tĩnh tâm, tới {place} ngồi thiền.");
                    break;
                }
                default:
                {
                    var home = c.Origin >= 0 && c.Origin < _sim.Settlements.All.Count ? _sim.Settlements.All[c.Origin] : null;
                    if (home == null || !home.Alive) return false;
                    float dx = home.X - c.HomeX, dy = home.Y - c.HomeY;
                    if (dx * dx + dy * dy > reach * reach * 2.25f) return false;
                    target = home.Id;
                    x = home.X + 0.5f;
                    y = home.Y + 0.5f;
                    text = $"đang về thăm cố hương {home.Name}";
                    note = $"Nhớ cố hương, về thăm {home.Name}.";
                    break;
                }
            }
            if (!_w.IsWalkable(x, y) && !(e == Errand.Hunt || c.Realm >= Realm.TrucCo)) return false;
            c.Errand = e;
            c.ErrandLeg = 0;
            c.ErrandTarget = target;
            c.ErrandStop = stop;
            c.ErrandText = text;
            c.ErrandUntil = tick + ErrandDeadlineDays;
            Note(c, tick, note);
            SendTo(c, x, y, Trip.Errand);
            AddBusy(c);
            return true;
        }

        // A few fixed linh dược viên round each home (the same every time, so the way there wears into a trail and the
        // route is remembered): the one with the richest qi, on wild ground.
        bool HerbGround(Cultivator c, float reach, out float x, out float y)
        {
            x = y = 0f;
            float best = -1f;
            uint seed = Hash.U32(_w.Seed ^ 0x4E7Bu, (int)c.HomeX, (int)c.HomeY);
            for (int k = 0; k < 5; k++)
            {
                uint h = Hash.U32(seed, k, 17);
                float a = (h & 0xFFFF) / 65536f * Mathf.PI * 2f, r = reach * (0.35f + 0.6f * ((h >> 16) & 0xFF) / 255f);
                float tx = c.HomeX + Mathf.Cos(a) * r, ty = c.HomeY + Mathf.Sin(a) * r;
                if (!_w.InBounds((int)tx, (int)ty) || !_w.IsWalkable(tx, ty)) continue;
                int i = _w.Idx((int)tx, (int)ty);
                if (_w.Owner[i] != 0 || _w.Terrain[i] == Terrain.Farmland) continue;
                float q = _sim.Qi.SampleQi((int)tx, (int)ty);
                if (q > best) { best = q; x = (int)tx + 0.5f; y = (int)ty + 0.5f; }
            }
            return best >= 0f;
        }

        // A beast worth the trouble and within their means: the nearest awake one clearly weaker than they are.
        Beast Prey(Cultivator c, float reach)
        {
            float mine = CombatSystem.Strength(c), bestD = reach * reach;
            Beast best = null;
            foreach (var b in _sim.Beasts.All)
            {
                if (!b.Alive || b.Rampage || b.ChaseEntity >= 0) continue;
                float s = BeastSystem.Strength(b);
                if (s > mine * 0.8f || s < mine * 0.05f) continue; // too strong, or not worth the flight
                float dx = _e.X[b.Entity] - c.HomeX, dy = _e.Y[b.Entity] - c.HomeY, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        Settlement Town(float x, float y, float reach)
        {
            Settlement best = null;
            float bestD = reach * reach;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive || s.Sect || SettlementSystem.Standing(s) < 1) continue; // a trấn or more has a market
                float dx = s.X - x, dy = s.Y - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // Two of the villages round the sect, the nearer first.
        bool PatrolStops(Cultivator c, float reach, ref DetRandom rng, out int first, out int second)
        {
            first = second = -1;
            float r = Mathf.Max(reach, 120f); // sects keep to the mountains, a day or two from the nearest village
            int a = -1, b = -1;
            float da = float.MaxValue, db = float.MaxValue;
            int skip = rng.Range(0, 3); // not always the same two
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive || s.Sect) continue;
                float dx = s.X - c.HomeX, dy = s.Y - c.HomeY, d = dx * dx + dy * dy;
                if (d > r * r) continue;
                if (((s.Id + skip) % 3) == 0 && d > 100f) continue;
                if (d < da) { b = a; db = da; a = s.Id; da = d; }
                else if (d < db) { b = s.Id; db = d; }
            }
            if (a < 0) return false;
            first = a;
            second = b;
            return true;
        }

        // Somewhere to sit with the Dao: a landmark left by the ages (lôi khí of a tribulation, a battlefield's grudge
        // for a ma tu, a volcano), else the richest qi on a height near home.
        bool PonderPlace(Cultivator c, float reach, ref DetRandom rng, out float x, out float y, out string place)
        {
            x = y = 0f;
            place = null;
            float bestD = reach * reach;
            foreach (var m in _sim.Disasters.Landmarks)
            {
                if (!m.Alive || (m.Kind == Landmark.Battlefield) != c.Demonic) continue;
                float dx = m.X - c.HomeX, dy = m.Y - c.HomeY, d = dx * dx + dy * dy;
                if (d >= bestD || !_w.IsWalkable(m.X + 0.5f, m.Y + 0.5f)) continue;
                bestD = d;
                x = m.X + 0.5f;
                y = m.Y + 0.5f;
                place = m.Name;
            }
            if (place != null) return true;
            float best = -1f;
            for (int k = 0; k < 6; k++)
            {
                float tx = c.HomeX + rng.Range(-reach, reach) * 0.6f, ty = c.HomeY + rng.Range(-reach, reach) * 0.6f;
                if (!_w.InBounds((int)tx, (int)ty) || !_w.IsWalkable(tx, ty)) continue;
                var t = _w.Terrain[_w.Idx((int)tx, (int)ty)];
                float q = _sim.Qi.SampleQi((int)tx, (int)ty) * (t == Terrain.Hills || t == Terrain.Mountain || t == Terrain.Snow ? 1.5f : 1f);
                if (q > best) { best = q; x = (int)tx + 0.5f; y = (int)ty + 0.5f; }
            }
            if (best < 0f) return false;
            place = $"đỉnh núi {Near(x, y)}";
            return true;
        }

        static string Pick(ref DetRandom rng, params string[] lines) => lines[rng.Range(0, lines.Length)];

        static float PillPrice(Cultivator c) => 60f * ((int)c.Realm + 1) * ((int)c.Realm + 1);

        // ---------------------------------------------------------------- day by day

        public void ErrandStep(long tick)
        {
            var list = Busy();
            for (int k = list.Count - 1; k >= 0; k--)
            {
                var c = list[k];
                if (!c.Alive || c.Errand == Errand.None) { list.RemoveAt(k); continue; }
                // Taken away by something bigger (a war, a feud, a beast that chased them, a bí cảnh): the errand is off.
                bool travelling = c.Travelling && c.Trip == Trip.Errand, working = c.Away && !c.Travelling && c.Trip == Trip.None && c.ErrandUntil >= tick;
                if (!travelling && !working)
                {
                    if (c.Away && !c.Travelling && c.Trip == Trip.None) Finish(c, tick, true); // stood there past their time
                    else EndErrand(c);
                    list.RemoveAt(k);
                    continue;
                }
                if (travelling)
                {
                    if (c.Errand == Errand.Hunt && Stalk(c, tick)) { list.RemoveAt(k); continue; }
                    if (tick > c.ErrandUntil) { Note(c, tick, "Lạc đường, đành quay về."); EndErrand(c); ReturnHome(c); list.RemoveAt(k); continue; }
                    if (!_sim.Creatures.HasArrived(c.Entity)) continue;
                    Reach(c, tick);
                    continue;
                }
                if (tick >= c.ErrandUntil && Finish(c, tick, false)) list.RemoveAt(k);
            }
        }

        void EndErrand(Cultivator c)
        {
            c.Errand = Errand.None;
            c.ErrandText = null;
            c.ErrandTarget = c.ErrandStop = -1;
        }

        // Arrived at the place of the errand: get to work there.
        void Reach(Cultivator c, long tick)
        {
            var rng = RngFor(tick, 930000 + c.Index);
            c.Travelling = false;
            c.Trip = Trip.None;
            c.Away = true;
            _e.Flying[c.Entity] = false;
            var all = _sim.Settlements.All;
            switch (c.Errand)
            {
                case Errand.Herbs:
                    c.ErrandUntil = tick + rng.Range(4, 11);
                    c.ErrandText = $"đang hái linh dược {Near(_e.X[c.Entity], _e.Y[c.Entity])}";
                    break;
                case Errand.Market:
                    c.ErrandUntil = tick + rng.Range(3, 7);
                    c.ErrandText = $"đang dạo chợ {all[c.ErrandTarget].Name}";
                    break;
                case Errand.Patrol:
                    c.ErrandUntil = tick + rng.Range(1, 3);
                    c.ErrandText = $"đang tuần tra {all[c.ErrandTarget].Name}";
                    break;
                case Errand.Ponder:
                    c.ErrandUntil = tick + rng.Range(8, 21);
                    c.ErrandText = "đang tĩnh tọa ngộ đạo";
                    break;
                case Errand.Visit:
                    c.ErrandUntil = tick + rng.Range(3, 8);
                    c.ErrandText = $"đang ở thăm {all[c.ErrandTarget].Name}";
                    break;
                default:
                    c.ErrandUntil = tick + 2;
                    break;
            }
            c.StayUntil = c.ErrandUntil + SimClock.DaysPerMonth; // the monthly step's safety net, never reached first
        }

        // Done at the spot: what came of it, then the next step or home. True when the errand is over.
        bool Finish(Cultivator c, long tick, bool late)
        {
            var rng = RngFor(tick, 940000 + c.Index);
            var all = _sim.Settlements.All;
            float x = _e.X[c.Entity], y = _e.Y[c.Entity];
            bool over = true;
            switch (c.Errand)
            {
                case Errand.Herbs:
                {
                    float qi = _sim.Qi.SampleQi((int)x, (int)y) / Mathf.Max(1f, Realms.RequiredQi[(int)c.Realm]);
                    float chance = Mathf.Clamp(0.35f + 0.3f * qi + 0.3f * c.Luck, 0.2f, 0.9f);
                    if (late || rng.NextFloat() >= chance)
                    {
                        Note(c, tick, Pick(ref rng, "Lục tìm mấy ngày không thấy linh dược nào, tay trắng trở về.", "Linh thảo chỗ này đã bị kẻ khác hái sạch.", "Chỉ tìm được ít cỏ dại tầm thường, đành quay về."));
                        break;
                    }
                    string herb = _w.Lore.Herbs[rng.Range(0, _w.Lore.Herbs.Length)];
                    bool rare = rng.NextFloat() < 0.08f + 0.1f * c.Luck;
                    c.Progress += Realms.Need(c.Realm, c.Stage) * (rare ? 0.3f : 0.08f);
                    ShowLoot(c, Loot.Herb, tick, rare ? 40 : 20);
                    if (rare)
                        _sim.Events.Add(tick, EventKind.Fortune, c.Realm >= Realm.KetDan ? 2 : 1, $"{c.Title} ({SectName(c)}) hái được {herb} ngàn năm {Near(x, y)}.",
                            x, y, Fx.Blessing, c.Index, -1, c.SectId);
                    Note(c, tick, rare ? $"Hái được {herb} ngàn năm, mừng rỡ khôn xiết!" : $"Hái được mấy cây {herb}.");
                    // Some sell what they picked at the nearest market before going home.
                    var town = c.SectId >= 0 && c.Realm == Realm.LuyenKhi ? null : Town(x, y, 60f);
                    if (town != null && rng.NextFloat() < 0.4f)
                    {
                        c.Errand = Errand.Market;
                        c.ErrandTarget = town.Id;
                        c.ErrandText = $"đang mang linh dược lên {town.Name} bán";
                        c.ErrandUntil = tick + ErrandDeadlineDays;
                        c.Stones += 10f * ((int)c.Realm + 1);
                        Note(c, tick, $"Mang linh dược lên {town.Name} bán.");
                        SendTo(c, town.X + 0.5f, town.Y + 0.5f, Trip.Errand);
                        return false;
                    }
                    break;
                }
                case Errand.Market:
                {
                    var town = all[c.ErrandTarget];
                    float price = PillPrice(c);
                    if (c.Pills == 0 && c.Stones >= price && c.Realm < Realm.HoaThan)
                    {
                        c.Stones -= price;
                        c.Pills++;
                        ShowLoot(c, Loot.Pill, tick, 20);
                        Note(c, tick, $"Bỏ {price:0} linh thạch mua được một viên {Lore.PillFor(c.Realm + 1)} ở {town.Name}.");
                    }
                    else
                    {
                        c.Stones += 5f * ((int)c.Realm + 1);
                        town.Food += 10f; // what they spent feeds the town
                        Note(c, tick, rng.NextFloat() < 0.5f ? $"Dạo chợ {town.Name}, đổi chút linh thạch." : $"Ngồi quán trà {town.Name} nghe ngóng chuyện thiên hạ.");
                    }
                    break;
                }
                case Errand.Patrol:
                {
                    var s = all[c.ErrandTarget];
                    Note(c, tick, Pick(ref rng, $"Tuần tra qua {s.Name}, dân làng bình an.", $"Ghé {s.Name}, trưởng thôn dâng trà cảm tạ sư môn che chở.", $"Đi một vòng {s.Name}, không thấy yêu thú quấy nhiễu."));
                    if (c.ErrandStop >= 0 && all[c.ErrandStop].Alive)
                    {
                        var next = all[c.ErrandStop];
                        c.ErrandTarget = next.Id;
                        c.ErrandStop = -1;
                        c.ErrandText = $"đang tuần tra tới {next.Name}";
                        c.ErrandUntil = tick + ErrandDeadlineDays;
                        SendTo(c, next.X + 0.5f, next.Y + 0.5f, Trip.Errand);
                        return false;
                    }
                    break;
                }
                case Errand.Ponder:
                {
                    c.DaoHeart = Mathf.Min(1f, c.DaoHeart + 0.04f);
                    c.Comprehension = Mathf.Min(1f, c.Comprehension + 0.01f);
                    bool epiphany = !late && rng.NextFloat() < 0.06f + 0.12f * c.Comprehension;
                    if (epiphany)
                    {
                        c.Progress += Realms.Need(c.Realm, c.Stage) * 0.5f;
                        _sim.Events.Add(tick, EventKind.Fortune, c.Realm >= Realm.KetDan ? 2 : 1, $"{c.Title} ({SectName(c)}) tĩnh tọa {Near(x, y)}, bỗng nhiên đốn ngộ, tu vi tăng vọt.",
                            x, y, Fx.Blessing, c.Index, -1, c.SectId);
                        Note(c, tick, "Đốn ngộ! Tu vi tăng vọt.");
                    }
                    else Note(c, tick, Pick(ref rng, "Tĩnh tọa nhiều ngày, tâm cảnh vững thêm một chút.", "Ngồi giữa gió núi bảy ngày, lòng nhẹ đi nhiều.", "Không ngộ ra được gì, nhưng tâm đã yên."));
                    break;
                }
                case Errand.Visit:
                {
                    var s = all[c.ErrandTarget];
                    if (s.Alive) s.Food += 30f;
                    Note(c, tick, s.Alive ? $"Ở lại {s.Name} mấy ngày, để lại ít linh thạch cho người thân." : "Về tới nơi, cố hương chỉ còn là đống tro tàn.");
                    break;
                }
            }
            if (!over) return false;
            EndErrand(c);
            if (c.Alive && c.Away && c.LootUntil <= tick) ReturnHome(c);
            else if (c.Alive && c.Away) c.StayUntil = c.LootUntil; // holds the prize up a while, then home (monthly step)
            return true;
        }

        // On the way to the beast: it moves, so the way is set again each day; in sight, the hunt is on (BeastChase).
        bool Stalk(Cultivator c, long tick)
        {
            var beasts = _sim.Beasts.All;
            var b = c.ErrandTarget >= 0 && c.ErrandTarget < beasts.Count ? beasts[c.ErrandTarget] : null;
            if (b == null || !b.Alive)
            {
                Note(c, tick, "Tới nơi thì con mồi đã bị kẻ khác hạ, đành quay về.");
                EndErrand(c);
                ReturnHome(c);
                return true;
            }
            float bx = _e.X[b.Entity], by = _e.Y[b.Entity], dx = bx - _e.X[c.Entity], dy = by - _e.Y[c.Entity];
            float sight = BeastSystem.Sight(b) + 2f;
            if (dx * dx + dy * dy > sight * sight) { Retarget(c, bx, by); return false; }
            if (b.ChaseEntity >= 0) return false; // busy with someone else: wait for it
            Note(c, tick, $"Tìm thấy {b.Title} {Near(bx, by)}, lập tức ra tay.");
            EndErrand(c);
            _sim.Beasts.Challenge(b, c, tick);
            return true;
        }
    }
}
