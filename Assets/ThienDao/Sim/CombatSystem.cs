using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    // Đấu pháp: one cultivator against another. Used by sect battles, by chance meetings on the road
    // (enemies, ma tu robbing the weak), and by vendettas: kill someone's master or disciple and they will come for you.
    public sealed class CombatSystem
    {
        const float MeetRadius = 3f;
        const int DuelCooldownDays = SimClock.DaysPerYear;

        readonly Simulation _sim;
        readonly List<Cultivator> _out = new List<Cultivator>();

        public CombatSystem(Simulation sim) => _sim = sim;

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_sim.World.Seed ^ 0xD0E1u, (int)tick, salt));

        // Realm dominates; stage, tâm cảnh, khí vận and the demonic path tilt it.
        public static float Strength(Cultivator c) =>
            Realms.Power[(int)c.Realm] * (1f + c.Stage * 0.15f) * (0.7f + 0.3f * c.DaoHeart) * (0.9f + 0.2f * c.Luck) * (c.Demonic ? 1.15f : 1f) *
            (1f + 0.12f * Mathf.Min(3, c.Treasures)); // pháp bảo

        // Returns the winner. The loser dies with probability deathBase × 2^(realm gap) (to the death: much higher),
        // otherwise flees badly hurt. The winner takes the loser's storage bag.
        public Cultivator Duel(Cultivator a, Cultivator b, long tick, float deathBase, string context, ref DetRandom rng)
        {
            float sa = Strength(a) * rng.Range(0.6f, 1.4f), sb = Strength(b) * rng.Range(0.6f, 1.4f);
            var winner = sa >= sb ? a : b;
            var loser = winner == a ? b : a;
            winner.LastDuelTick = loser.LastDuelTick = tick;
            float p = Mathf.Clamp(deathBase * Mathf.Pow(2f, (int)winner.Realm - (int)loser.Realm), 0.03f, 0.95f);
            var e = _sim.Entities;
            float x = e.X[loser.Entity], y = e.Y[loser.Entity];
            int imp = Mathf.Max(ImportanceOf(winner), ImportanceOf(loser));
            if (rng.NextFloat() < p)
            {
                Kill(winner, loser, tick, $"{winner.Title} ({Sect(winner)}) {context}, chém giết {loser.Title} ({Sect(loser)}).", imp);
            }
            else
            {
                loser.Progress *= 0.7f;
                loser.DaoHeart = Mathf.Max(0f, loser.DaoHeart - 0.05f);
                _sim.Cultivation.ReturnHome(loser);
                _sim.Events.Add(tick, EventKind.Duel, Mathf.Max(0, imp - 1),
                    $"{winner.Title} ({Sect(winner)}) {context}, đánh {loser.Title} trọng thương bỏ chạy.", x, y, Fx.Lightning, winner.Index, loser.Index, winner.SectId, loser.SectId);
            }
            return winner;
        }

        static int ImportanceOf(Cultivator c) => c.Realm >= Realm.NguyenAnh ? 3 : c.Realm >= Realm.KetDan ? 2 : 1;

        string Sect(Cultivator c) => _sim.Cultivation.SectName(c);

        // Death at another's hand: loot, the tally, and a blood debt for the victim's master and disciples.
        public void Kill(Cultivator killer, Cultivator victim, long tick, string text, int importance)
        {
            if (!victim.Alive) return;
            killer.Kills++;
            float need = Realms.Need(killer.Realm, killer.Stage);
            killer.Progress += need * 0.1f * Mathf.Max(1, 1 + (int)victim.Realm - (int)killer.Realm); // the storage bag
            killer.Stones += victim.Stones;
            victim.Stones = 0f;
            if (victim.Treasures > 0)
            {
                killer.Treasures++;
                victim.Treasures--;
                if (killer.TreasureName == null || killer.Treasures == 1) killer.TreasureName = victim.TreasureName;
            }
            _sim.Cultivation.Slay(victim, killer, tick, text, importance);

            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c == killer) continue;
                bool kin = c.MasterIdx == victim.Index || victim.MasterIdx == c.Index;
                if (!kin || c.Nemesis >= 0) continue;
                c.Nemesis = killer.Index;
                c.NemesisFor = victim.Index;
                c.NemesisTick = tick;
            }
            _sim.Stories?.OnKill(killer, victim, tick);
            if (killer.Nemesis == victim.Index) ClearVendetta(killer);
        }

        void ClearVendetta(Cultivator c)
        {
            c.Nemesis = -1;
            c.NemesisFor = -1;
            if (c.HuntTarget >= 0)
            {
                c.HuntTarget = -1;
                _sim.Cultivation.ReturnHome(c);
            }
        }

        // ---------------------------------------------------------------- monthly: meetings and pursuits

        public void MonthlyStep(long tick)
        {
            var all = _sim.Cultivation.All;
            var e = _sim.Entities;

            // Pursuers close in; within reach the matter is settled to the death.
            for (int k = 0; k < all.Count; k++)
            {
                var c = all[k];
                if (!c.Alive || c.HuntTarget < 0) continue;
                var t = all[c.HuntTarget];
                if (!t.Alive)
                {
                    ClearVendetta(c);
                    continue;
                }
                float dx = e.X[t.Entity] - e.X[c.Entity], dy = e.Y[t.Entity] - e.Y[c.Entity];
                if (dx * dx + dy * dy <= MeetRadius * MeetRadius * 4f)
                {
                    var rng = RngFor(tick, c.Index);
                    var w = Duel(c, t, tick, 0.75f, c.NemesisFor >= 0 ? $"báo thù cho {all[c.NemesisFor].Name}" : "tìm đến tận nơi", ref rng);
                    if (c.Alive && w != c) ClearVendetta(c); // beaten: the grudge remains, the chase ends for now
                    else if (c.Alive) c.HuntTarget = -1;
                    if (c.Alive) _sim.Cultivation.ReturnHome(c);
                }
                else if (c.Travelling && c.Trip == Trip.Hunt) _sim.Cultivation.Retarget(c, e.X[t.Entity], e.Y[t.Entity]);
                else if (c.Away && tick < c.StayUntil) _sim.Cultivation.SendToHunt(c, e.X[t.Entity], e.Y[t.Entity], c.StayUntil); // the trail moved on
                else c.HuntTarget = -1; // gave up for now, or was called home
            }

            // Chance meetings among those out on the map.
            _out.Clear();
            foreach (var c in all)
                if (c.Alive && _sim.Cultivation.IsShownOnMap(c) && !c.AtWar && c.HuntTarget < 0) _out.Add(c);
            for (int i = 0; i < _out.Count; i++)
            {
                var a = _out[i];
                if (!a.Alive || tick - a.LastDuelTick < DuelCooldownDays) continue;
                for (int j = i + 1; j < _out.Count; j++)
                {
                    var b = _out[j];
                    if (!b.Alive || tick - b.LastDuelTick < DuelCooldownDays) continue;
                    float dx = e.X[a.Entity] - e.X[b.Entity], dy = e.Y[a.Entity] - e.Y[b.Entity];
                    if (dx * dx + dy * dy > MeetRadius * MeetRadius) continue;
                    var rng = RngFor(tick, 1000000 + a.Index * 31 + b.Index);
                    Meet(a, b, tick, ref rng);
                    break;
                }
            }
        }

        void Meet(Cultivator a, Cultivator b, long tick, ref DetRandom rng)
        {
            if (a.Nemesis == b.Index || b.Nemesis == a.Index)
            {
                var avenger = a.Nemesis == b.Index ? a : b;
                var other = avenger == a ? b : a;
                Duel(avenger, other, tick, 0.75f, "oan gia ngõ hẹp", ref rng);
                return;
            }
            bool sameSect = a.SectId >= 0 && a.SectId == b.SectId;
            if (sameSect) return;
            var stance = a.SectId >= 0 && b.SectId >= 0 ? _sim.Factions.StanceBetween(a.SectId, b.SectId) : Stance.Neutral;
            if (stance == Stance.Allied) return;
            float chance = 0f;
            string context = "giao thủ";
            if (stance == Stance.War) { chance = 0.7f; context = "gặp kẻ địch giữa đường"; }
            else if (stance == Stance.Hostile) { chance = 0.3f; context = "nảy sinh xung đột"; }
            // Giết người đoạt bảo: a ma tu falls on someone weaker.
            var strong = Strength(a) >= Strength(b) ? a : b;
            var weak = strong == a ? b : a;
            if (strong.Demonic && !weak.Demonic && strong.Realm >= weak.Realm) { chance = Mathf.Max(chance, 0.35f); context = "giết người đoạt bảo"; }
            else if (a.Demonic != b.Demonic) { chance = Mathf.Max(chance, 0.25f); context = "chính tà bất lưỡng lập"; }
            if (rng.NextFloat() >= chance) return;
            var first = context == "giết người đoạt bảo" ? strong : a;
            Duel(first, first == a ? b : a, tick, context == "giết người đoạt bảo" ? 0.5f : 0.3f, context, ref rng);
        }

        // ---------------------------------------------------------------- yearly: setting out for revenge

        public void YearlyStep(long tick)
        {
            var all = _sim.Cultivation.All;
            for (int k = 0; k < all.Count; k++)
            {
                var c = all[k];
                if (!c.Alive || c.Nemesis < 0 || c.HuntTarget >= 0 || !_sim.Cultivation.IsAtHome(c)) continue;
                var t = all[c.Nemesis];
                if (!t.Alive)
                {
                    c.Nemesis = -1; // heaven took the debt
                    c.NemesisFor = -1;
                    continue;
                }
                // They wait until they believe they can win; a short life left makes them reckless.
                bool ready = Strength(c) >= Strength(t) * 0.8f || c.AgeYears(tick) > c.LifespanYears * 0.9f;
                if (!ready || c.Realm < Realm.TrucCo) continue;
                var rng = RngFor(tick, 2000000 + c.Index);
                if (rng.NextFloat() >= 0.5f) continue;
                StartHunt(c, tick);
            }
        }

        // Sets out after their nemesis (who must be alive).
        public void StartHunt(Cultivator c, long tick)
        {
            var all = _sim.Cultivation.All;
            if (c.Nemesis < 0 || !all[c.Nemesis].Alive) return;
            var t = all[c.Nemesis];
            c.HuntTarget = t.Index;
            var e = _sim.Entities;
            _sim.Cultivation.SendToHunt(c, e.X[t.Entity], e.Y[t.Entity], tick + 2L * SimClock.DaysPerYear);
            string whom = c.NemesisFor >= 0 ? all[c.NemesisFor].Name : "người thân";
            _sim.Events.Add(tick, EventKind.Vendetta, 2,
                $"{c.Title} lên đường truy sát {t.Title}, báo thù cho {whom}.", e.X[c.Entity], e.Y[c.Entity], Fx.None, c.Index, t.Index, c.SectId, t.SectId);
        }

        public void HashInto(ref ulong h)
        {
            foreach (var c in _sim.Cultivation.All)
                StateHash.Add(ref h, c.Nemesis | ((long)c.HuntTarget << 24) | ((long)c.Kills << 48));
        }
    }
}
