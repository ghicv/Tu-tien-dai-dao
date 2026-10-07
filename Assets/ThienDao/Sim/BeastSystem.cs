using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public sealed class Beast
    {
        public static readonly string[] GradeNames = { "", "nhất giai", "nhị giai", "tam giai", "tứ giai", "ngũ giai", "lục giai", "thất giai", "bát giai", "cửu giai" };

        public int Index;
        public string Name;          // "Xích Mục Yêu Lang"
        public Species From;         // the animal it once was
        public int Grade = 1;        // 1..9: nhất giai (≈ Luyện Khí) … cửu giai (≈ Hóa Thần)
        public float Progress;
        public long BirthTick;
        public float HomeX, HomeY;   // its lair
        public int Entity;
        public bool Alive = true;
        public long DeathTick = -1;
        public int Clan = -1;        // the Yêu Vương it follows (its own index if it is the king)
        public string ClanName;      // set on the king
        public int Kills;            // people and cultivators
        public int KilledBy = -1;    // a cultivator's index
        public bool Humanoid;        // hóa hình
        public BeastKind Kind;       // what it looks and fights like (BeastSystem.Kinds)
        public bool Rampage;         // hung thú: roams from town to town, killing (BeastHorde.cs)
        public int Prey = -1;        // the settlement it is heading for
        public long RestUntil;       // gorged after a massacre
        public int Ravaged;          // towns it has fallen on
        public int LastPrey = -1, PreyBefore = -1; // the last two it ravaged: it moves on rather than circling back
        public long RampageSince, CalmUntil;        // when its rampage began; asleep again, it will not wake before CalmUntil
        public float Hp = -1f;                      // sinh lực; -1 = whole (BeastSystem.HpOf); a hung thú keeps its wounds
        public float DeathX = -1, DeathY = -1; // where it fell (fight scenes draw its kind)

        public bool IsKing => Clan == Index;
        public float AgeYears(long tick) => (tick - BirthTick) / (float)SimClock.DaysPerYear;
        public int LifespanYears => BeastSystem.Lifespan[Grade];
        public string Title => Rampage ? $"Hung thú {Name}" : IsKing ? $"Yêu Vương {Name}" : Humanoid ? $"{Name} (hóa hình)" : Name;
        public string GradeText => GradeNames[Grade];
    }

    // Yêu thú (GDD §10): wild animals that live long in rich qi open their spirit and become beasts of a grade,
    // growing stronger by drawing qi. They hold a lair, prey on the herds, raid villages and meet cultivators on
    // the road (yêu đan for the victor). A strong one may proclaim itself Yêu Vương and gather a yêu tộc, which
    // raids the lands of men until a sect comes to cut the king down.
    public sealed partial class BeastSystem
    {
        public const int MaxAlive = 160;
        public static readonly int[] Lifespan = { 0, 80, 150, 250, 400, 600, 900, 1300, 2000, 3000 };
        static readonly float[] Need = { 0f, 30f, 80f, 180f, 380f, 800f, 1700f, 3500f, 7000f, 99999f }; // progress to try the next grade
        static readonly float[] GradeUpChance = { 0f, 0.6f, 0.45f, 0.3f, 0.18f, 0.1f, 0.05f, 0.025f, 0.01f, 0f };
        const int KingGrade = 5, HumanoidGrade = 7;

        readonly Simulation _sim;
        readonly WorldData _w;
        readonly EntityStore _e;
        public readonly List<Beast> All = new List<Beast>();
        readonly Lore.Picker _clanNames;
        public int AliveCount { get; private set; }
        public readonly int[] CountByGrade = new int[10];

        public BeastSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _clanNames = new Lore.Picker(_w.Lore.BeastClans, _w.Lore.Syllables);
            _e = sim.Entities;
            SeedInitial();
        }

        // The world starts with a few beasts already grown in the wild places where qi runs deep.
        void SeedInitial()
        {
            var rng = new DetRandom(_w.Seed ^ 0xBEA51u);
            for (int attempt = 0, made = 0; attempt < 3000 && made < 14; attempt++)
            {
                int x = rng.Range(16, _w.W - 16), y = rng.Range(16, _w.H - 16);
                int i = _w.Idx(x, y);
                if (!TerrainInfo.IsWalkable(_w.Terrain[i]) || _w.Owner[i] != 0 || _w.QiCap[i] < 3000 || NearPeople(x, y, 40f)) continue;
                var from = rng.NextFloat() < 0.6f ? Species.Wolf : rng.NextFloat() < 0.6f ? Species.Deer : Species.Rabbit;
                Spawn(from, rng.Range(1, 5), x + 0.5f, y + 0.5f, 0, ref rng);
                made++;
            }
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0xBEA57u, (int)tick, salt));

        // Fighting strength, on the cultivators' scale: two grades to a realm.
        public static float Strength(Beast b) => Realms.Power[(b.Grade + 1) / 2] * (b.Grade % 2 == 0 ? 2.2f : 1f) * (b.IsKing ? 1.3f : 1f) * (0.5f + 0.5f * HpFrac(b));

        // Sinh lực: like a cultivator of the matching realm, a hide half again thicker on the even grades.
        public static float MaxHp(Beast b) => Realms.Hp[(b.Grade + 1) / 2] * (b.Grade % 2 == 0 ? 1.6f : 1f) * (b.IsKing || b.Rampage ? 1.3f : 1f);
        public static float HpOf(Beast b) => b.Hp < 0f ? MaxHp(b) : Mathf.Min(b.Hp, MaxHp(b));
        public static float HpFrac(Beast b) => b.Hp < 0f ? 1f : Mathf.Clamp01(b.Hp / MaxHp(b));
        public static void Hurt(Beast b, float share) => b.Hp = Mathf.Max(1f, HpOf(b) - MaxHp(b) * Mathf.Clamp01(share));

        public Beast ForEntity(int entity)
        {
            if (!_e.IsAlive(entity) || _e.Species[entity] != Species.Beast) return null;
            int i = _e.Payload[entity];
            return i >= 0 && i < All.Count ? All[i] : null;
        }

        public Beast KingOf(Beast b) => b.Clan >= 0 ? All[b.Clan] : null;

        public int ClanSize(Beast king)
        {
            int n = 0;
            foreach (var b in All)
                if (b.Alive && b.Clan == king.Index) n++;
            return n;
        }

        // ---------------------------------------------------------------- birth

        public Beast Spawn(Species from, int grade, float x, float y, long tick, ref DetRandom rng, BeastKind kind = BeastKind.Count)
        {
            if (kind == BeastKind.Count) kind = KindFor(x, y, ref rng);
            var b = new Beast
            {
                Index = All.Count,
                Name = $"{_w.Lore.BeastEpithets[rng.Range(0, _w.Lore.BeastEpithets.Length)]} {KindName(kind, ref rng)}",
                From = from,
                Kind = kind,
                Grade = Mathf.Clamp(grade, 1, 9),
                BirthTick = tick - (long)(rng.Range(20f, 60f) * SimClock.DaysPerYear),
                HomeX = x,
                HomeY = y
            };
            b.Entity = _e.Spawn(Species.Beast, x, y, b.BirthTick);
            _e.Payload[b.Entity] = b.Index;
            _e.Flying[b.Entity] = Flies(kind); // điêu, giao long and huyết bức take to the air
            All.Add(b);
            AliveCount++;
            CountByGrade[b.Grade]++;
            return b;
        }

        public void Perish(Beast b, long tick, string text, int importance, Fx fx = Fx.None)
        {
            if (b.Alive) Die(b, tick, text, importance, null, fx);
        }

        void Die(Beast b, long tick, string text, int importance, Cultivator killer = null, Fx fx = Fx.None)
        {
            float x = _e.X[b.Entity], y = _e.Y[b.Entity];
            b.DeathX = x;
            b.DeathY = y;
            b.Alive = false;
            b.DeathTick = tick;
            if (killer != null) b.KilledBy = killer.Index;
            AliveCount--;
            CountByGrade[b.Grade]--;
            _e.Kill(b.Entity, DeathCause.Natural);
            if (b.IsKing) // the yêu tộc scatters
                foreach (var o in All)
                    if (o.Clan == b.Index) o.Clan = -1;
            b.Clan = -1;
            _sim.Events.Add(tick, EventKind.Beast, importance, text, x, y, killer != null ? Fx.BeastSlain : fx, killer?.Index ?? -1, -1, killer?.SectId ?? -1);
        }

        // Each year, in regions where animals have lived long under rich qi, one may open its spirit.
        void Awaken(long tick)
        {
            if (AliveCount >= MaxAlive) return;
            var wild = _sim.Wildlife;
            var rng = RngFor(tick, 1);
            const int size = WildlifeSystem.Region;
            for (int r = 0; r < wild.RW * wild.RH && AliveCount < MaxAlive; r++)
            {
                int cx = r % wild.RW * size + size / 2, cy = r / wild.RW * size + size / 2;
                float qi = _sim.Qi.SampleQi(cx, cy);
                if (qi < 2500f) continue;
                float wolves = wild.At(Species.Wolf, r), deer = wild.At(Species.Deer, r), rabbits = wild.At(Species.Rabbit, r);
                float chance = Mathf.Min(0.25f, (wolves * 6f + deer + rabbits * 0.3f) / 2000f) * (qi / 4000f);
                if (rng.NextFloat() >= chance) continue;
                // Somewhere walkable in the region, out in the wilds: not where a sect would see it born.
                for (int k = 0; k < 8; k++)
                {
                    float x = r % wild.RW * size + rng.Range(4f, size - 4f), y = r / wild.RW * size + rng.Range(4f, size - 4f);
                    if (!_w.IsWalkable(x, y) || _w.Owner[_w.Idx((int)x, (int)y)] != 0 || NearPeople(x, y, 40f)) continue;
                    float pick = rng.NextFloat() * (wolves * 6f + deer + rabbits * 0.3f);
                    var from = pick < wolves * 6f ? Species.Wolf : pick < wolves * 6f + deer ? Species.Deer : Species.Rabbit;
                    var b = Spawn(from, 1, x, y, tick, ref rng);
                    _sim.Events.Add(tick, EventKind.Beast, 1,
                        $"Một con {SpeciesInfo.Names[(int)from].ToLower()} sống lâu nơi linh khí dồi dào, khai mở linh trí thành {b.Name} ({b.GradeText}).", x, y);
                    break;
                }
            }
        }

        bool NearPeople(float x, float y, float r)
        {
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) < r * r) return true;
            return false;
        }

        // ---------------------------------------------------------------- monthly: grow, prowl, prey, meet

        public void MonthlyStep(long tick)
        {
            var wild = _sim.Wildlife;
            int n = All.Count;
            for (int k = 0; k < n; k++)
            {
                var b = All[k];
                if (!b.Alive) continue;
                var rng = RngFor(tick, 10 + b.Index);
                if (b.AgeYears(tick) >= b.LifespanYears)
                {
                    Die(b, tick, $"{b.Title} ({b.GradeText}) thọ nguyên đã tận, chết già trong hang ổ.", b.Grade >= KingGrade ? 2 : 0);
                    continue;
                }
                if (!b.Rampage && !Flies(b.Kind) && !_w.IsWalkable(b.HomeX, b.HomeY)) // its lair was flooded or buried (hung thú and fliers keep none)
                {
                    Die(b, tick, $"{b.Title} mất hang ổ giữa thiên tai, bỏ mạng.", b.Grade >= 3 ? 1 : 0);
                    continue;
                }

                // Draws qi like a cultivator; the richer the lair, the faster it grows.
                int hx = (int)b.HomeX, hy = (int)b.HomeY;
                float qi = _sim.Qi.SampleQi(hx, hy);
                b.Progress += Mathf.Clamp(qi / (1500f + 800f * b.Grade), 0.1f, 1.5f) * (1f + 0.25f * b.Grade);
                _sim.Qi.AddQi(hx, hy, 4, -3f * b.Grade);
                if (b.Grade < 9 && b.Progress >= Need[b.Grade])
                {
                    b.Progress = 0f;
                    if (rng.NextFloat() < GradeUpChance[b.Grade])
                    {
                        CountByGrade[b.Grade]--;
                        b.Grade++;
                        CountByGrade[b.Grade]++;
                        bool human = b.Grade == HumanoidGrade;
                        if (human) b.Humanoid = true;
                        _sim.Events.Add(tick, EventKind.Beast, b.Grade >= KingGrade ? 2 : b.Grade >= 3 ? 1 : 0,
                            human ? $"{b.Name} tu luyện đến {b.GradeText}, hóa hình thành người, yêu khí ngút trời."
                                  : $"{b.Title} tiến giai lên {b.GradeText}.", b.HomeX, b.HomeY, Fx.LightPillar);
                    }
                }

                // Wounds close in the lair; a roaming hung thú heals slowly, so the last hunt's blows still tell.
                if (b.Hp >= 0f)
                {
                    b.Hp += MaxHp(b) * (b.Rampage ? 0.04f : 0.12f);
                    if (b.Hp >= MaxHp(b)) b.Hp = -1f;
                }

                // A hung thú does not keep a lair: it goes from town to town (BeastHorde.cs).
                if (b.Rampage)
                {
                    RampageStep(b, tick, ref rng);
                    continue;
                }
                // One that has gone back to sleep sleeps: no prowling, no raids, no clan, until it wakes.
                if (tick < b.CalmUntil) continue;

                // Preys on the herds around its lair.
                int region = wild.RegionOf(b.HomeX, b.HomeY);
                wild.Cull(Species.Deer, region, 1f - 0.003f * b.Grade);
                wild.Cull(Species.Rabbit, region, 1f - 0.004f * b.Grade);

                // Prowls its territory.
                if (rng.NextFloat() < 0.5f)
                {
                    float r = 4f + b.Grade;
                    float tx = b.HomeX + rng.Range(-r, r), ty = b.HomeY + rng.Range(-r, r);
                    if (_w.IsWalkable(tx, ty))
                    {
                        _e.TX[b.Entity] = tx;
                        _e.TY[b.Entity] = ty;
                    }
                }

                if (b.Grade >= 2 && rng.NextFloat() < 0.012f * b.Grade) Raid(b, tick, ref rng);
                if (b.Alive) Encounter(b, tick, ref rng);
            }
            CoalitionStep(tick);
        }

        // A hungry beast falls on the nearest village in its territory; if a sect guards the land, it sends someone.
        void Raid(Beast b, long tick, ref DetRandom rng)
        {
            float reach = 20f + 5f * b.Grade;
            Settlement target = null;
            float best = reach * reach;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                float dx = s.X - b.HomeX, dy = s.Y - b.HomeY, d = dx * dx + dy * dy;
                if (d < best) { best = d; target = s; }
            }
            if (target == null) return;
            var guard = target.Sect ? target : _sim.Factions.ProtectorOf(target.X, target.Y);
            var defender = guard != null ? StrongestAtHome(guard.Id) : null;
            // Beasts have instinct: a land guarded by someone far stronger is left alone most of the time.
            if (defender != null && CombatSystem.Strength(defender) > Strength(b) * 1.5f && rng.NextFloat() < 0.8f) return;
            if (defender != null && rng.NextFloat() < 0.6f)
            {
                Fight(defender, b, tick, $"ra tay bảo vệ {target.Name}", ref rng);
                return;
            }
            int dead = _sim.Settlements.Kill(target, Mathf.Max(1, (int)(target.Population * rng.Range(0.01f, 0.025f) * b.Grade)));
            b.Kills += dead;
            _sim.Events.Add(tick, EventKind.Beast, dead >= 20 ? 2 : 1, $"{b.Title} ({b.GradeText}) tập kích {target.Name}, {dead} người bị ăn thịt.",
                target.X + 0.5f, target.Y + 0.5f, Fx.Stampede);
        }

        Cultivator StrongestAtHome(int sectId)
        {
            Cultivator best = null;
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.SectId == sectId && _sim.Cultivation.IsAtHome(c) && !c.AtWar && (best == null || c.Rank > best.Rank)) best = c;
            return best;
        }

        // Cultivators out on the road who pass a beast's lair: some hunt it for its yêu đan, the weak run or die.
        void Encounter(Beast b, long tick, ref DetRandom rng)
        {
            int id = _sim.Creatures.FindNearest(_e.X[b.Entity], _e.Y[b.Entity], 12f, 1 << (int)Species.Cultivator);
            var c = _sim.Cultivation.ForEntity(id);
            if (c == null || !_sim.Cultivation.IsShownOnMap(c) || c.AtWar || c.HuntTarget >= 0 || tick - c.LastDuelTick < SimClock.DaysPerMonth * 6) return;
            if (rng.NextFloat() >= 0.35f * CreatureSystem.RoadPace) return;
            Fight(c, b, tick, CombatSystem.Strength(c) >= Strength(b) ? "săn yêu đan" : "lỡ bước vào lãnh địa yêu thú", ref rng);
        }

        // Tu sĩ against yêu thú. The loser usually dies; the cultivator who wins takes its yêu đan.
        public void Fight(Cultivator c, Beast b, long tick, string context, ref DetRandom rng)
        {
            c.LastDuelTick = tick;
            float sc = CombatSystem.Strength(c) * rng.Range(0.6f, 1.4f), sb = Strength(b) * rng.Range(0.6f, 1.4f);
            string who = $"{c.Title} ({_sim.Cultivation.SectName(c)})";
            if (sc >= sb)
            {
                CombatSystem.Hurt(c, Mathf.Clamp(0.45f * sb / Mathf.Max(0.01f, sc), 0.05f, 0.7f)); // claws leave their marks
                c.Kills++;
                // Yêu đan: worth linh thạch, and from a strong beast, a pill for the next gate.
                c.Stones += 25f * b.Grade * b.Grade;
                if (b.Grade >= 3) c.Pills++;
                c.Progress += Realms.Need(c.Realm, c.Stage) * 0.05f * b.Grade;
                int imp = b.IsKing ? 3 : b.Grade >= KingGrade ? 2 : 1;
                string king = b.IsKing ? $", {b.ClanName} tan rã" : "";
                Die(b, tick, $"{who} {context}, trảm sát {b.Title} ({b.GradeText}), đoạt yêu đan{king}.", imp, c);
                return;
            }
            b.Kills++;
            Hurt(b, Mathf.Clamp(0.45f * sc / Mathf.Max(0.01f, sb), 0.05f, 0.7f));
            float death = Mathf.Clamp(0.5f * sb / Mathf.Max(1f, sc), 0.2f, 0.9f);
            if (rng.NextFloat() < death)
            {
                _sim.Cultivation.Perish(c, tick, $"{who} {context}, bị {b.Title} ({b.GradeText}) xé xác.", c.Realm >= Realm.KetDan ? 2 : 1, Fx.BeastKill);
                return;
            }
            c.Progress *= 0.75f;
            c.Hp = Mathf.Max(1f, Mathf.Min(CombatSystem.HpOf(c), CombatSystem.MaxHp(c) * rng.Range(0.1f, 0.35f)));
            float fx = _e.X[c.Entity], fy = _e.Y[c.Entity];
            _sim.Cultivation.ReturnHome(c);
            _sim.Events.Add(tick, EventKind.Beast, 1, $"{who} {context}, không địch nổi {b.Title}, trọng thương bỏ chạy.",
                fx, fy, Fx.BeastFlee, c.Index, -1, c.SectId);
        }

        // ---------------------------------------------------------------- yearly: awakenings, yêu vương, raids, sect hunts

        public void YearlyStep(long tick)
        {
            Awaken(tick);
            Emerge(tick);
            var rng = RngFor(tick, 2);
            // A strong beast with lesser ones around it proclaims itself king.
            foreach (var b in All)
            {
                if (!b.Alive || b.Clan >= 0 || b.Grade < KingGrade || b.Rampage || tick < b.CalmUntil) continue;
                int followers = 0;
                foreach (var o in All)
                    if (o.Alive && o != b && o.Clan < 0 && o.Grade < b.Grade && Near(o, b, 160f)) followers++;
                if (followers < 2) continue;
                b.Clan = b.Index;
                b.ClanName = _clanNames.Next(ref rng); // each yêu tộc its own name
                if (System.Array.IndexOf(LoreDatabase.Beasts.beastClans, b.ClanName) < 0) b.ClanName += " Yêu Tộc";
                foreach (var o in All)
                    if (o.Alive && o != b && o.Clan < 0 && o.Grade < b.Grade && Near(o, b, 160f)) o.Clan = b.Index;
                _sim.Events.Add(tick, EventKind.Beast, 3, $"{b.Name} ({b.GradeText}) tự xưng Yêu Vương, thu phục {followers} yêu thú, lập nên {b.ClanName}.",
                    b.HomeX, b.HomeY, Fx.Stampede);
            }
            foreach (var king in All)
            {
                if (!king.Alive || !king.IsKing) continue;
                int size = ClanSize(king);
                // The yêu tộc comes down on the lands of men.
                if (rng.NextFloat() < 0.15f + 0.03f * size)
                {
                    Settlement target = null;
                    float best = 200f * 200f;
                    foreach (var s in _sim.Settlements.All)
                    {
                        if (!s.Alive) continue;
                        float dx = s.X - king.HomeX, dy = s.Y - king.HomeY, d = dx * dx + dy * dy;
                        if (d < best) { best = d; target = s; }
                    }
                    if (target != null) _sim.Disasters.TideOf(king.ClanName, target.X, target.Y, 40 * size, tick);
                }
                // A sect whose land lies near sends its strongest to cut down the king.
                var hunter = SectChampionNear(king, 220f);
                if (hunter != null && rng.NextFloat() < 0.35f)
                    Fight(hunter, king, tick, $"dẫn đệ tử chinh phạt {king.ClanName}", ref rng);
            }
        }

        static bool Near(Beast a, Beast b, float r) => (a.HomeX - b.HomeX) * (a.HomeX - b.HomeX) + (a.HomeY - b.HomeY) * (a.HomeY - b.HomeY) <= r * r;

        Cultivator SectChampionNear(Beast king, float reach)
        {
            Cultivator best = null;
            foreach (var f in _sim.Factions.All)
            {
                if (!f.Alive) continue;
                var s = _sim.Settlements.All[f.Id];
                float dx = s.X - king.HomeX, dy = s.Y - king.HomeY;
                if (dx * dx + dy * dy > reach * reach) continue;
                var c = StrongestAtHome(f.Id);
                if (c != null && CombatSystem.Strength(c) >= Strength(king) * 0.8f && (best == null || c.Rank > best.Rank)) best = c;
            }
            return best;
        }

        // Water or lava covered the rect: beasts there drown or burn unless their grade carries them out.
        public void Flood(int x0, int y0, int x1, int y1, long tick)
        {
            foreach (var b in All)
            {
                if (!b.Alive) continue;
                float x = _e.X[b.Entity], y = _e.Y[b.Entity];
                if (x < x0 || x > x1 + 1 || y < y0 || y > y1 + 1 || _w.IsWalkable(x, y)) continue;
                if (b.Grade >= 4) continue; // it moves its lair next month or dies there
                Die(b, tick, $"{b.Title} bị thiên tai nuốt chửng.", 0);
            }
        }

        public void HashInto(ref ulong h)
        {
            foreach (var b in All)
            {
                StateHash.Add(ref h, b.Alive ? b.Index : -b.Index - 1);
                StateHash.Add(ref h, b.Grade | ((long)b.Clan << 8) | ((long)b.Kills << 32));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(b.Progress));
                StateHash.Add(ref h, System.BitConverter.SingleToInt32Bits(b.Hp));
            }
        }
    }
}
