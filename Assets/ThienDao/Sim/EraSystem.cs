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

        // Mốc của nhân tộc (devlog 35), in a world that opened at khai thiên lập địa: when the first of each came.
        public long CultivatorsSince = -1, SectsSince = -1;
        public int GreatCalamitiesPassed; // thượng cổ ends with the first đại kiếp, trung cổ with the second

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
            if (_lastGreat && !great && (!_sim.World.Primordial || SectsSince >= 0)) GreatCalamitiesPassed++; // the đại kiếp of the beasts' age ends no age of men
            bool milestone = Milestones(tick);
            int year = (int)(tick / SimClock.DaysPerYear) + 1;
            if (All.Count == 0 || milestone || great != _lastGreat || year % EvaluateEveryYears == 1) Evaluate(tick, great);
            _lastGreat = great;
        }

        // The great firsts of nhân tộc, told once: the first to cultivate (who, untaught, finds the way to draw qi in
        // and leaves it as Dẫn Khí Quyết, the first method), the first sect. True when one came this year.
        bool Milestones(long tick)
        {
            if (!_sim.World.Primordial) return false;
            bool any = false;
            if (CultivatorsSince < 0 && _sim.Cultivation.All.Count > 0)
            {
                CultivatorsSince = tick;
                any = true;
                var c = _sim.Cultivation.All[0];
                _sim.Events.Add(tick, EventKind.Era, 3,
                    $"Thủy tổ tu tiên! {c.Name} là người đầu tiên của nhân tộc cảm ứng được linh khí thiên địa, tự ngộ ra Dẫn Khí Quyết, bộ công pháp đầu tiên của nhân gian.",
                    c.HomeX, c.HomeY, Fx.LightPillar, c.Index);
            }
            if (SectsSince < 0 && _sim.Factions.All.Count > 0)
            {
                SectsSince = tick;
                any = true;
                var f = _sim.Factions.All[0];
                var seat = _sim.Settlements.All[f.Id];
                _sim.Events.Add(tick, EventKind.Era, 3,
                    $"{_sim.Factions.NameOf(f.Id)} khai tông lập phái, tông môn đầu tiên của nhân tộc. Thượng cổ tu tiên bắt đầu.",
                    seat.X + 0.5f, seat.Y + 0.5f, Fx.LightPillar, -1, -1, f.Id);
            }
            return any;
        }

        // The great age of the world's history: hồng hoang, the dawn of men, the founders, then thượng cổ, trung cổ,
        // cận cổ, each ended by a đại kiếp.
        public string EpochName
        {
            get
            {
                if (_sim.World.Primordial && SectsSince < 0)
                    return _sim.Settlements.FirstTribeTick < 0 ? "Hồng hoang" : CultivatorsSince < 0 ? "Nhân tộc mông muội" : "Thủy tổ tu tiên";
                return GreatCalamitiesPassed == 0 ? "Thượng cổ" : GreatCalamitiesPassed == 1 ? "Trung cổ" : GreatCalamitiesPassed == 2 ? "Cận cổ" : "Kim cổ";
            }
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

            if (_sim.World.Primordial && SectsSince < 0)
            {
                if (_sim.Settlements.FirstTribeTick < 0) { name = "Hồng Hoang"; reason = "thiên địa sơ khai, vạn thú tranh bá, chân linh làm chủ, chưa có dấu chân người"; }
                else if (CultivatorsSince < 0) { name = "Nhân Tộc Mông Muội"; reason = "nhân tộc yếu ớt sống trong kẽ hở giữa lãnh địa vạn thú, chưa ai biết đến tu luyện"; }
                else { name = "Thủy Tổ Tu Tiên"; reason = "nhân tộc bắt đầu dẫn khí nhập thể, nhưng chưa có tông môn nào"; }
            }
            else if (great) { name = "Đại Kiếp"; reason = "thiên địa đại kiếp giáng lâm, linh khí khô kiệt, tai ương liên miên"; }
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
            StateHash.Add(ref h, All.Count | ((long)_lastPop << 16) | ((long)GreatCalamitiesPassed << 48));
            StateHash.Add(ref h, CultivatorsSince ^ (SectsSince << 24));
            foreach (var e in All) StateHash.Add(ref h, e.Start ^ ((long)Hash.FromString(e.Name) << 20));
        }
    }
}
