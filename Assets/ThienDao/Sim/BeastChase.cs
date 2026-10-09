using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    // Đuổi bắt (devlog 32): what happens when a beast and a passer-by see each other, played out on the map day by
    // day instead of settled in one roll. A waking beast looks around every few days. A cultivator out on the road
    // within its sight, a band of migrants or a caravan, draws it:
    //  - the much stronger cultivator hunts the beast for its yêu đan, and the beast runs;
    //  - a match closes in on each other;
    //  - the weaker one runs, and the beast gives chase (sword-flyers usually get away; walkers seldom do).
    // When they meet they fight (Fight: the fight scene plays where it happens). A chase that drags on, or a quarry
    // that gets out of sight, is given up. Migrants and caravans always run; caught, they lose people or the goods.
    public sealed partial class BeastSystem
    {
        public const byte ChaseHunts = 1;  // the beast hunts its quarry
        public const byte ChaseHunted = 2; // a cultivator hunts the beast
        public const byte ChaseClash = 3;  // a match: they close in on each other
        const int ChaseDays = 15;
        const int HuntedDays = 8;        // a beast that runs from a hunter shakes it off sooner
        const float NoticeChance = 0.04f;
        const int PreyMask = (1 << (int)Species.Cultivator) | (1 << (int)Species.Migrants) | (1 << (int)Species.Caravan);

        public static float Sight(Beast b) => 5f + b.Grade;

        // Whoever some beast is chasing today (scratch, rebuilt each day: a quarry is chased by one beast at a time).
        [System.NonSerialized] System.Collections.Generic.HashSet<int> _chased;

        // The living, in the order of All (All keeps the dead for the histories): the daily step need not walk past them.
        // Scratch, rebuilt after a load; the fallen are swept out once a month.
        [System.NonSerialized] System.Collections.Generic.List<Beast> _living;

        public void DailyStep(long tick)
        {
            if (_living == null) _living = All.FindAll(o => o.Alive);
            else if (tick % SimClock.DaysPerMonth == 0) _living.RemoveAll(o => !o.Alive);
            _chased ??= new System.Collections.Generic.HashSet<int>();
            _chased.Clear();
            foreach (var o in _living)
                if (o.Alive && o.ChaseEntity >= 0) _chased.Add(o.ChaseEntity);
            for (int k = 0; k < _living.Count; k++)
            {
                var b = _living[k];
                if (!b.Alive || b.Rampage || tick < b.CalmUntil) continue; // a hung thú hunts towns, a sleeper sleeps
                if (b.ChaseEntity >= 0) { ChaseStep(b, tick); continue; }
                if (tick < b.HuntUntil) continue; // stalking a deer: the renderer plays it
                if ((tick + b.Index) % 3 != 0) { Stalk(b, tick); continue; } // looks around every third day
                Look(b, tick);
            }
        }

        // Now and then it goes after the herds around it: a short dash at a spot a few cells off. The kill itself is
        // the monthly cull (MonthlyStep); this is what the player sees of it (UnitRenderer plays the deer).
        void Stalk(Beast b, long tick)
        {
            var rng = RngFor(tick, 70000 + b.Index);
            if (rng.NextFloat() >= 1f / 12f) return;
            float bx = _e.X[b.Entity], by = _e.Y[b.Entity];
            int region = _sim.Wildlife.RegionOf(bx, by);
            if (_sim.Wildlife.At(Species.Deer, region) + _sim.Wildlife.At(Species.Rabbit, region) < 3f) return;
            float a = rng.Range(0f, Mathf.PI * 2f), r = rng.Range(3f, 6f);
            float tx = bx + Mathf.Cos(a) * r, ty = by + Mathf.Sin(a) * r;
            if (!_w.IsWalkable(tx, ty)) return;
            b.HuntX = tx;
            b.HuntY = ty;
            b.HuntUntil = tick + 5;
            _e.TX[b.Entity] = tx;
            _e.TY[b.Entity] = ty;
        }

        bool Quarry(int id, long tick)
        {
            var s = _e.Species[id];
            if (s != Species.Cultivator) return true; // migrants, caravans
            var c = _sim.Cultivation.ForEntity(id);
            return c != null && c.Alive && c.Travelling && !c.AtWar && c.HuntTarget < 0 && c.Trip != Trip.Flee &&
                   c.Realm < Realm.HoaThan && tick - c.LastDuelTick >= SimClock.DaysPerMonth * 6 && !InChase(id);
        }

        bool InChase(int entity) => _chased != null && _chased.Contains(entity);

        void Look(Beast b, long tick)
        {
            // Not every passer-by is noticed: one who stays a month in its sight is seen two times in five (it looks ten
            // times a month). Rolled first, so the search is only made when it would count.
            var rng = RngFor(tick, 50000 + b.Index);
            if (rng.NextFloat() >= NoticeChance) return;
            float bx = _e.X[b.Entity], by = _e.Y[b.Entity];
            int id = _sim.Creatures.FindNearest(bx, by, Sight(b), PreyMask, i => Quarry(i, tick));
            if (id < 0) return;
            var s = _e.Species[id];
            if (s != Species.Cultivator)
            {
                if (b.Grade < 2) return; // a young one leaves crowds alone
                Begin(b, id, ChaseHunts, tick);
                return;
            }
            var c = _sim.Cultivation.ForEntity(id);
            float ratio = CombatSystem.Strength(c) / Mathf.Max(0.01f, Strength(b));
            Begin(b, id, ratio >= 1.5f ? ChaseHunted : ratio >= 0.7f ? ChaseClash : ChaseHunts, tick);
            float cx = _e.X[c.Entity], cy = _e.Y[c.Entity];
            if (b.ChaseMode == ChaseHunts) Away(c, cx, cy, bx, by);
            else _sim.Cultivation.Engage(c, bx, by, tick + ChaseDays + 5);
        }

        // A cultivator who came looking for it (an errand to hunt, Errands): they close in, or it runs if they far outmatch it.
        public void Challenge(Beast b, Cultivator c, long tick)
        {
            if (!b.Alive || b.ChaseEntity >= 0 || c == null || !c.Alive) return;
            float ratio = CombatSystem.Strength(c) / Mathf.Max(0.01f, Strength(b));
            Begin(b, c.Entity, ratio >= 1.5f ? ChaseHunted : ChaseClash, tick);
            _chased?.Add(c.Entity);
            _sim.Cultivation.Engage(c, _e.X[b.Entity], _e.Y[b.Entity], tick + ChaseDays + 5);
        }

        void Begin(Beast b, int entity, byte mode, long tick)
        {
            b.ChaseEntity = entity;
            b.ChaseMode = mode;
            b.ChaseSince = tick;
            b.HuntUntil = 0;
        }

        // Run from (fx, fy): a point well beyond the beast's sight, straight away from it.
        void Away(Cultivator c, float x, float y, float fx, float fy)
        {
            float dx = x - fx, dy = y - fy, d = Mathf.Max(0.5f, Mathf.Sqrt(dx * dx + dy * dy));
            float tx = Mathf.Clamp(x + dx / d * 30f, 1f, _w.W - 2f), ty = Mathf.Clamp(y + dy / d * 30f, 1f, _w.H - 2f);
            if (c.Trip == Trip.Flee) _sim.Cultivation.Retarget(c, tx, ty);
            else _sim.Cultivation.Flee(c, tx, ty);
        }

        void ChaseStep(Beast b, long tick)
        {
            int id = b.ChaseEntity;
            float bx = _e.X[b.Entity], by = _e.Y[b.Entity];
            if (!_e.IsAlive(id)) { GiveUp(b, null, tick); return; }
            var c = _e.Species[id] == Species.Cultivator ? _sim.Cultivation.ForEntity(id) : null;
            if (_e.Species[id] == Species.Cultivator && (c == null || !c.Alive || !_sim.Cultivation.IsShownOnMap(c))) { GiveUp(b, c, tick); return; }
            float tx = _e.X[id], ty = _e.Y[id];
            float dx = tx - bx, dy = ty - by, d2 = dx * dx + dy * dy;
            float sight = Sight(b);
            if (d2 <= 1.6f * 1.6f) { Catch(b, id, c, tick); return; }
            if (tick - b.ChaseSince > (b.ChaseMode == ChaseHunted ? HuntedDays : ChaseDays) || d2 > sight * sight * 9f) { GiveUp(b, c, tick); return; }
            if (b.ChaseMode == ChaseHunted)
            {
                // It runs; the cultivator follows.
                float d = Mathf.Sqrt(d2);
                float rx = Mathf.Clamp(bx - dx / d * 12f, 1f, _w.W - 2f), ry = Mathf.Clamp(by - dy / d * 12f, 1f, _w.H - 2f);
                if (Flies(b.Kind) || _w.IsWalkable(rx, ry)) { _e.TX[b.Entity] = rx; _e.TY[b.Entity] = ry; }
                _sim.Cultivation.Retarget(c, bx, by);
                return;
            }
            // It gives chase (or closes in); a quarry that runs keeps running away from it.
            _e.TX[b.Entity] = tx;
            _e.TY[b.Entity] = ty;
            if (c == null) { RunFrom(id, bx, by); return; }
            if (b.ChaseMode == ChaseHunts) Away(c, tx, ty, bx, by);
            else _sim.Cultivation.Retarget(c, bx, by);
        }

        // Migrants and caravans make for wherever is away from it.
        void RunFrom(int id, float fx, float fy)
        {
            float x = _e.X[id], y = _e.Y[id], dx = x - fx, dy = y - fy, d = Mathf.Max(0.5f, Mathf.Sqrt(dx * dx + dy * dy));
            float tx = x + dx / d * 6f, ty = y + dy / d * 6f;
            if (_w.IsWalkable(tx, ty)) { _e.TX[id] = tx; _e.TY[id] = ty; }
        }

        void Catch(Beast b, int id, Cultivator c, long tick)
        {
            b.ChaseEntity = -1;
            _e.TX[b.Entity] = b.HomeX;
            _e.TY[b.Entity] = b.HomeY;
            var rng = RngFor(tick, 60000 + b.Index);
            if (c != null)
            {
                string context = b.ChaseMode == ChaseHunted ? "đuổi kịp và săn yêu đan" : b.ChaseMode == ChaseClash ? "chạm trán giữa đường, giao chiến" : "bị đuổi kịp giữa đường";
                Fight(c, b, tick, context, ref rng);
                if (c.Alive && c.Trip != Trip.Return) _sim.Cultivation.ReturnHome(c);
                return;
            }
            float x = _e.X[id], y = _e.Y[id];
            if (_e.Species[id] == Species.Caravan)
            {
                if (_sim.Trade.Maul(id))
                {
                    b.Kills += 4;
                    _sim.Events.Add(tick, EventKind.Beast, 1, $"{b.Title} ({b.GradeText}) đuổi kịp một thương đội, người ngựa tan tác, hàng hóa vương vãi.", x, y, Fx.Stampede);
                }
                return;
            }
            int dead = _sim.Settlements.MaulMigrants(id, rng.Range(0.2f, 0.5f), tick);
            b.Kills += dead;
            if (dead > 0) _sim.Events.Add(tick, EventKind.Beast, 1, $"{b.Title} ({b.GradeText}) đuổi kịp một đoàn di dân, {dead} người bỏ mạng.", x, y, Fx.Stampede);
        }

        void GiveUp(Beast b, Cultivator c, long tick)
        {
            byte mode = b.ChaseMode;
            b.ChaseEntity = -1;
            _e.TX[b.Entity] = b.HomeX;
            _e.TY[b.Entity] = b.HomeY;
            if (c == null || !c.Alive) return;
            if (c.Trip == Trip.Flee || c.Trip == Trip.Hunt) _sim.Cultivation.ReturnHome(c);
            if (mode == ChaseHunts && c.Realm >= Realm.TrucCo)
                _sim.Events.Add(tick, EventKind.Beast, 0, $"{c.Title} ngự kiếm thoát khỏi {b.Title} ({b.GradeText}).", _e.X[c.Entity], _e.Y[c.Entity], Fx.None, c.Index);
        }
    }
}
