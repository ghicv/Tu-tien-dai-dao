using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    public enum Trip : byte { None, Relocate, Excursion, Return, Battle, Hunt }

    // What a nhân vật chính is doing with their life right now.
    public enum Goal : byte { None, Seclusion, SeekCave, Training, SeekFortune, Market, JoinSect, Flee }
    public enum Outing : byte { Sightseeing, Training, HerbHunting }

    public sealed class Cultivator
    {
        public static readonly string[] OutingNames = { "đang du ngoạn", "đang lịch luyện", "đang tìm linh thảo" };

        public int Index;
        public string Name;
        public long BirthTick;
        public int Roots;           // SpiritRoots bitmask
        public float Comprehension; // ngộ tính 0..1
        public float Luck;          // khí vận 0..1
        public float DaoHeart;      // tâm cảnh 0..1
        public float Ambition;      // dã tâm 0..1: founding sects, breaking away
        public Realm Realm;
        public int Stage;
        public float Progress;
        public int Entity;
        public int SectId = -1;     // settlement id of the sect, -1 = tán tu
        public bool Alive = true;
        public bool Demonic;        // ma tu
        public bool Travelling;
        public bool Away;           // at an excursion destination
        public Trip Trip;
        public Outing Outing;
        public long StayUntil;
        public float HomeX, HomeY;  // sect hall or own cave
        public int FailedAttempts;
        public long LastAttemptTick = -100000;
        public int BonusYears;      // lifespan gained from blessings

        public float AgeYears(long tick) => (tick - BirthTick) / (float)SimClock.DaysPerYear;

        public bool AtWar;          // flying to or standing on a battlefield

        // Lịch sử cá nhân (M5): who taught them, who they owe blood, what they have done.
        public int MasterIdx = -1;   // sư phụ (index into Cultivation.All)
        public int Nemesis = -1;     // the one who killed their master or disciple
        public int NemesisFor = -1;  // whose death that was
        public long NemesisTick;
        public int HuntTarget = -1;  // on the road to settle it
        public int Kills;
        public int KilledBy = -1;
        public long DeathTick = -1;
        public long LastDuelTick = -100000;
        public float Fame;           // sum of the importance of what they took part in
        public bool Legend;
        public string Epithet;       // danh hiệu, given when they become a legend
        public bool Blessed;         // Thiên Đạo has touched them
        public int OriginRoots;      // the root they were born with

        // Nhân vật chính: the player watches them, and they live by goals (ProtagonistAI) instead of idling at home.
        public bool Watched;
        public float Stones;         // own linh thạch
        public int Pills;            // breakthrough pills for the next gate
        public int Treasures;        // pháp bảo
        public float Hp = -1f;       // sinh lực; -1 = whole (CombatSystem.HpOf)
        public int Technique;        // công pháp (index into Techniques.All); 0 = the common Dẫn Khí Quyết (TechniqueSystem)
        public string TreasureName;
        public Goal Goal;
        public string GoalText;
        public long GoalUntil;
        public bool GoalDone;
        public long LastDiscipleTick = -100000;
        public int GoalMark;          // realm and stage when the goal began (to tell progress from a stuck bottleneck)

        public string Activity =>
            AtWar ? "đang xuất chiến" :
            Watched && GoalText != null && Goal != Goal.None ? GoalText :
            Trip == Trip.Hunt || HuntTarget >= 0 ? "đang truy sát kẻ thù" :
            Trip == Trip.Relocate ? "đang đi tìm động phủ mới" :
            Trip == Trip.Return ? "đang trở về" :
            Trip == Trip.Excursion || Away ? OutingNames[(int)Outing] : "đang bế quan";
        public int LifespanYears => Realms.LifespanYears[(int)Realm] + BonusYears;
        public string RealmText => Realms.Describe(Realm, Stage);

        public string Title
        {
            get
            {
                if (Epithet != null) return $"{Epithet} {Name}";
                string t = Realms.Titles[(int)Realm];
                return t.Length > 0 ? $"{Name} {t}" : Name;
            }
        }

        // Ordering key for the power ranking.
        public float Rank => (int)Realm * 100f + Stage * 10f + Mathf.Min(9.9f, Progress / Mathf.Max(1f, Realms.Need(Realm, Stage)) * 10f);
    }

    // Tu sĩ: awakened from village children, cultivate by drawing qi from where they sit, break through,
    // deviate, face tribulation, die of old age, and move toward richer qi.
    public sealed class CultivationSystem
    {
        const float AwakenChance = 0.01f;     // share of 10-year-olds with a spirit root, before local qi bonus
        const int SectRecruitRange = 260;
        const float DesperateAge = 0.85f;     // share of lifespan after which they gamble on breakthroughs

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly EntityStore _e;

        public readonly List<Cultivator> All = new List<Cultivator>();
        public readonly int[] CountByRealm = new int[(int)Realm.Count];
        public int AliveCount { get; private set; }

        public CultivationSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _e = sim.Entities;
            SeedInitial();
        }

        public Cultivator ForEntity(int entity)
        {
            if (!_e.IsAlive(entity) || _e.Species[entity] != Species.Cultivator) return null;
            int i = _e.Payload[entity];
            return i >= 0 && i < All.Count ? All[i] : null;
        }

        public string SectName(Cultivator c) => c.SectId >= 0 ? _sim.Settlements.All[c.SectId].BaseName : "tán tu";

        readonly Dictionary<int, Cultivator> _masters = new Dictionary<int, Cultivator>(); // sect id → tông chủ

        readonly Dictionary<int, long> _successionTick = new Dictionary<int, long>();

        public Cultivator MasterOf(int sectId) => _masters.TryGetValue(sectId, out var m) && m.Alive ? m : null;

        // When the sect last changed hands (long.MinValue if never); a fresh, contested succession breeds schism.
        public long LastSuccession(int sectId) => _successionTick.TryGetValue(sectId, out long t) ? t : long.MinValue;

        // Tông chủ is the strongest living member; other Kết Đan+ are elders.
        public string Role(Cultivator c)
        {
            if (c.SectId < 0) return c.Realm >= Realm.NguyenAnh ? "Tán tu, bá chủ một phương" : "Tán tu";
            if (MasterOf(c.SectId) == c) return c.Realm >= Realm.NguyenAnh ? "Tông chủ, bá chủ một phương" : "Tông chủ";
            if (c.Realm >= Realm.KetDan) return "Trưởng lão";
            return c.Realm == Realm.TrucCo ? "Đệ tử nội môn" : "Đệ tử ngoại môn";
        }

        void UpdateMasters(long tick)
        {
            var best = new Dictionary<int, Cultivator>();
            foreach (var c in All)
            {
                if (!c.Alive || c.SectId < 0) continue;
                if (!best.TryGetValue(c.SectId, out var b) || c.Rank > b.Rank) best[c.SectId] = c;
            }
            foreach (var kv in best)
            {
                var old = MasterOf(kv.Key);
                if (old == kv.Value) continue;
                _masters[kv.Key] = kv.Value;
                if (old != null)
                {
                    _successionTick[kv.Key] = tick;
                    // The old master is gone: word gets around that the sect is weak while the new one finds their feet.
                    var seat = _sim.Settlements.All[kv.Key];
                    if (!old.Alive) _sim.Knowledge?.Spread(RumorKind.WeakSect, kv.Key, seat.X + 0.5f, seat.Y + 0.5f, 20f, tick, 10);
                }
                if (old != null || tick > 0)
                    _sim.Events.Add(tick, EventKind.Succession, kv.Value.Realm >= Realm.KetDan ? 2 : 1,
                        $"{kv.Value.Title} trở thành tông chủ {SectName(kv.Value)}.", -1f, -1f, Fx.None, kv.Value.Index, old?.Index ?? -1, kv.Key);
            }
        }

        // Cultivators in seclusion or at their sect are off the map (the sect shows a few stand-ins);
        // they appear only while out on the road or at an excursion spot.
        public bool IsShownOnMap(Cultivator c) => c.Alive && (c.Travelling || c.Away);

        public bool IsAtHome(Cultivator c) => c.Alive && !c.Travelling && !c.Away;

        // Nearest cultivator currently visible on the map within radius (for clicks, hover and divine acts).
        public Cultivator FindShownNear(float x, float y, float radius)
        {
            Cultivator best = null;
            float bestD = radius * radius;
            foreach (var c in All)
            {
                if (!IsShownOnMap(c)) continue;
                float dx = _e.X[c.Entity] - x, dy = _e.Y[c.Entity] - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        const float OutingChancePerMonth = 1f / 60f;

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0xC017u, (int)tick, salt));

        // ---------------------------------------------------------------- creation

        static int RollRoots(ref DetRandom rng)
        {
            float roll = rng.NextFloat();
            if (roll < 0.02f) return 1 << (5 + rng.Range(0, 3)); // Lôi / Phong / Băng
            int count = roll < 0.05f ? 1 : roll < 0.17f ? 2 : roll < 0.45f ? 3 : roll < 0.75f ? 4 : 5;
            int mask = 0;
            while (SpiritRoots.Count(mask) < count) mask |= 1 << rng.Range(0, 5);
            return mask;
        }

        Cultivator Create(ref DetRandom rng, Realm realm, int stage, float ageYears, int sectId, float x, float y, long tick, int roots = -1)
        {
            var c = new Cultivator
            {
                Index = All.Count,
                Name = _w.Lore.PersonName(ref rng),
                Roots = roots >= 0 ? roots : RollRoots(ref rng),
                Comprehension = rng.NextFloat(),
                Luck = rng.NextFloat(),
                DaoHeart = rng.Range(0.4f, 0.8f),
                Ambition = rng.NextFloat(),
                Realm = realm,
                Stage = stage,
                SectId = sectId
            };
            c.OriginRoots = c.Roots;
            c.Progress = rng.Range(0f, 0.8f) * Realms.Need(realm, stage);
            ageYears = Mathf.Min(ageYears, c.LifespanYears * 0.8f);
            c.BirthTick = tick - (long)(ageYears * SimClock.DaysPerYear);
            c.HomeX = x;
            c.HomeY = y;
            c.Entity = _e.Spawn(Species.Cultivator, x, y, c.BirthTick);
            _e.Payload[c.Entity] = c.Index;
            All.Add(c);
            AliveCount++;
            CountByRealm[(int)realm]++;
            return c;
        }

        void SeedInitial()
        {
            var rng = new DetRandom(_w.Seed ^ 0xC0171u);
            Settlement richest = null;
            float richestQi = -1f;
            // Sect hierarchy after the novel: Kết Đan masters and elders, Trúc Cơ inner disciples, Luyện Khí outer disciples.
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Sect) continue;
                float sx = s.X + 0.5f, sy = s.Y + 0.5f;
                Create(ref rng, Realm.KetDan, rng.Range(2, 4), rng.Range(250f, 420f), s.Id, sx, sy, 0);
                int elders = rng.Range(1, 4);
                for (int k = 0; k < elders; k++)
                    Create(ref rng, Realm.KetDan, rng.Range(0, 2), rng.Range(180f, 330f), s.Id, sx + rng.Range(-3f, 3f), sy + rng.Range(-3f, 3f), 0);
                int inner = rng.Range(3, 8);
                for (int k = 0; k < inner; k++)
                    Create(ref rng, Realm.TrucCo, rng.Range(0, 4), rng.Range(60f, 170f), s.Id, sx + rng.Range(-3f, 3f), sy + rng.Range(-3f, 3f), 0);
                int outer = rng.Range(10, 21);
                for (int k = 0; k < outer; k++)
                    Create(ref rng, Realm.LuyenKhi, rng.Range(0, 10), rng.Range(12f, 60f), s.Id, sx + rng.Range(-3f, 3f), sy + rng.Range(-3f, 3f), 0);

                float qi = _w.QiCap[_w.Idx(s.X, s.Y)];
                if (qi > richestQi) { richestQi = qi; richest = s; }
            }

            // A Nguyên Anh lão tổ is overlord of a whole region; at most one at the start, heading the richest sect.
            if (richest != null && rng.NextFloat() < 0.5f)
                Create(ref rng, Realm.NguyenAnh, rng.Range(0, 2), rng.Range(450f, 750f), richest.Id, richest.X + 0.5f, richest.Y + 1.5f, 0);

            // Tán tu scattered where qi is decent.
            for (int attempt = 0, made = 0; attempt < 4000 && made < 20; attempt++)
            {
                int x = rng.Range(8, _w.W - 8), y = rng.Range(8, _w.H - 8);
                int i = _w.Idx(x, y);
                if (!TerrainInfo.IsWalkable(_w.Terrain[i]) || _w.QiCap[i] < 1500) continue;
                bool trucCo = rng.NextFloat() < 0.2f;
                Create(ref rng, trucCo ? Realm.TrucCo : Realm.LuyenKhi, trucCo ? rng.Range(0, 2) : rng.Range(3, 12),
                    rng.Range(25f, 110f), -1, x + 0.5f, y + 0.5f, 0);
                made++;
            }
            UpdateMasters(0);
            // Every sect member below the top has a sư phụ among the elders.
            foreach (var c in All)
                if (c.SectId >= 0 && MasterOf(c.SectId) != c) AssignMaster(c, c.SectId, ref rng);
        }

        // ---------------------------------------------------------------- monthly

        public void MonthlyStep(long tick)
        {
            int count = All.Count;
            for (int k = 0; k < count; k++)
            {
                var c = All[k];
                if (!c.Alive) continue;
                var rng = RngFor(tick, c.Index);
                // Wounds mend: quickly at home in the cave or sect, slowly on the road.
                CombatSystem.Heal(c, IsAtHome(c) ? 0.25f : 0.06f);

                if (c.AgeYears(tick) >= c.LifespanYears)
                {
                    Die(c, tick, $"{c.Title} ({SectName(c)}) thọ nguyên đã tận, tọa hóa ở cảnh giới {c.RealmText}.", c.Realm >= Realm.KetDan ? 2 : c.Realm >= Realm.TrucCo ? 1 : 0);
                    continue;
                }

                if (!c.Travelling && !c.Away && !_w.IsWalkable(c.HomeX, c.HomeY))
                    MoveHomeAshore(c);

                if (c.Travelling)
                {
                    if (!_sim.Creatures.HasArrived(c.Entity)) continue; // no cultivating on the road
                    Arrive(c, tick, ref rng);
                }
                else if (c.Away && (tick >= c.StayUntil || !_w.IsWalkable(_e.X[c.Entity], _e.Y[c.Entity])))
                {
                    SendTo(c, c.HomeX, c.HomeY, Trip.Return);
                    continue;
                }
                else if (!c.Away && !c.Watched && CanRoam(c) && !CombatSystem.Wounded(c) && rng.NextFloat() < OutingChancePerMonth) // the wounded stay in to heal
                {
                    StartOuting(c, ref rng);
                    continue;
                }

                Cultivate(c);

                float need = Realms.Need(c.Realm, c.Stage);
                while (c.Progress >= need && !Realms.IsPeak(c.Realm, c.Stage))
                {
                    c.Progress -= need;
                    c.Stage++;
                    need = Realms.Need(c.Realm, c.Stage);
                }

                if (c.Realm < Realm.HoaThan && Realms.IsPeak(c.Realm, c.Stage) &&
                    tick - c.LastAttemptTick >= SimClock.DaysPerYear * Realms.AttemptCooldownYears[(int)c.Realm])
                {
                    bool ready = c.Progress >= need;
                    bool desperate = c.AgeYears(tick) > c.LifespanYears * DesperateAge && c.Progress >= need * 0.6f;
                    bool placeAllows = c.Realm != Realm.NguyenAnh ||
                                       _sim.Qi.SampleQi((int)_e.X[c.Entity], (int)_e.Y[c.Entity]) >= Realms.HoaThanMinQi;
                    // Bình cảnh công pháp: no method, no way up, however ready they are.
                    bool methodAllows = _sim.Techniques == null || _sim.Techniques.Allows(c, c.Realm + 1);
                    if ((ready || desperate) && placeAllows && methodAllows) TryBreakthrough(c, tick, !ready, ref rng);
                }
            }
        }

        void Cultivate(Cultivator c)
        {
            float x = _e.X[c.Entity], y = _e.Y[c.Entity];
            float qi = _sim.Qi.SampleQi((int)x, (int)y);
            float qiFactor = Mathf.Clamp(qi / Realms.RequiredQi[(int)c.Realm], 0f, 1.5f);
            float gain = SpiritRoots.SpeedMultiplier(c.Roots) * qiFactor * (0.6f + 0.8f * c.Comprehension) * (0.7f + 0.6f * c.DaoHeart);
            gain *= _sim.Techniques?.Speed(c) ?? 1f; // công pháp: its grade, and whether its element is among the roots
            if (c.Demonic)
            {
                gain *= 1.5f; // ma đạo: fast, at a price
                // Oán khí of an old battlefield feeds ma công. The scar byte is a cheap first test.
                int cell = _sim.World.Idx(Mathf.Clamp((int)x, 0, _sim.World.W - 1), Mathf.Clamp((int)y, 0, _sim.World.H - 1));
                if (ScarInfo.Kind(_sim.World.Scar[cell]) == ScarKind.Battlefield) gain *= 1f + _sim.Disasters.GrudgeAt(x, y);
            }
            if (c.Watched && c.Goal == Goal.Seclusion && IsAtHome(c)) gain *= 1.3f; // bế quan khổ tu
            c.Progress += gain;
            // Drawing qi depletes the spot; crowded caves run dry and push cultivators to look elsewhere.
            _sim.Qi.AddQi((int)x, (int)y, 4, -Realms.AbsorbPerMonth[(int)c.Realm] * Mathf.Min(1f, qiFactor + 0.2f));
        }

        void TryBreakthrough(Cultivator c, long tick, bool desperate, ref DetRandom rng)
        {
            c.LastAttemptTick = tick;
            var next = c.Realm + 1;
            string who = $"{c.Title} ({SectName(c)})";
            // Talent helps in proportion to how hard the gate is.
            float baseChance = Realms.BreakChance[(int)next] * _sim.Rules[Rule.Breakthrough];
            float chance = baseChance * (1f + c.Comprehension * 0.8f + c.DaoHeart * 0.4f + c.Luck * 0.4f);
            // The sect buys Trúc Cơ Đan with its linh thạch; a poor sect's disciples go without.
            // From the sect's own alchemists if they have one in store (TradeSystem), else bought with linh thạch.
            bool pill = next == Realm.TrucCo && c.SectId >= 0 && _sim.Factions != null &&
                        ((_sim.Trade != null && _sim.Trade.TakePill(c.SectId)) || _sim.Factions.TrySpend(c.SectId, FactionSystem.PillCost));
            if (pill) chance += 0.25f;
            // Their own pill for this gate, bought or found on the road.
            bool ownPill = c.Pills > 0;
            if (ownPill) { c.Pills--; chance += 0.2f; }
            if (desperate) chance *= 0.6f;

            float px = _e.X[c.Entity], py = _e.Y[c.Entity];
            if (rng.NextFloat() < chance)
            {
                // Breaking open the gate to Nguyên Anh and beyond calls down heaven's tribulation; survive it or perish.
                if (next >= Realm.NguyenAnh && !Tribulation(c, next, tick, false, ref rng)) return;
                SetRealm(c, next, 0);
                c.Progress = 0f;
                c.FailedAttempts = 0;
                int importance = next >= Realm.NguyenAnh ? 3 : next == Realm.KetDan ? 2 : 1;
                _sim.Events.Add(tick, EventKind.Breakthrough, importance,
                    $"{c.Name} ({SectName(c)}) đột phá {Realms.Names[(int)next]}{(pill ? " nhờ Trúc Cơ Đan" : ownPill ? $" nhờ {Lore.PillFor(next)}" : "")}.", px, py, Fx.LightPillar, c.Index, -1, c.SectId);
                _sim.Stories?.OnBreakthrough(c, tick);
                return;
            }

            c.Progress *= 0.6f;
            c.DaoHeart = Mathf.Max(0f, c.DaoHeart - 0.1f);
            c.FailedAttempts++;
            float deviation = 0.12f * (1.3f - c.DaoHeart) * (1f + c.FailedAttempts * 0.2f) * (_sim.Techniques != null && _sim.Techniques.Of(c).Demonic ? 1.3f : 1f); // ma công bites back
            if (rng.NextFloat() >= deviation)
            {
                _sim.Events.Add(tick, EventKind.BreakthroughFailed, c.Realm >= Realm.TrucCo ? 1 : 0, $"{who} đột phá {Realms.Names[(int)next]} thất bại.");
                return;
            }

            float outcome = rng.NextFloat();
            if (outcome < 0.4f)
            {
                Die(c, tick, $"{who} tẩu hỏa nhập ma, kinh mạch đứt đoạn mà chết.", c.Realm >= Realm.TrucCo ? 2 : 1, Fx.DemonBlast);
            }
            else if (outcome < 0.7f || c.Demonic || !_sim.Rules.DemonicAllowed)
            {
                if (c.Realm > Realm.LuyenKhi) SetRealm(c, c.Realm - 1, Realms.Stages[(int)c.Realm - 1] - 1);
                else c.Stage = Mathf.Max(0, c.Stage - 3);
                c.Progress = 0f;
                _sim.Events.Add(tick, EventKind.Deviation, 1, $"{who} tẩu hỏa nhập ma, tu vi tụt xuống {c.RealmText}.", px, py, Fx.DemonBlast, c.Index, -1, c.SectId);
            }
            else
            {
                c.Demonic = true;
                _sim.Events.Add(tick, EventKind.Deviation, 2, $"{who} tẩu hỏa nhập ma, sa vào ma đạo.", px, py, Fx.DemonBlast, c.Index, -1, c.SectId);
            }
        }

        // Thiên kiếp on c as they break into `next` (natural), or because Thiên Đạo willed it (divine). The bolts fall
        // on all around (DisasterSystem.TribulationStrikes) and leave lôi địa behind; returns whether c lives through it.
        bool Tribulation(Cultivator c, Realm next, long tick, bool divine, ref DetRandom rng)
        {
            float px = _e.X[c.Entity], py = _e.Y[c.Entity];
            string who = $"{c.Title} ({SectName(c)})";
            bool great = next >= Realm.HoaThan;
            float survive = (great ? 0.3f : 0.5f) + 0.3f * c.DaoHeart + 0.2f * c.Luck + 0.04f * Mathf.Min(3, c.Treasures);
            // Called down out of season, it is judged against the body that has to bear it: Luyện Khí rarely live.
            if (divine) survive += 0.06f * ((int)c.Realm - (int)Realm.KetDan);
            bool shielded = c.SectId >= 0 && IsAtHome(c); // hộ sơn đại trận
            if (shielded) survive += 0.05f;
            survive = 1f - Mathf.Clamp01((1f - survive) * _sim.Rules[Rule.TribulationHarshness]); // Quy luật
            _sim.Events.Add(tick, EventKind.Tribulation, 3,
                divine ? $"Thiên Đạo giáng {(great ? "đại thiên kiếp" : "thiên kiếp")} xuống {who}!"
                       : $"{(great ? "Đại thiên kiếp" : "Thiên kiếp")} giáng xuống {who} khi đột phá {Realms.Names[(int)next]}!",
                px, py, Fx.Tribulation, c.Index, -1, c.SectId);
            bool ambushed = Ambush(c, who, tick, ref rng);
            int radius = Mathf.Clamp(4 + 2 * (int)next, 6, 16);
            _sim.Disasters.TribulationStrikes(c, px, py, radius, divine, shielded, tick, ref rng);
            if (ambushed) return false;
            if (rng.NextFloat() < survive)
            {
                CombatSystem.Hurt(c, rng.Range(0.3f, 0.75f)); // through the lightning, but scorched to the bone
                return true;
            }
            Die(c, tick, $"{who} vẫn lạc dưới thiên kiếp.", 3, Fx.Lightning);
            return false;
        }

        // Thừa nước đục thả câu: a blood enemy, or a ma tu coveting the storage bag, falls on someone in the middle of their
        // tribulation, when all their strength goes into holding off the sky. Returns true if the one in tribulation died.
        bool Ambush(Cultivator c, string who, long tick, ref DetRandom rng)
        {
            const float Reach = 250f;
            float px = _e.X[c.Entity], py = _e.Y[c.Entity];
            Cultivator foe = null;
            float best = 0f;
            foreach (var o in All)
            {
                if (!o.Alive || o == c || o.AtWar || (int)o.Realm < (int)c.Realm - 1) continue;
                bool grudge = o.Nemesis == c.Index;
                bool greed = o.Demonic && !c.Demonic && (o.SectId < 0 || o.SectId != c.SectId);
                if (!grudge && !greed) continue;
                float dx = _e.X[o.Entity] - px, dy = _e.Y[o.Entity] - py;
                if (dx * dx + dy * dy > Reach * Reach) continue;
                float s = CombatSystem.Strength(o) * (grudge ? 2f : 1f);
                if (s > best) { best = s; foe = o; }
            }
            if (foe == null || rng.NextFloat() >= (foe.Nemesis == c.Index ? 0.6f : 0.15f)) return false;
            string them = $"{foe.Title} ({SectName(foe)})";
            float a = CombatSystem.Strength(foe) * rng.Range(0.6f, 1.4f);
            float d = CombatSystem.Strength(c) * 0.5f * rng.Range(0.6f, 1.4f);
            if (a > d)
            {
                _sim.Combat.Kill(foe, c, tick, $"{them} đánh lén {who} giữa lúc độ kiếp, khiến {c.Name} thân tử đạo tiêu.", 3);
                return true;
            }
            _sim.Combat.Kill(c, foe, tick, $"{them} định đánh lén {who} lúc độ kiếp, bị phản sát, thân xác tan dưới lôi kiếp.", 3);
            return false;
        }

        // Thiên Đạo reverses life and death: the fallen walk again with a fresh span of years, and whoever killed them,
        // if still alive, now owes them a blood debt.
        public bool Revive(Cultivator c, long tick)
        {
            if (c == null || c.Alive) return false;
            float x = c.HomeX, y = c.HomeY;
            if (!_w.IsWalkable(x, y) && FindDryGround(x, y, out float dx, out float dy)) { x = dx; y = dy; }
            if (c.SectId >= 0 && !_sim.Settlements.All[c.SectId].Alive) c.SectId = -1;
            int years = Mathf.Max(0, (int)((tick - c.DeathTick) / SimClock.DaysPerYear));
            c.HomeX = x;
            c.HomeY = y;
            c.Alive = true;
            c.DeathTick = -1;
            c.AtWar = c.Travelling = c.Away = false;
            c.Trip = Trip.None;
            c.HuntTarget = -1;
            c.Goal = Goal.None;
            c.GoalText = null;
            float left = c.LifespanYears - c.AgeYears(tick);
            if (left < 100f) c.BonusYears += Mathf.CeilToInt(100f - left);
            c.DaoHeart = Mathf.Min(1f, c.DaoHeart + 0.2f);
            c.Blessed = true;
            c.Entity = _e.Spawn(Species.Cultivator, x, y, c.BirthTick);
            _e.Payload[c.Entity] = c.Index;
            AliveCount++;
            CountByRealm[(int)c.Realm]++;
            var killer = c.KilledBy >= 0 && c.KilledBy != c.Index ? All[c.KilledBy] : null;
            c.KilledBy = -1;
            bool grudge = killer != null && killer.Alive;
            if (grudge)
            {
                c.Nemesis = killer.Index;
                c.NemesisFor = -1; // their own death
                c.NemesisTick = tick;
            }
            _sim.Events.Add(tick, EventKind.Divine, 3,
                $"Thiên Đạo nghịch chuyển sinh tử: {c.Title} ({SectName(c)}) sống lại sau {years} năm vẫn lạc" +
                (grudge ? $", lòng mang huyết thù với {killer.Title}." : "."), x, y, Fx.Blessing, c.Index, grudge ? killer.Index : -1, c.SectId);
            return true;
        }

        // Thiên Đạo calls down a tribulation on the chosen one. Stuck at a bottleneck, heaven opens the gate for whoever
        // survives; otherwise the lightning tempers body and heart. Either way it may simply kill them.
        public void CallTribulation(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return;
            var rng = RngFor(tick, 720000 + c.Index);
            bool atGate = c.Realm < Realm.HoaThan && Realms.IsPeak(c.Realm, c.Stage);
            var next = atGate ? c.Realm + 1 : c.Realm;
            if (!Tribulation(c, next, tick, true, ref rng)) return;
            float px = _e.X[c.Entity], py = _e.Y[c.Entity];
            if (atGate)
            {
                SetRealm(c, next, 0);
                c.Progress = 0f;
                c.FailedAttempts = 0;
                c.LastAttemptTick = tick;
                _sim.Events.Add(tick, EventKind.Breakthrough, next >= Realm.KetDan ? 3 : 2,
                    $"{c.Name} ({SectName(c)}) vượt qua thiên kiếp, mượn lôi kiếp phá tan bình cảnh, đột phá {Realms.Names[(int)next]}.",
                    px, py, Fx.LightPillar, c.Index, -1, c.SectId);
                _sim.Stories?.OnBreakthrough(c, tick);
                return;
            }
            c.Progress += Realms.Need(c.Realm, c.Stage) * 0.5f;
            c.DaoHeart = Mathf.Min(1f, c.DaoHeart + 0.15f);
            _sim.Events.Add(tick, EventKind.Tribulation, 2,
                $"{c.Title} ({SectName(c)}) chống đỡ được thiên kiếp, lôi kiếp tôi luyện thân thể, tâm cảnh càng thêm vững vàng.",
                px, py, Fx.LightPillar, c.Index, -1, c.SectId);
        }

        void SetRealm(Cultivator c, Realm realm, int stage)
        {
            CountByRealm[(int)c.Realm]--;
            c.Realm = realm;
            c.Stage = stage;
            CountByRealm[(int)realm]++;
        }

        void Die(Cultivator c, long tick, string text, int importance, Fx fx = Fx.None, Cultivator killer = null)
        {
            float x = _e.X[c.Entity], y = _e.Y[c.Entity];
            c.Alive = false;
            c.DeathTick = tick;
            c.AtWar = false;
            c.HuntTarget = -1;
            if (killer != null) c.KilledBy = killer.Index;
            AliveCount--;
            CountByRealm[(int)c.Realm]--;
            _e.Kill(c.Entity, DeathCause.Natural);
            // A legend's death is remembered at full weight.
            if (c.Legend) importance = Mathf.Max(importance, 3);
            _sim.Events.Add(tick, EventKind.Death, importance, text, x, y, fx, c.Index, killer?.Index ?? -1, c.SectId, killer?.SectId ?? -1);
            _sim.Relics?.OnDeath(c, tick); // the cave they leave behind may become a bí cảnh
        }

        // Killed by another cultivator (duel, battle, vendetta): the killer is remembered.
        public void Slay(Cultivator victim, Cultivator killer, long tick, string text, int importance) =>
            Die(victim, tick, text, importance, killer != null ? Fx.DuelKill : Fx.Explosion, killer);

        // Killed by a calamity (beast tide, a stray bolt of someone else's tribulation).
        public void Perish(Cultivator c, long tick, string text, int importance, Fx fx)
        {
            if (c.Alive) Die(c, tick, text, importance, fx);
        }

        // ---------------------------------------------------------------- yearly

        public void YearlyStep(long tick)
        {
            Awaken(tick);
            UpdateMasters(tick);
            int count = All.Count;
            for (int k = 0; k < count; k++)
            {
                var c = All[k];
                if (c.Alive && !c.Travelling && !c.Watched) ConsiderRelocating(c, tick); // nhân vật chính choose for themselves
                if (c.Alive && c.SectId >= 0) c.Stones += 1f + 2f * (int)c.Realm; // the sect's yearly stipend
            }
        }

        void Awaken(long tick)
        {
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                var rng = RngFor(tick, 500000 + s.Id);
                float qi = _sim.Qi.SampleQi(s.X, s.Y);
                float expected = s.Cohorts[2] / 5f * AwakenChance * (1f + qi / 3000f) * _sim.Rules[Rule.SpiritRoots];
                int awakened = Mathf.FloorToInt(expected) + (rng.NextFloat() < expected - Mathf.Floor(expected) ? 1 : 0);
                for (int k = 0; k < awakened; k++)
                {
                    if (!_sim.Settlements.TakeChild(s)) break;
                    // The sect whose land the village lies in takes its gifted children first.
                    var sect = _sim.Factions?.ProtectorOf(s.X, s.Y) ?? NearestSect(s.X, s.Y);
                    var c = Create(ref rng, Realm.LuyenKhi, 0, 10f, sect?.Id ?? -1, s.X + 0.5f, s.Y + 0.5f, tick);
                    c.Progress = 0f;
                    if (sect != null) Recruit(c, sect, ref rng);
                    bool gifted = SpiritRoots.Count(c.Roots) == 1;
                    _sim.Events.Add(tick, EventKind.Awakening, gifted ? 2 : 0,
                        $"Đứa trẻ {c.Name} ở {s.Name} lộ {SpiritRoots.Kind(c.Roots)} ({SpiritRoots.Elements(c.Roots)}){(sect != null ? $", được {sect.BaseName} thu nhận" : ", trở thành tán tu")}.", s.X + 0.5f, s.Y + 0.5f, Fx.None, c.Index, -1, c.SectId);
                }
            }
        }

        // A new disciple walks from the village to the sect, which becomes home.
        void Recruit(Cultivator c, Settlement sect, ref DetRandom rng)
        {
            c.HomeX = sect.X + 0.5f + rng.Range(-2f, 2f);
            c.HomeY = sect.Y + 0.5f + rng.Range(-2f, 2f);
            SendTo(c, c.HomeX, c.HomeY, Trip.Return);
            AssignMaster(c, sect.Id, ref rng);
            _sim.Techniques?.OnJoin(c, sect.Id); // the sect's method, if it serves them better
        }

        // Bái sư: a Kết Đan+ elder of the sect if there is one, otherwise someone of a higher realm.
        void AssignMaster(Cultivator c, int sectId, ref DetRandom rng)
        {
            c.MasterIdx = -1;
            int elders = 0, seniors = 0;
            foreach (var m in All)
            {
                if (!m.Alive || m == c || m.SectId != sectId || m.Realm <= c.Realm) continue;
                if (m.Realm >= Realm.KetDan) elders++;
                else if (m.Realm >= Realm.TrucCo) seniors++;
            }
            bool fromElders = elders > 0;
            int pick = rng.Range(0, Mathf.Max(1, fromElders ? elders : seniors));
            if (!fromElders && seniors == 0) return;
            foreach (var m in All)
            {
                if (!m.Alive || m == c || m.SectId != sectId || m.Realm <= c.Realm) continue;
                if (fromElders ? m.Realm < Realm.KetDan : m.Realm < Realm.TrucCo || m.Realm >= Realm.KetDan) continue;
                if (pick-- == 0) { c.MasterIdx = m.Index; return; }
            }
        }

        public Cultivator MasterOfDisciple(Cultivator c) => c.MasterIdx >= 0 ? All[c.MasterIdx] : null;

        Settlement NearestSect(int x, int y)
        {
            Settlement best = null;
            int bestD = SectRecruitRange * SectRecruitRange;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Sect || !s.Alive) continue;
                int d = (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y);
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        // Cultivators whose spot no longer feeds their realm look for a richer cave.
        void ConsiderRelocating(Cultivator c, long tick)
        {
            if (!CanRoam(c) || c.Away) return;
            float x = c.HomeX, y = c.HomeY;
            float need = Realms.RequiredQi[(int)c.Realm];
            float here = _sim.Qi.SampleQi((int)x, (int)y);
            if (here >= need * 0.9f) return;

            var rng = RngFor(tick, 900000 + c.Index);
            bool flies = c.Realm >= Realm.TrucCo;
            float reach = flies ? 220f : 80f;
            float bestScore = here * 1.25f, bx = -1f, by = -1f;
            for (int k = 0; k < 10; k++)
            {
                float tx = x + rng.Range(-reach, reach), ty = y + rng.Range(-reach, reach);
                if (!_w.InBounds((int)tx, (int)ty) || tx < 0f || ty < 0f) continue;
                if (!_w.IsWalkable(tx, ty)) continue; // even flyers land on solid ground
                float score = _sim.Qi.SampleQi((int)tx, (int)ty);
                if (score > bestScore) { bestScore = score; bx = tx; by = ty; }
            }
            // A ma tu who hears of an old battlefield goes where the oán khí is thick.
            Landmark field = null;
            if (c.Demonic && (field = _sim.Disasters.HeaviestBattlefield(x, y, reach)) != null)
            {
                float fx = field.X + 0.5f, fy = field.Y + 0.5f;
                float score = _sim.Qi.SampleQi(field.X, field.Y) * (1f + _sim.Disasters.GrudgeAt(fx, fy));
                if (score > bestScore && _w.IsWalkable(fx, fy)) { bestScore = score; bx = fx; by = fy; }
                else field = null;
            }
            if (bx < 0f) return;
            SendTo(c, bx, by, Trip.Relocate);
            if (field != null && c.Realm >= Realm.TrucCo)
                _sim.Events.Add(tick, EventKind.Relocation, 1, $"{c.Title} tìm đến {field.Name} luyện ma công giữa oán khí.", -1f, -1f, Fx.None, c.Index);
            else if (c.Realm >= Realm.KetDan)
                _sim.Events.Add(tick, EventKind.Relocation, 1, $"{c.Title} rời đi tìm động phủ có linh khí dồi dào hơn.", -1f, -1f, Fx.None, c.Index);
        }

        void SendTo(Cultivator c, float x, float y, Trip trip)
        {
            c.Travelling = true;
            c.Trip = trip;
            c.AtWar = trip == Trip.Battle;
            if (trip != Trip.Excursion) c.Away = false;
            _e.Flying[c.Entity] = c.Realm >= Realm.TrucCo; // từ Trúc Cơ: ngự kiếm phi hành
            _e.TX[c.Entity] = x;
            _e.TY[c.Entity] = y;
        }

        // Luyện Khí disciples keep to their sect; everyone else wanders out now and then.
        static bool CanRoam(Cultivator c) => !(c.SectId >= 0 && c.Realm == Realm.LuyenKhi);

        void StartOuting(Cultivator c, ref DetRandom rng)
        {
            bool flies = c.Realm >= Realm.TrucCo;
            float reach = flies ? 150f : 40f;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float tx = c.HomeX + rng.Range(-reach, reach), ty = c.HomeY + rng.Range(-reach, reach);
                if (!_w.IsWalkable(tx, ty)) continue;
                c.Outing = (Outing)rng.Range(0, 3);
                SendTo(c, tx, ty, Trip.Excursion);
                return;
            }
        }

        void Arrive(Cultivator c, long tick, ref DetRandom rng)
        {
            c.Travelling = false;
            _e.Flying[c.Entity] = false;
            var trip = c.Trip;
            c.Trip = Trip.None;
            switch (trip)
            {
                case Trip.Relocate:
                    c.HomeX = _e.X[c.Entity];
                    c.HomeY = _e.Y[c.Entity];
                    // Cause and effect across the ages: settling where an old calamity left its mark.
                    var mark = _sim.Disasters.LandmarkAt(c.HomeX, c.HomeY);
                    if (mark != null && c.Realm >= Realm.TrucCo)
                        _sim.Events.Add(tick, EventKind.Relocation, 1,
                            $"{c.Title} ({SectName(c)}) lập động phủ ở {mark.Name}, nơi {(mark.Kind == Landmark.Thunder ? "lôi khí còn sót lại từ " : "dưới chân ")}{mark.Origin}.",
                            c.HomeX, c.HomeY, Fx.None, c.Index, -1, c.SectId);
                    break;
                case Trip.Excursion:
                    c.Away = true;
                    c.StayUntil = tick + rng.Range(20, 90);
                    OutingReward(c, tick, ref rng);
                    break;
                case Trip.Battle:
                    c.Away = true; // holds the field until the battle is decided (StayUntil is a safety net)
                    break;
                case Trip.Hunt:
                    c.Away = true; // CombatSystem settles it or sends them on after the target (StayUntil = deadline)
                    break;
                default:
                    c.Away = false;
                    // Walkers find their way round on foot (NavSystem); only one with no road home at all (cut off on
                    // an islet by a flood) is set down there, rather than left stranded forever.
                    float dx = c.HomeX - _e.X[c.Entity], dy = c.HomeY - _e.Y[c.Entity];
                    if (dx * dx + dy * dy > 9f) Teleport(c, c.HomeX, c.HomeY);
                    break;
            }
        }

        void Teleport(Cultivator c, float x, float y)
        {
            int id = c.Entity;
            _e.X[id] = _e.PrevX[id] = _e.TX[id] = x;
            _e.Y[id] = _e.PrevY[id] = _e.TY[id] = y;
        }

        // Their cave became bare peak (water is handled at once by Flood): settle on the nearest walkable ground.
        void MoveHomeAshore(Cultivator c)
        {
            if (!FindDryGround(c.HomeX, c.HomeY, out float x, out float y)) return;
            c.HomeX = x;
            c.HomeY = y;
            Teleport(c, x, y);
        }

        // Water (or lava) just covered the rect: Luyện Khí standing there die; Trúc Cơ and above fly to the nearest shore.
        public void Flood(int x0, int y0, int x1, int y1, long tick)
        {
            foreach (var c in All)
            {
                if (!c.Alive) continue;
                float x = _e.X[c.Entity], y = _e.Y[c.Entity];
                bool inRect = x >= x0 && x <= x1 + 1 && y >= y0 && y <= y1 + 1;
                bool homeInRect = c.HomeX >= x0 && c.HomeX <= x1 + 1 && c.HomeY >= y0 && c.HomeY <= y1 + 1;
                bool airborne = c.Travelling && _e.Flying[c.Entity];

                if (inRect && !airborne && !_w.IsWalkable(x, y))
                {
                    if (c.Realm >= Realm.TrucCo)
                    {
                        bool atHome = IsAtHome(c);
                        if (FindDryGround(x, y, out float dx, out float dy))
                        {
                            Teleport(c, dx, dy);
                            if (atHome) { c.HomeX = dx; c.HomeY = dy; }
                        }
                        bool fire = _w.Terrain[_w.Idx((int)x, (int)y)] == Terrain.Lava;
                        if (c.Realm >= Realm.KetDan)
                            _sim.Events.Add(tick, EventKind.Fortune, 1, $"{c.Title} ngự kiếm thoát khỏi {(fire ? "biển lửa" : "biển nước")}.", x, y, Fx.None, c.Index);
                    }
                    else
                    {
                        bool fire = _w.Terrain[_w.Idx((int)x, (int)y)] == Terrain.Lava;
                        Die(c, tick, fire ? $"{c.Name} ({SectName(c)}) bị dung nham thiêu chết." : $"{c.Name} ({SectName(c)}) rơi xuống nước chết đuối.",
                            fire ? 1 : 0, fire ? Fx.Explosion : Fx.Splash);
                        continue;
                    }
                }
                if (homeInRect && !_w.IsWalkable(c.HomeX, c.HomeY) && FindDryGround(c.HomeX, c.HomeY, out float hx, out float hy))
                {
                    c.HomeX = hx;
                    c.HomeY = hy;
                }
            }
        }

        bool FindDryGround(float fx, float fy, out float x, out float y)
        {
            int cx = (int)fx, cy = (int)fy;
            for (int r = 1; r <= 80; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                x = cx + dx + 0.5f;
                y = cy + dy + 0.5f;
                if (_w.IsWalkable(x, y)) return true;
            }
            x = fx;
            y = fy;
            return false;
        }

        // The sect moved to new ground: members' homes follow, those at home go with it.
        public void RehomeSect(int sectId, int x, int y)
        {
            int k = 0;
            foreach (var c in All)
            {
                if (!c.Alive || c.SectId != sectId) continue;
                c.HomeX = x + 0.5f + (k % 5 - 2) * 0.6f;
                c.HomeY = y + 0.5f + (k / 5 % 5 - 2) * 0.6f;
                k++;
                if (!c.Travelling && !c.Away) Teleport(c, c.HomeX, c.HomeY);
            }
        }

        public void DisbandSect(int sectId, long tick)
        {
            string name = _sim.Settlements.All[sectId].BaseName;
            int n = 0;
            foreach (var c in All)
            {
                if (!c.Alive || c.SectId != sectId) continue;
                c.SectId = -1;
                n++;
            }
            _masters.Remove(sectId);
            if (n > 0) _sim.Events.Add(tick, EventKind.Disaster, 2, $"{name} tan rã, {n} tu sĩ trở thành tán tu.", -1f, -1f, Fx.None, -1, -1, sectId);
        }

        void OutingReward(Cultivator c, long tick, ref DetRandom rng)
        {
            switch (c.Outing)
            {
                case Outing.Sightseeing:
                    c.DaoHeart = Mathf.Min(1f, c.DaoHeart + 0.03f);
                    break;
                case Outing.Training:
                    c.Comprehension = Mathf.Min(1f, c.Comprehension + 0.02f);
                    break;
                default:
                    if (rng.NextFloat() < 0.25f)
                    {
                        c.Progress += Realms.Need(c.Realm, c.Stage) * 0.15f;
                        string herb = _w.Lore.Herbs[rng.Range(0, _w.Lore.Herbs.Length)];
                        _sim.Events.Add(tick, EventKind.Fortune, c.Realm >= Realm.KetDan ? 1 : 0, $"{c.Title} tìm được {herb}, tu vi tăng tiến.", -1f, -1f, Fx.None, c.Index);
                    }
                    break;
            }
        }

        // ---------------------------------------------------------------- thế lực (driven by FactionSystem)

        public void SendToBattle(Cultivator c, float x, float y, long holdUntil)
        {
            c.StayUntil = holdUntil;
            SendTo(c, x, y, Trip.Battle);
        }

        // Truy sát: flies after the target until `deadline`, retargeted each month by CombatSystem.
        public void SendToHunt(Cultivator c, float x, float y, long deadline)
        {
            c.StayUntil = deadline;
            SendTo(c, x, y, Trip.Hunt);
        }

        public void Retarget(Cultivator c, float x, float y)
        {
            _e.TX[c.Entity] = x;
            _e.TY[c.Entity] = y;
        }

        public void ReturnHome(Cultivator c)
        {
            if (!c.Alive) return;
            if (c.Travelling || c.Away) SendTo(c, c.HomeX, c.HomeY, Trip.Return);
            c.AtWar = false;
        }


        // Joins (or changes to) a sect; the sect becomes home and they head there.
        public void JoinSect(Cultivator c, Settlement sect, long tick)
        {
            c.SectId = sect.Id;
            var rng = RngFor(tick, 800000 + c.Index);
            Recruit(c, sect, ref rng);
        }

        public void LeaveSect(Cultivator c) => c.SectId = -1;

        // ---------------------------------------------------------------- nhân vật chính (driven by ProtagonistAI)

        public void SetWatched(Cultivator c, bool on, long tick)
        {
            if (c == null || c.Watched == on) return;
            c.Watched = on;
            if (!on) { c.Goal = Goal.None; c.GoalText = null; }
            _sim.Events.Add(tick, EventKind.Divine, 1, on ? $"Thiên Đạo để mắt tới {c.Title}." : $"Thiên Đạo thôi dõi theo {c.Title}.",
                _e.X[c.Entity], _e.Y[c.Entity], Fx.None, c.Index, -1, c.SectId);
        }

        // A purposeful trip: Relocate makes the destination home; Excursion stays a while and comes back.
        public void Travel(Cultivator c, float x, float y, Trip trip) => SendTo(c, x, y, trip);

        public void BringHome(Cultivator c)
        {
            if (c.Away) SendTo(c, c.HomeX, c.HomeY, Trip.Return);
        }

        public void JoinSectByChoice(Cultivator c, Settlement sect, long tick) => JoinSect(c, sect, tick);

        // The founder heads a new sect from day one (no succession event).
        public void SetMaster(int sectId, Cultivator c) => _masters[sectId] = c;

        // ---------------------------------------------------------------- Thiên Đạo

        // Ban linh căn on a chosen cultivator: a mixed root is refined to Thiên linh căn (one of its own elements),
        // a Thiên linh căn to Dị linh căn; a Dị linh căn cannot rise further, so insight and fortune grow instead.
        public bool GrantRootTo(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return false;
            var rng = RngFor(tick, 700000 + c.Index);
            string before = $"{SpiritRoots.Kind(c.Roots)} ({SpiritRoots.Elements(c.Roots)})";
            bool variant = (c.Roots & SpiritRoots.Variant) != 0;
            if (!variant && SpiritRoots.Count(c.Roots) > 1)
            {
                int pick = rng.Range(0, SpiritRoots.Count(c.Roots));
                for (int b = 0; b < 5; b++)
                {
                    if ((c.Roots & (1 << b)) == 0) continue;
                    if (pick-- == 0) { c.Roots = 1 << b; break; }
                }
            }
            else if (!variant) c.Roots = 1 << (5 + rng.Range(0, 3));
            c.Comprehension = Mathf.Max(c.Comprehension, 0.85f);
            c.Luck = Mathf.Max(c.Luck, 0.8f);
            string after = $"{SpiritRoots.Kind(c.Roots)} ({SpiritRoots.Elements(c.Roots)})";
            _sim.Events.Add(tick, EventKind.Divine, 2,
                variant ? $"Thiên Đạo điểm hóa {c.Title}: linh căn vốn đã cực phẩm, ngộ tính và khí vận tăng vọt."
                        : $"Thiên Đạo tẩy luyện linh căn của {c.Title}: {before} hóa thành {after}.",
                _e.X[c.Entity], _e.Y[c.Entity], Fx.Blessing, c.Index, -1, c.SectId);
            c.Blessed = true;
            return true;
        }

        // A mortal of the village takes up the path on their own (FaithSystem): a child of heaven's favour where
        // the people keep faith, a ma tu out of a cult where they have turned from heaven. No event: the caller tells it.
        public Cultivator RiseFromVillage(Settlement s, long tick, bool demonic, out float age)
        {
            age = 0f;
            if (s == null || !s.Alive || !_sim.Settlements.TakeAdult(s, out age)) return null;
            var rng = RngFor(tick, 715000 + s.Id);
            var sect = demonic ? null : _sim.Factions?.ProtectorOf(s.X, s.Y) ?? NearestSect(s.X, s.Y);
            var c = Create(ref rng, Realm.LuyenKhi, 0, age, sect?.Id ?? -1, s.X + 0.5f, s.Y + 0.5f, tick);
            c.Progress = 0f;
            if (demonic) c.Demonic = true;
            else c.Luck = Mathf.Max(c.Luck, 0.7f);
            if (sect != null) Recruit(c, sect, ref rng);
            else StartOuting(c, ref rng);
            return c;
        }

        // Ban linh căn on a mortal of a village: that grown man or woman awakens a heavenly root and sets out.
        public Cultivator AwakenMortal(Settlement s, long tick)
        {
            if (s == null || !s.Alive || !_sim.Settlements.TakeAdult(s, out float age)) return null;
            var rng = RngFor(tick, 710000 + s.Id);
            int roots = rng.NextFloat() < 0.5f ? 1 << (5 + rng.Range(0, 3)) : 1 << rng.Range(0, 5);
            var sect = _sim.Factions?.ProtectorOf(s.X, s.Y) ?? NearestSect(s.X, s.Y);
            var c = Create(ref rng, Realm.LuyenKhi, 0, age, sect?.Id ?? -1, s.X + 0.5f, s.Y + 0.5f, tick, roots);
            c.Progress = 0f;
            c.Comprehension = Mathf.Max(c.Comprehension, 0.85f);
            c.Blessed = true;
            c.Luck = Mathf.Max(c.Luck, 0.8f);
            // Either way they set out at once, so the player sees who was chosen.
            if (sect != null) Recruit(c, sect, ref rng);
            else StartOuting(c, ref rng);
            _sim.Events.Add(tick, EventKind.Divine, 2,
                $"Thiên Đạo điểm hóa {c.Name} ({age:0} tuổi) ở {s.Name}, thức tỉnh {SpiritRoots.Kind(roots)} ({SpiritRoots.Elements(roots)})" +
                (sect != null ? $", lên đường bái nhập {sect.BaseName}." : ", trở thành tán tu."), s.X + 0.5f, s.Y + 0.5f, Fx.Blessing, c.Index, -1, c.SectId);
            return c;
        }

        public void Bless(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return;
            c.Progress += Realms.Need(c.Realm, c.Stage) * 0.8f;
            c.Luck = 1f;
            c.DaoHeart = Mathf.Min(1f, c.DaoHeart + 0.2f);
            c.BonusYears += 20;
            c.Blessed = true;
            _sim.Events.Add(tick, EventKind.Divine, 1, $"{c.Title} gặp cơ duyên, tu vi tăng mạnh.", _e.X[c.Entity], _e.Y[c.Entity], Fx.Blessing, c.Index);
        }

        // ---------------------------------------------------------------- phúc / họa (Thiên Đạo, devlog 26)

        static readonly float[] DescendAge = { 16f, 22f, 90f, 230f, 450f, 900f };

        // A tán tu of the chosen realm appears at (x, y), the spot becoming their cave. From there they live like
        // anyone else: cultivate, roam, take disciples, found a sect if ambitious, make enemies.
        public Cultivator Descend(Realm realm, float x, float y, long tick)
        {
            if (realm < Realm.LuyenKhi || realm > Realm.HoaThan || !_w.IsWalkable(x, y)) return null;
            var rng = RngFor(tick, 720000 + All.Count);
            var c = Create(ref rng, realm, 0, DescendAge[(int)realm] * rng.Range(0.8f, 1.2f), -1, x, y, tick);
            // A method that can carry them at least one realm further (a Hóa Thần is given a cực phẩm one).
            int grade = realm <= Realm.TrucCo ? 2 : (int)realm;
            if (_sim.Techniques != null) c.Technique = _sim.Techniques.New(grade, -2, false, -1, tick).Index;
            c.Blessed = true;
            _sim.Events.Add(tick, EventKind.Divine, realm >= Realm.KetDan ? 2 : 1,
                $"Thiên Đạo đưa {c.Title} ({Realms.Names[(int)realm]}, {SpiritRoots.Kind(c.Roots)}) xuống nhân gian, làm một tán tu.",
                x, y, Fx.LightPillar, c.Index);
            return c;
        }

        // Ban pháp bảo: one more treasure (stronger in a fight, steadier under thiên kiếp); a coveted one, too,
        // for a ma tu falls on whoever carries one (CombatSystem: giết người đoạt bảo).
        public void GrantTreasure(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return;
            var rng = RngFor(tick, 730000 + c.Index);
            c.Treasures++;
            c.TreasureName = _w.Lore.Treasures[rng.Range(0, _w.Lore.Treasures.Length)];
            c.Blessed = true;
            _sim.Events.Add(tick, EventKind.Divine, 2, $"Thiên Đạo ban cho {c.Title} ({SectName(c)}) pháp bảo {c.TreasureName}.",
                _e.X[c.Entity], _e.Y[c.Entity], Fx.Blessing, c.Index, -1, c.SectId);
        }

        // Giáng tâm ma: the dao heart shaken (weaker, slower to break through, likelier to deviate later), and
        // the weaker the heart was, the likelier the demon wins at once: dead, fallen a realm, or turned ma tu.
        public void HeartDemon(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return;
            var rng = RngFor(tick, 740000 + c.Index);
            string who = $"{c.Title} ({SectName(c)})";
            float px = _e.X[c.Entity], py = _e.Y[c.Entity];
            float heart = c.DaoHeart;
            c.DaoHeart = Mathf.Max(0f, c.DaoHeart - 0.45f);
            c.Progress *= 0.5f;
            if (rng.NextFloat() >= 0.75f - 0.6f * heart)
            {
                _sim.Events.Add(tick, EventKind.Deviation, 1, $"Thiên Đạo giáng tâm ma xuống {who}: đạo tâm lung lay, tu vi trì trệ.", px, py, Fx.DemonBlast, c.Index, -1, c.SectId);
                return;
            }
            float outcome = rng.NextFloat();
            if (outcome < 0.25f)
                Die(c, tick, $"Tâm ma Thiên Đạo giáng xuống nuốt chửng {who}, kinh mạch đứt đoạn mà chết.", c.Realm >= Realm.TrucCo ? 2 : 1, Fx.DemonBlast);
            else if (outcome < 0.6f || c.Demonic || !_sim.Rules.DemonicAllowed)
            {
                if (c.Realm > Realm.LuyenKhi) SetRealm(c, c.Realm - 1, Realms.Stages[(int)c.Realm - 1] - 1);
                else c.Stage = Mathf.Max(0, c.Stage - 3);
                c.Progress = 0f;
                _sim.Events.Add(tick, EventKind.Deviation, 2, $"{who} bị tâm ma Thiên Đạo giáng xuống quấy phá, tẩu hỏa nhập ma, tu vi tụt xuống {c.RealmText}.", px, py, Fx.DemonBlast, c.Index, -1, c.SectId);
            }
            else
            {
                c.Demonic = true;
                _sim.Events.Add(tick, EventKind.Deviation, 2, $"{who} không thắng nổi tâm ma Thiên Đạo giáng xuống, sa vào ma đạo.", px, py, Fx.DemonBlast, c.Index, -1, c.SectId);
            }
        }

        // Phế tu vi: a whole great realm torn away (Luyện Khí lose all their tầng). Strength, sinh lực and thọ nguyên
        // fall with it; one who has outlived the lower realm's span will not see many more years.
        public void Cripple(Cultivator c, long tick)
        {
            if (c == null || !c.Alive) return;
            string who = $"{c.Title} ({SectName(c)})";
            var from = c.Realm;
            if (c.Realm > Realm.LuyenKhi) SetRealm(c, c.Realm - 1, 0);
            else c.Stage = 0;
            c.Progress = 0f;
            c.DaoHeart = Mathf.Max(0f, c.DaoHeart - 0.2f);
            c.Hp = Mathf.Min(CombatSystem.HpOf(c), CombatSystem.MaxHp(c));
            if (c.Realm < Realm.TrucCo) _e.Flying[c.Entity] = false; // the sword no longer answers: walking home
            bool old = c.AgeYears(tick) > c.LifespanYears;
            _sim.Events.Add(tick, EventKind.Divine, from >= Realm.KetDan ? 3 : 2,
                $"Thiên Đạo phế bỏ tu vi của {who}: từ {Realms.Names[(int)from]} rơi xuống {c.RealmText}" + (old ? ", thọ nguyên đã cạn." : "."),
                _e.X[c.Entity], _e.Y[c.Entity], Fx.DemonBlast, c.Index, -1, c.SectId);
        }

        public void HashInto(ref ulong h)
        {
            foreach (var c in All)
            {
                StateHash.Add(ref h, c.Alive ? c.Index : -c.Index - 1);
                StateHash.Add(ref h, (int)c.Realm | (c.Stage << 8) | (c.Demonic ? 1 << 16 : 0) | (c.Travelling ? 1 << 17 : 0) |
                                     (c.Away ? 1 << 18 : 0) | ((int)c.Trip << 20) | ((long)c.StayUntil << 24));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(c.Progress));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(c.DaoHeart));
                StateHash.Add(ref h, c.SectId | (c.AtWar ? 1L << 32 : 0) | (c.Watched ? 1L << 33 : 0) | ((long)c.Pills << 40) | ((long)c.Goal << 50));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(c.Stones));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(c.Hp));
                StateHash.Add(ref h, c.Technique);
            }
        }
    }
}
