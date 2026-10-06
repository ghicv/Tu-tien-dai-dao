using System.Collections.Generic;
using ThienDao.Core;
using UnityEngine;

namespace ThienDao.Sim
{
    public sealed class Era
    {
        public long Start;
        public string Name;
        public string Reason;
        public int Year => (int)(Start / SimClock.DaysPerYear) + 1;
    }

    // Thời đại (GDD §13): ages are not scripted, they are recognised from the state of the world every half century
    // (and at once when a đại kiếp begins or ends). Under them runs the long breath of heaven and earth: the world's
    // qi waxes and wanes over thousands of years on a cycle of its own for each seed — mạt pháp at the bottom,
    // linh khí phục tô at the top — so no two worlds share their history.
    public sealed class EraSystem
    {
        const float Amplitude = 0.25f;
        const int EvaluateEveryYears = 50;

        readonly Simulation _sim;
        readonly float _periodYears, _phase;
        public readonly List<Era> All = new List<Era>();
        int _lastBattles, _lastDestructions, _lastPop;
        bool _lastGreat;

        public EraSystem(Simulation sim)
        {
            _sim = sim;
            var rng = new DetRandom(sim.World.Seed ^ 0xE2Au);
            _periodYears = rng.Range(1500f, 3500f);
            // Start somewhere ordinary: the world opens neither in mạt pháp nor at the golden peak.
            float start = rng.Range(-0.2f, 0.6f);
            _phase = Mathf.Asin(start);
            if (rng.NextFloat() < 0.5f) _phase = Mathf.PI - _phase; // and either waxing or waning
        }

        public Era Current => All.Count > 0 ? All[All.Count - 1] : null;

        // Share of its natural level the world's qi stands at in this year of the great cycle.
        public float QiFactor(long tick)
        {
            float years = tick / (float)SimClock.DaysPerYear;
            return 1f + Amplitude * Mathf.Sin(2f * Mathf.PI * years / _periodYears + _phase);
        }

        public bool Waxing(long tick)
        {
            float years = tick / (float)SimClock.DaysPerYear;
            return Mathf.Cos(2f * Mathf.PI * years / _periodYears + _phase) > 0f;
        }

        public void YearlyStep(long tick)
        {
            bool great = _sim.Disasters.GreatCalamityActive;
            int year = (int)(tick / SimClock.DaysPerYear) + 1;
            if (All.Count == 0 || great != _lastGreat || year % EvaluateEveryYears == 1) Evaluate(tick, great);
            _lastGreat = great;
        }

        void Evaluate(long tick, bool great)
        {
            var n = _sim.Events.CountByKind;
            int battles = n[(int)EventKind.Battle] - _lastBattles, destructions = n[(int)EventKind.Destruction] - _lastDestructions;
            int pop = _sim.Settlements.TotalPopulation;
            var cr = _sim.Cultivation.CountByRealm;
            float qi = QiFactor(tick);
            var prev = Current;
            string name, reason;

            Faction top = null;
            float total = 0f;
            foreach (var f in _sim.Factions.All)
            {
                if (!f.Alive) continue;
                total += f.Power;
                if (top == null || f.Power > top.Power) top = f;
            }

            if (great) { name = "Đại Kiếp"; reason = "thiên địa đại kiếp giáng lâm, linh khí khô kiệt, tai ương liên miên"; }
            else if (prev != null && prev.Name == "Đại Kiếp") { name = "Tân Thời Đại"; reason = "đại kiếp qua đi, vạn vật hồi sinh"; }
            else if (qi < 0.82f) { name = "Mạt Pháp"; reason = $"linh khí thiên địa chỉ còn {qi * 100f:0}%, tu sĩ khó lòng tiến cảnh"; }
            // Skirmishes are counted as battles too: only a real age of war passes 200 in fifty years.
            else if (battles >= 200 || destructions >= 3) { name = "Đại Chiến Loạn Thế"; reason = $"{battles} trận giao tranh, {destructions} tông môn bị diệt trong {EvaluateEveryYears} năm"; }
            else if (cr[(int)Realm.HoaThan] > 0) { name = "Hóa Thần Hiện Thế"; reason = "có người đạt tới Hóa Thần, cảnh giới vạn năm khó gặp"; }
            else if (cr[(int)Realm.NguyenAnh] >= 3) { name = "Quần Hùng Tranh Bá"; reason = $"{cr[(int)Realm.NguyenAnh]} lão quái Nguyên Anh cùng tại thế"; }
            else if (qi > 1.18f) { name = "Linh Khí Phục Tô"; reason = $"linh khí thiên địa dâng tới {qi * 100f:0}%, thời hoàng kim của người tu tiên"; }
            else if (top != null && total > 0f && top.Power / total > 0.35f && _sim.Factions.AliveCount >= 4)
            {
                name = $"{_sim.Factions.NameOf(top.Id)} Xưng Bá";
                reason = $"{_sim.Factions.NameOf(top.Id)} nắm {top.Power / total * 100f:0}% thực lực thiên hạ";
            }
            else if (_lastPop > 0 && pop < _lastPop * 0.8f) { name = "Suy Tàn"; reason = $"nhân gian từ {_lastPop:N0} người còn {pop:N0}"; }
            else if (_sim.Cultivation.AliveCount < 60) { name = "Tiên Đạo Điêu Linh"; reason = $"cả thiên hạ chỉ còn {_sim.Cultivation.AliveCount} tu sĩ"; }
            else if (_sim.Factions.AliveCount >= 12) { name = "Bách Tông Tề Phóng"; reason = $"{_sim.Factions.AliveCount} tông môn cùng tồn tại"; }
            else { name = "Thái Bình"; reason = "thiên hạ yên ổn, tu sĩ an tâm tu luyện"; }

            _lastBattles = n[(int)EventKind.Battle];
            _lastDestructions = n[(int)EventKind.Destruction];
            _lastPop = pop;
            if (prev != null && prev.Name == name) return;
            All.Add(new Era { Start = tick, Name = name, Reason = reason });
            _sim.Events.Add(tick, EventKind.Era, 3, prev == null ? $"Khai thiên lập địa, thời đại {name}: {reason}." : $"Thiên hạ bước vào thời đại {name}: {reason}.");
        }

        public void HashInto(ref ulong h)
        {
            StateHash.Add(ref h, All.Count | ((long)_lastPop << 16));
            foreach (var e in All) StateHash.Add(ref h, e.Start ^ ((long)Hash.FromString(e.Name) << 20));
        }
    }
}
