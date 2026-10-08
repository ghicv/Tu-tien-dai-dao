using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Sim
{
    // động phủ of the dead, di tích of a fallen sect, thiên địa linh vật, cổ mộ and thượng cổ di tích from before history
    public enum RelicKind : byte { Cave, Ruins, Treasure, Tomb, Ancient }

    public sealed class Relic
    {
        public int Index;
        public RelicKind Kind;
        public string Name;          // "Động phủ của Hàn Lập"
        public int X, Y;
        public int Tier;             // 1..5, the realm it was made at: danger and reward
        public int Owner = -1;       // the cultivator who left it
        public int Sect = -1;        // the sect it came from
        public string Origin;        // "Kết Đan, vẫn lạc năm 312"
        public string Treasure;      // a named pháp bảo inside, if any
        public float Stones;
        public int Pills;
        public long Tick;
        public bool Discovered;
        public int DiscoveredBy = -1;
        public int Layers = 1;       // explorations left before it is empty
        public bool Open => Layers > 0;
        public int ObjectId = -1;    // its building on the map (RelicCave … RelicTreasure), -1 if none stands
        public int Technique = -1;   // a ngọc giản inside (index into Techniques.All), -1 if none: truyền thừa (TechniqueSystem)
    }

    // Bí cảnh (GDD §11): the past becomes the present. Strong cultivators who die leave their caves (and whatever
    // treasure was not taken from them); fallen sects leave ruins; the land itself hides wonders under its volcanoes
    // and lôi địa. These lie hidden until someone passing by finds them; then the bold go in, for the dead's
    // pháp bảo and truyền thừa, or never come out.
    public sealed class RelicSystem
    {
        const float FindReach = 25f;
        const int MaxOpen = 120;

        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly List<Relic> All = new List<Relic>();

        public RelicSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            _w.Objects.Removed += OnObjectRemoved;
            SeedAncient();
        }

        // ---------------------------------------------------------------- on the map

        static ObjectType ArtOf(RelicKind k) =>
            k == RelicKind.Cave ? ObjectType.RelicCave : k == RelicKind.Ruins ? ObjectType.RelicRuins :
            k == RelicKind.Tomb ? ObjectType.RelicTomb : k == RelicKind.Ancient ? ObjectType.RelicAncient : ObjectType.RelicTreasure;

        // The site stands on the land whether or not anyone knows what it is: the nearest free ground to where it
        // came to be. Trees and rocks give way; fields, houses and walls do not.
        void PlaceArt(Relic r)
        {
            var type = ArtOf(r.Kind);
            int fw = ObjectInfo.FootprintW[(int)type], fh = ObjectInfo.FootprintH[(int)type];
            for (int ring = 0; ring <= 12; ring++) // out past a sect town's fields if need be
            for (int dy = -ring; dy <= ring; dy++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                int ox = r.X + dx - fw / 2, oy = r.Y + dy - fh / 2;
                if (!Fits(ox, oy, fw, fh, type)) continue;
                for (int y = oy; y < oy + fh; y++)
                for (int x = ox; x < ox + fw; x++)
                    _w.Objects.RemoveAtCell(x, y);
                int id = _w.Objects.Place(type, ox, oy, 0);
                if (id < 0) continue;
                r.ObjectId = id;
                r.X = ox + fw / 2;
                r.Y = oy + fh / 2;
                return;
            }
        }

        bool Fits(int ox, int oy, int fw, int fh, ObjectType type)
        {
            for (int y = oy; y < oy + fh; y++)
            for (int x = ox; x < ox + fw; x++)
            {
                if (!_w.InBounds(x, y)) return false;
                int i = _w.Idx(x, y);
                if (_w.Owner[i] != 0 || (_w.Zone[i] & ZoneFlags.Wall) != 0) return false;
                if (!ObjectInfo.CanStandOn(type, _w.Terrain[i])) return false;
                int obj = _w.Objects.CellObject[i];
                if (obj >= 0 && ObjectInfo.IsBuilding(_w.Objects.Get(obj).Type)) return false;
            }
            return true;
        }

        // Plundered: the seal broken, the doors open, nothing left inside.
        void Emptied(Relic r)
        {
            if (r.ObjectId >= 0) _w.Objects.SetVariant(r.ObjectId, 1);
        }

        void OnObjectRemoved(int id, WorldObject o)
        {
            if (!ObjectInfo.IsRelic(o.Type)) return;
            foreach (var r in All)
                if (r.ObjectId == id) { r.ObjectId = -1; return; }
        }

        // Cổ mộ and thượng cổ di tích: older than any sect, deep in the wilds, sealed and strong. A world starts
        // with a few; finding them is the work of luck.
        void SeedAncient()
        {
            var rng = new DetRandom(_w.Seed ^ 0xA9C1E9u);
            var lore = _w.Lore;
            int tombs = Mathf.Min(lore.AncientTombs.Length, rng.Range(3, 5));
            int ruins = Mathf.Min(lore.AncientRuins.Length, rng.Range(3, 5));
            for (int k = 0; k < tombs + ruins; k++)
            {
                bool tomb = k < tombs;
                if (!WildSite(tomb, ref rng, out int x, out int y)) continue;
                int tier = rng.Range(3, 6);
                var r = Add(tomb ? RelicKind.Tomb : RelicKind.Ancient, tomb ? lore.AncientTombs[k] : lore.AncientRuins[k - tombs], x, y, tier, 0);
                if (r == null) continue;
                r.Origin = tomb ? "mộ phần của cổ tu sĩ thời thượng cổ" : "di tích từ thời thượng cổ, trước cả các tông môn";
                r.Treasure = _w.Lore.Treasures[rng.Range(0, _w.Lore.Treasures.Length)];
                r.Stones = rng.Range(600f, 1800f) * tier / 3f;
                r.Pills = rng.Range(1, 5);
                r.Layers = 3;
                r.Technique = AncientMethod(tier, tomb && Hash.Float01(_w.Seed ^ 0xA9C1Eu, k, 3) < 0.33f, 0); // a cổ mộ may hold a ma công
            }
        }

        // A thượng cổ method, lost since before the sects: of the relic's tier (trung phẩm to cực phẩm).
        int AncientMethod(int tier, bool demonic, long tick)
        {
            if (_sim.Techniques == null) return -1;
            var t = _sim.Techniques.New(Mathf.Clamp(tier, 3, 5), -2, demonic, -1, tick);
            t.Ancient = true;
            t.Lost = true;
            return t.Index;
        }

        // Far from every village, on ground a building can stand on: tombs in the hills, ruins wherever.
        bool WildSite(bool tomb, ref DetRandom rng, out int x, out int y)
        {
            for (int attempt = 0; attempt < 300; attempt++)
            {
                x = rng.Range(24, _w.W - 24);
                y = rng.Range(24, _w.H - 24);
                var t = _w.Terrain[_w.Idx(x, y)];
                if (!ObjectInfo.CanStandOn(ObjectType.RelicAncient, t)) continue;
                if (tomb && t != Terrain.Hills && attempt < 200) continue;
                bool alone = true;
                foreach (var s in _sim.Settlements.All)
                    if (s.Alive && (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y) < 50 * 50) { alone = false; break; }
                foreach (var r in All)
                    if ((r.X - x) * (r.X - x) + (r.Y - y) * (r.Y - y) < 80 * 80) { alone = false; break; }
                if (alone) return true;
            }
            x = y = 0;
            return false;
        }

        DetRandom RngFor(long tick, int salt) => new DetRandom(Hash.U32(_w.Seed ^ 0x2E11Cu, (int)tick, salt));

        static int Year(long tick) => (int)(tick / SimClock.DaysPerYear) + 1;

        int OpenCount()
        {
            int n = 0;
            foreach (var r in All)
                if (r.Open) n++;
            return n;
        }

        Relic Add(RelicKind kind, string name, float x, float y, int tier, long tick, bool divine = false)
        {
            if ((!divine && OpenCount() >= MaxOpen) || !_w.InBounds((int)x, (int)y)) return null; // what Thiên Đạo sets down always stands
            var r = new Relic { Index = All.Count, Kind = kind, Name = name, X = (int)x, Y = (int)y, Tier = Mathf.Clamp(tier, 1, 5), Tick = tick };
            All.Add(r);
            PlaceArt(r);
            return r;
        }

        // ---------------------------------------------------------------- how relics come to be

        // A cultivator of Kết Đan and above died: their cave keeps what their killer did not take.
        public void OnDeath(Cultivator c, long tick)
        {
            if (c.Realm < Realm.KetDan) return;
            var rng = RngFor(tick, c.Index);
            float chance = c.Realm >= Realm.HoaThan ? 1f : c.Realm == Realm.NguyenAnh ? 0.7f : 0.35f;
            if (rng.NextFloat() >= chance) return;
            var r = Add(RelicKind.Cave, $"Động phủ của {c.Name}", c.HomeX, c.HomeY, (int)c.Realm, tick);
            if (r == null) return;
            r.Owner = c.Index;
            r.Sect = c.SectId;
            r.Origin = $"{Realms.Names[(int)c.Realm]}, vẫn lạc năm {Year(tick)}";
            r.Treasure = c.Treasures > 0 ? c.TreasureName : null;
            r.Stones = c.Stones * 0.8f + 50f * (int)c.Realm;
            r.Pills = c.Pills;
            r.Layers = (int)c.Realm >= (int)Realm.NguyenAnh ? 2 : 1;
            if (c.Technique > 0) r.Technique = c.Technique; // their method, written down for whoever comes after
        }

        // A sect was wiped out: its scripture hall lies in ruins on the old mountain gate.
        public void OnSectDestroyed(Settlement s, string name, long tick)
        {
            var rng = RngFor(tick, 100000 + s.Id);
            if (rng.NextFloat() >= 0.6f) return;
            var r = Add(RelicKind.Ruins, $"Di tích {name}", s.X + rng.Range(-6, 7), s.Y + rng.Range(-6, 7), 3, tick);
            if (r == null) return;
            r.Sect = s.Id;
            r.Origin = $"tông môn bị diệt năm {Year(tick)}";
            r.Technique = _sim.Techniques?.OfSect(s.Id)?.Index ?? -1; // the scripture hall: its trấn phái công pháp lies in the ruins
            r.Treasure = rng.NextFloat() < 0.5f ? _w.Lore.Treasures[rng.Range(0, _w.Lore.Treasures.Length)] : null;
            r.Stones = rng.Range(200f, 800f);
            r.Pills = rng.Range(0, 3);
            r.Layers = 2;
        }

        // The land's own wonders, deep under a new volcano or a lôi địa.
        public void OnLandmark(Landmark l, long tick)
        {
            var rng = RngFor(tick, 200000 + l.X * 31 + l.Y);
            if (rng.NextFloat() >= 0.5f) return;
            string what = _w.Lore.NaturalTreasures[rng.Range(0, _w.Lore.NaturalTreasures.Length)];
            var r = Add(RelicKind.Treasure, $"{what} ở {l.Name}", l.X + rng.Range(-l.R, l.R + 1), l.Y + rng.Range(-l.R, l.R + 1), 2 + rng.Range(0, 3), tick);
            if (r == null) return;
            r.Origin = l.Origin;
            r.Treasure = what;
            r.Stones = rng.Range(100f, 400f);
        }

        // ---------------------------------------------------------------- finding and exploring

        public void MonthlyStep(long tick)
        {
            ResolveContests(tick);
            Discover(tick);
        }

        void Discover(long tick)
        {
            var e = _sim.Entities;
            // By index over a fixed count: an explorer who dies leaves a new bí cảnh behind (All grows mid-loop).
            for (int k = 0, n = All.Count; k < n; k++)
            {
                var r = All[k];
                if (r.Discovered || !r.Open) continue;
                int id = _sim.Creatures.FindNearest(r.X + 0.5f, r.Y + 0.5f, FindReach, 1 << (int)Species.Cultivator);
                var c = _sim.Cultivation.ForEntity(id);
                if (c == null || !_sim.Cultivation.IsShownOnMap(c) || c.AtWar) continue;
                var rng = RngFor(tick, 300000 + r.Index);
                // Fortune and insight find what the eyes pass over; the older and grander, the better hidden.
                float chance = 0.12f * (0.5f + c.Luck) * (0.7f + 0.6f * c.Comprehension) / r.Tier;
                if (_sim.Techniques != null && _sim.Techniques.Capped(c)) chance *= 2.5f; // stuck at their method's ceiling, they search harder
                if (rng.NextFloat() >= chance) continue;
                r.Discovered = true;
                r.DiscoveredBy = c.Index;
                Reveal(r, 12f, c, tick); // known to the finder, and told at home
                _sim.Events.Add(tick, EventKind.Relic, 2, $"{c.Title} ({_sim.Cultivation.SectName(c)}) tình cờ phát hiện {r.Name} ({r.Origin}).",
                    r.X + 0.5f, r.Y + 0.5f, Fx.Blessing, c.Index, r.Owner, c.SectId, r.Sect);
                // They try it at once if they dare; otherwise word spreads and stronger people will come.
                if (DeathChance(r, c) < 0.35f) Explore(c, r, tick);
            }
        }

        public void YearlyStep(long tick)
        {
            Emerge(tick);
            // Word of a known bí cảnh draws the strong from far around. By index: Explore can add a new one.
            for (int k = 0, n = All.Count; k < n; k++)
            {
                var r = All[k];
                if (!r.Discovered || !r.Open || ContestOver(r) != null) continue;
                var rng = RngFor(tick, 400000 + r.Index);
                if (rng.NextFloat() >= 0.3f) continue;
                // A great cơ duyên is not left to one: every sect around sends someone, and they fight for it.
                if (Worthy(r) && StartContest(r, tick)) continue;
                Cultivator best = null;
                foreach (var c in _sim.Cultivation.All)
                {
                    if (!c.Alive || c.AtWar || c.HuntTarget >= 0 || !_sim.Cultivation.IsAtHome(c) || (int)c.Realm < r.Tier - 1) continue;
                    float dx = c.HomeX - r.X, dy = c.HomeY - r.Y;
                    if (dx * dx + dy * dy > 220f * 220f) continue;
                    if (!Heard(r, c)) continue;
                    if (best == null || c.Luck + c.Ambition > best.Luck + best.Ambition) best = c;
                }
                if (best == null) continue;
                _sim.Events.Add(tick, EventKind.Relic, 1, $"{best.Title} nghe tin {r.Name} xuất thế, lên đường thám hiểm.",
                    r.X + 0.5f, r.Y + 0.5f, Fx.None, best.Index, r.Owner, best.SectId, r.Sect);
                Explore(best, r, tick);
            }
        }

        // ---------------------------------------------------------------- thiên tài địa bảo xuất thế

        // Now and then the richest qi in the world condenses into a linh vật, and its light rises to the sky for
        // all to see. Where the qi is, the sects are; so they come.
        void Emerge(long tick)
        {
            var rng = RngFor(tick, 600000);
            if (rng.NextFloat() >= 0.12f * _sim.Rules[Rule.Calamities] + 0.06f) return;
            int bx = -1, by = -1;
            float best = 0f;
            for (int k = 0; k < 48; k++)
            {
                int x = rng.Range(16, _w.W - 16), y = rng.Range(16, _w.H - 16);
                int i = _w.Idx(x, y);
                if (!_w.IsWalkable(x + 0.5f, y + 0.5f) || _w.Owner[i] != 0) continue;
                float q = _w.QiCap[i];
                if (q > best) { best = q; bx = x; by = y; }
            }
            if (bx < 0 || best < 3000f) return;
            string what = _w.Lore.NaturalTreasures[rng.Range(0, _w.Lore.NaturalTreasures.Length)];
            int tier = best >= 7000f ? 4 : best >= 5000f ? 3 : 2;
            var r = Add(RelicKind.Treasure, what, bx, by, tier, tick);
            if (r == null || r.ObjectId < 0) return;
            r.Treasure = what;
            r.Origin = $"xuất thế năm {Year(tick)}";
            r.Stones = rng.Range(80f, 240f) * tier;
            r.Discovered = true;
            Reveal(r, 100f, null, tick); // its light is seen from far off
            _sim.Events.Add(tick, EventKind.Relic, 3, $"Bảo quang xung thiên: {what} xuất thế giữa chốn linh khí nồng đậm, cả thiên hạ chấn động!",
                r.X + 0.5f, r.Y + 0.5f, Fx.LightPillar);
            StartContest(r, tick);
        }

        // ---------------------------------------------------------------- set down by Thiên Đạo (devlog 26)

        // A thiên tài địa bảo where the player clicks: the richer the qi there, the higher its tier. Its light is seen
        // by all, and the sects around come to fight over it like any other.
        public Relic PlaceTreasure(int x, int y, long tick)
        {
            if (!_w.InBounds(x, y) || !_w.IsWalkable(x + 0.5f, y + 0.5f)) return null;
            var rng = RngFor(tick, 610000 + x * 1031 + y);
            float qi = _w.QiCap[_w.Idx(x, y)];
            string what = _w.Lore.NaturalTreasures[rng.Range(0, _w.Lore.NaturalTreasures.Length)];
            int tier = qi >= 7000f ? 5 : qi >= 5000f ? 4 : qi >= 3000f ? 3 : 2;
            var r = Add(RelicKind.Treasure, what, x, y, tier, tick, true);
            if (r == null) return null;
            r.Treasure = what;
            r.Origin = $"Thiên Đạo ban xuống năm {Year(tick)}";
            r.Stones = rng.Range(80f, 240f) * tier;
            r.Discovered = true;
            Reveal(r, 100f, null, tick);
            _sim.Events.Add(tick, EventKind.Relic, 3, $"Thiên Đạo giáng xuống {what}, bảo quang xung thiên, thiên hạ chấn động!", r.X + 0.5f, r.Y + 0.5f, Fx.LightPillar);
            StartContest(r, tick);
            return r;
        }

        // A bí cảnh torn open where the player clicks: an ancient site of three layers, its seal broken for all
        // to see. Treasure, linh thạch and danger inside; the sects race and fight to be first through the door.
        public Relic OpenSecretRealm(int x, int y, long tick)
        {
            if (!_w.InBounds(x, y) || !ObjectInfo.CanStandOn(ObjectType.RelicAncient, _w.Terrain[_w.Idx(x, y)])) return null;
            var rng = RngFor(tick, 620000 + x * 1031 + y);
            var lore = _w.Lore;
            int tier = rng.Range(3, 6);
            var r = Add(RelicKind.Ancient, lore.AncientRuins[rng.Range(0, lore.AncientRuins.Length)], x, y, tier, tick, true);
            if (r == null) return null;
            r.Origin = $"bí cảnh Thiên Đạo mở ra năm {Year(tick)}";
            r.Technique = AncientMethod(tier, false, tick);
            r.Treasure = lore.Treasures[rng.Range(0, lore.Treasures.Length)];
            r.Stones = rng.Range(600f, 1800f) * tier / 3f;
            r.Pills = rng.Range(1, 5);
            r.Layers = 3;
            r.Discovered = true;
            Reveal(r, 70f, null, tick);
            _sim.Events.Add(tick, EventKind.Relic, 3, $"Thiên Đạo xé mở phong ấn, bí cảnh {r.Name} hiện thế; tu sĩ bốn phương kéo đến!", r.X + 0.5f, r.Y + 0.5f, Fx.LightPillar);
            StartContest(r, tick);
            return r;
        }

        // ---------------------------------------------------------------- tranh đoạt cơ duyên

        const float ContestReach = 260f;
        const int MaxContenders = 5;

        const int PerSect = 3;

        sealed class Contest
        {
            public int Relic;
            public long Resolve;
            public readonly List<int> Who = new List<int>();
            public readonly List<int> Side = new List<int>(); // sect id, or -1 - index for a lone tán tu / ma tu
        }

        readonly List<Contest> _contests = new List<Contest>();

        public bool Worthy(Relic r) => r.Tier >= 3 || r.Kind == RelicKind.Treasure || r.Kind == RelicKind.Tomb || r.Kind == RelicKind.Ancient ||
                                       (r.Technique >= 0 && _sim.Techniques != null && _sim.Techniques.All[r.Technique].Grade >= 3); // a truyền thừa is worth a fight

        Contest ContestOver(Relic r)
        {
            foreach (var c in _contests)
                if (c.Relic == r.Index) return c;
            return null;
        }

        public bool Contested(Relic r) => ContestOver(r) != null;

        // Every sect in reach sends its strongest (up to three, Trúc Cơ and above, free to go); the boldest tán tu
        // and the boldest ma tu come alone. They fly to the site for real and the fight happens there.
        // ---------------------------------------------------------------- tin đồn (KnowledgeSystem, devlog 29)

        // Has word of r reached where c lives?
        bool Heard(Relic r, Cultivator c) => _sim.Knowledge == null || _sim.Knowledge.Knows(RumorKind.Relic, r.Index, c.HomeX, c.HomeY);

        // r is known now: around it within r0, and at the home of whoever found it. Kept in memory a century.
        void Reveal(Relic r, float r0, Cultivator by, long tick)
        {
            var k = _sim.Knowledge;
            if (k == null) return;
            k.Spread(RumorKind.Relic, r.Index, r.X + 0.5f, r.Y + 0.5f, r0, tick, 100);
            if (by != null) k.Spread(RumorKind.Relic, r.Index, by.HomeX, by.HomeY, 10f, tick, 100);
        }

        // Word of r has reached sect f. If it is being fought over, they come late; if not, and it sounds worth a
        // fight (word grows as it travels), the fight starts now that enough have heard.
        public void OnHeard(Relic r, Faction f, float exaggeration, long tick)
        {
            if (!r.Open) return;
            var contest = ContestOver(r);
            if (contest == null)
            {
                if (Worthy(r) || r.Tier * exaggeration >= 3f) StartContest(r, tick);
                return;
            }
            if (tick >= contest.Resolve - 10 || contest.Side.Contains(f.Id)) return;
            int sides = 0;
            for (int j = 0; j < contest.Side.Count; j++)
                if (j == 0 || contest.Side[j] != contest.Side[j - 1]) sides++;
            if (sides >= MaxContenders) return;
            var picked = new List<Cultivator>();
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.SectId != f.Id || c.Watched || c.AtWar || c.HuntTarget >= 0 || !_sim.Cultivation.IsAtHome(c) || c.Realm < Realm.TrucCo || CombatSystem.Wounded(c, 0.6f)) continue;
                float dx = c.HomeX - r.X, dy = c.HomeY - r.Y;
                if (dx * dx + dy * dy > ContestReach * ContestReach * 2.25f) continue; // the word drew them from farther than usual
                picked.Add(c);
            }
            if (picked.Count == 0) return;
            picked.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            var rng = RngFor(tick, 710000 + r.Index * 31 + f.Id);
            int n = Mathf.Min(PerSect, picked.Count);
            for (int k = 0; k < n; k++)
            {
                _sim.Cultivation.SendToBattle(picked[k], r.X + 0.5f + rng.Range(-3f, 3f), r.Y + 0.5f + rng.Range(-3f, 3f), tick + 150);
                contest.Who.Add(picked[k].Index);
                contest.Side.Add(f.Id);
            }
            _sim.Events.Add(tick, EventKind.Relic, 2,
                $"Tin {r.Name} truyền tới {_sim.Factions.NameOf(f.Id)}" + (exaggeration >= 1.3f ? $", bị đồn thổi gấp {exaggeration:0.0} lần" : "") +
                $"; {n} người kéo tới tranh đoạt muộn.", r.X + 0.5f, r.Y + 0.5f, Fx.None, picked[0].Index, -1, f.Id);
        }

        bool StartContest(Relic r, long tick)
        {
            if (ContestOver(r) != null) return true;
            var sects = new List<List<Cultivator>>();
            Cultivator rogue = null, devil = null;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Watched || c.AtWar || c.HuntTarget >= 0 || !_sim.Cultivation.IsAtHome(c) || c.Realm < Realm.TrucCo || CombatSystem.Wounded(c, 0.6f)) continue;
                float dx = c.HomeX - r.X, dy = c.HomeY - r.Y;
                if (dx * dx + dy * dy > ContestReach * ContestReach) continue;
                if (!Heard(r, c)) continue; // only those the word has reached
                if (c.SectId < 0)
                {
                    if (c.Demonic) { if (devil == null || c.Rank > devil.Rank) devil = c; }
                    else if (rogue == null || c.Rank > rogue.Rank) rogue = c;
                    continue;
                }
                int k = sects.FindIndex(p => p[0].SectId == c.SectId);
                if (k < 0) sects.Add(new List<Cultivator> { c });
                else sects[k].Add(c);
            }
            foreach (var side in sects)
            {
                side.Sort((a, b) => b.Rank.CompareTo(a.Rank));
                if (side.Count > PerSect) side.RemoveRange(PerSect, side.Count - PerSect);
            }
            if (rogue != null) sects.Add(new List<Cultivator> { rogue });
            if (devil != null) sects.Add(new List<Cultivator> { devil });
            if (sects.Count < 2) return false;
            sects.Sort((a, b) => b[0].Rank.CompareTo(a[0].Rank));
            if (sects.Count > MaxContenders) sects.RemoveRange(MaxContenders, sects.Count - MaxContenders);

            var contest = new Contest { Relic = r.Index, Resolve = tick + 60 }; // two months: those who hear late can still come
            var rng = RngFor(tick, 700000 + r.Index);
            var names = new List<string>();
            foreach (var side in sects)
            {
                int key = side[0].SectId >= 0 ? side[0].SectId : -1 - side[0].Index;
                foreach (var c in side)
                {
                    _sim.Cultivation.SendToBattle(c, r.X + 0.5f + rng.Range(-3f, 3f), r.Y + 0.5f + rng.Range(-3f, 3f), tick + 150);
                    contest.Who.Add(c.Index);
                    contest.Side.Add(key);
                }
                var head = side[0];
                names.Add(head.SectId >= 0 ? $"{_sim.Cultivation.SectName(head)} ({side.Count} người)" : $"{(head.Demonic ? "ma tu" : "tán tu")} {head.Name}");
            }
            _contests.Add(contest);
            r.Discovered = true;
            _sim.Events.Add(tick, EventKind.Relic, 3, $"Tin {r.Name} truyền khắp thiên hạ: {string.Join(", ", names)} kéo đến tranh đoạt cơ duyên.",
                r.X + 0.5f, r.Y + 0.5f, Fx.LightPillar);
            return true;
        }

        // A melee at the site, fought as đấu pháp: the two strongest sides send their best at each other; the loser
        // dies or flees, and a side with no one left is out. The last side standing takes the cơ duyên. Every
        // death or rout leaves the losing sect hating the winner's (and enough of that is how wars start).
        void ResolveContests(long tick)
        {
            for (int k = _contests.Count - 1; k >= 0; k--)
            {
                var contest = _contests[k];
                if (tick < contest.Resolve) continue;
                _contests.RemoveAt(k);
                var r = All[contest.Relic];
                var all = _sim.Cultivation.All;
                var sides = new List<(int key, List<Cultivator> people)>();
                for (int j = 0; j < contest.Who.Count; j++)
                {
                    var c = all[contest.Who[j]];
                    if (!c.Alive || !c.AtWar) continue; // fell on the way, or was called off
                    int s = sides.FindIndex(x => x.key == contest.Side[j]);
                    if (s < 0) sides.Add((contest.Side[j], new List<Cultivator> { c }));
                    else sides[s].people.Add(c);
                }
                if (sides.Count == 0) continue;
                if (!r.Open)
                {
                    foreach (var side in sides)
                    foreach (var c in side.people) _sim.Cultivation.ReturnHome(c);
                    continue;
                }
                foreach (var side in sides) side.people.Sort((a, b) => b.Rank.CompareTo(a.Rank));
                var rng = RngFor(tick, 800000 + r.Index);
                int duels = 0, dead = 0, startSides = sides.Count;
                while (sides.Count > 1 && duels < 16)
                {
                    sides.Sort((a, b) => b.people[0].Rank.CompareTo(a.people[0].Rank));
                    var a = sides[0].people[0];
                    var b = sides[1].people[0];
                    var winner = _sim.Combat.Duel(a, b, tick, 0.2f, $"tranh đoạt {r.Name}", ref rng);
                    var loser = winner == a ? b : a;
                    duels++;
                    if (!loser.Alive) dead++;
                    if (winner.SectId >= 0 && loser.SectId >= 0) _sim.Factions.Grievance(winner.SectId, loser.SectId, loser.Alive ? 12f : 30f);
                    int ls = winner == a ? 1 : 0;
                    sides[ls].people.RemoveAt(0); // dead, or fled home wounded (Duel sends them)
                    if (sides[ls].people.Count == 0) sides.RemoveAt(ls);
                }
                // Whoever still stands from the losing sides when the fighting stops goes home empty-handed.
                for (int s = 1; s < sides.Count; s++)
                    foreach (var c in sides[s].people) _sim.Cultivation.ReturnHome(c);
                var victors = sides[0].people;
                var champion = victors[0];
                if (startSides > 1)
                    _sim.Events.Add(tick, EventKind.Relic, 3,
                        $"Đại chiến tranh đoạt {r.Name}: {duels} trận đấu pháp, {dead} tu sĩ vẫn lạc. " +
                        $"{(champion.SectId >= 0 ? _sim.Cultivation.SectName(champion) : champion.Title)} trụ lại sau cùng, {champion.Title} tiến vào bí cảnh.",
                        r.X + 0.5f, r.Y + 0.5f, Fx.Blessing, champion.Index, -1, champion.SectId);
                Explore(champion, r, tick);
                foreach (var c in victors)
                    if (c.Alive) _sim.Cultivation.ReturnHome(c);
            }
        }

        public static float DeathChance(Relic r, Cultivator c) => Mathf.Clamp(0.08f + 0.18f * (r.Tier - (int)c.Realm), 0.02f, 0.85f);

        // The nearest known, unplundered bí cảnh within reach (nhân vật chính seek them out).
        public Relic NearestKnown(float x, float y, float reach)
        {
            Relic best = null;
            float bestD = reach * reach;
            foreach (var r in All)
            {
                if (!r.Discovered || !r.Open || !(_sim.Knowledge?.Knows(RumorKind.Relic, r.Index, x, y) ?? true)) continue; // known to whoever asks from (x, y)
                float dx = r.X - x, dy = r.Y - y, d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = r; }
            }
            return best;
        }

        public Relic At(int x, int y, float reach)
        {
            foreach (var r in All)
                if (r.Open && (r.X - x) * (r.X - x) + (r.Y - y) * (r.Y - y) <= reach * reach) return r;
            return null;
        }

        // Going in: the guardians, traps and restrictions of the dead may kill; if not, the treasure, the stones,
        // and above all the truyền thừa (their understanding of the Dao) pass to the living.
        public void Explore(Cultivator c, Relic r, long tick)
        {
            if (!c.Alive || !r.Open) return;
            var rng = RngFor(tick, 500000 + r.Index * 7 + c.Index);
            string who = $"{c.Title} ({_sim.Cultivation.SectName(c)})";
            float px = r.X + 0.5f, py = r.Y + 0.5f;
            r.Discovered = true;
            if (rng.NextFloat() < DeathChance(r, c))
            {
                _sim.Cultivation.Perish(c, tick, $"{who} vào {r.Name}, vẫn lạc giữa cấm chế của người xưa.", r.Tier >= 3 ? 2 : 1, Fx.DemonBlast);
                return;
            }
            r.Layers--;
            var gains = new List<string>();
            if (r.Treasure != null && r.Kind != RelicKind.Treasure)
            {
                c.Treasures++;
                c.TreasureName = r.Treasure;
                gains.Add($"pháp bảo {r.Treasure}");
                r.Treasure = null;
            }
            float stones = r.Stones * (r.Layers > 0 ? 0.5f : 1f);
            if (stones > 0f)
            {
                c.Stones += stones;
                r.Stones -= stones;
                gains.Add($"{stones:0} linh thạch");
            }
            if (r.Pills > 0)
            {
                c.Pills += r.Pills;
                gains.Add($"{r.Pills} viên đan");
                r.Pills = 0;
            }
            // A ngọc giản: taken up if it is the better method, and brought home to the sect if it beats the sect's own.
            if (r.Technique >= 0 && _sim.Techniques != null)
            {
                var t = _sim.Techniques.All[r.Technique];
                bool wasLost = t.Lost;
                bool learned = _sim.Techniques.Learn(c, t, tick, out bool fell);
                bool offered = _sim.Techniques.Offer(c, t, tick);
                if (learned || offered)
                {
                    gains.Add($"ngọc giản {t.Name} ({t.GradeText}, {t.ElementText})" + (wasLost ? ", công pháp thất truyền nay tái hiện nhân gian" : "") +
                              (offered ? ", dâng về làm trấn phái công pháp" : "") + (fell ? ", từ đó sa vào ma đạo" : ""));
                    r.Technique = -1;
                }
            }
            // Truyền thừa: the Dao of the one who lived here; a natural wonder feeds the body instead.
            float insight = r.Kind == RelicKind.Treasure ? 0.6f : 0.4f * r.Tier;
            c.Progress += Realms.Need(c.Realm, c.Stage) * insight;
            // What the sect gets: a tithe of the stones, the truyền thừa copied into its library, and a linh vật
            // either refined by the one who found it (the ambitious, and the sect master) or offered to the sect.
            var sect = c.SectId >= 0 ? _sim.Factions.Get(c.SectId) : null;
            if (sect != null && !sect.Alive) sect = null;
            if (sect != null && stones >= 10f)
            {
                float tithe = stones * 0.4f;
                c.Stones -= tithe;
                sect.Treasury += tithe;
                gains.Add($"nộp {tithe:0} linh thạch vào kho tông môn");
            }
            if (r.Kind == RelicKind.Treasure)
            {
                bool keeps = sect == null || c.Ambition > 0.6f || _sim.Cultivation.MasterOf(c.SectId) == c;
                if (keeps)
                {
                    gains.Add($"luyện hóa {r.Treasure}");
                    c.BonusYears += 10 * r.Tier;
                }
                else
                {
                    Enshrine(_sim.Settlements.All[c.SectId], r.Tier);
                    gains.Add($"dâng {r.Treasure} về làm trấn phái linh vật, linh khí sơn môn dồi dào hẳn lên");
                }
            }
            else
            {
                c.Comprehension = Mathf.Min(1f, c.Comprehension + 0.05f * r.Tier);
                gains.Add("truyền thừa của người xưa");
                if (sect != null)
                {
                    float lift = 0.01f * r.Tier;
                    foreach (var m in _sim.Cultivation.All)
                        if (m.Alive && m.SectId == c.SectId && m != c) m.Comprehension = Mathf.Min(1f, m.Comprehension + lift);
                    gains.Add("chép truyền thừa vào Tàng Kinh Các, ngộ tính đệ tử cả tông đều tăng");
                }
            }
            var owner = r.Owner >= 0 ? _sim.Cultivation.All[r.Owner] : null;
            bool heir = owner != null && IsHeir(c, owner);
            _sim.Events.Add(tick, EventKind.Relic, r.Tier >= 3 || r.Treasure != null ? 3 : 2,
                $"{who} {(heir ? "trở về" : "thám hiểm")} {r.Name}{(owner != null ? $" ({r.Origin})" : "")}, thu được {string.Join(", ", gains)}." +
                (r.Open ? "" : " Bí cảnh từ đây trống rỗng."), px, py, Fx.Blessing, c.Index, r.Owner, c.SectId, r.Sect);
            if (!r.Open) Emptied(r);
            _sim.Stories?.OnRelic(c, r, owner, heir, tick);
        }

        // Trấn phái linh vật: set in the sect's hall, it lifts the ground's qi for good (QiBase, not a passing gust),
        // so the sect's disciples cultivate faster ever after; and the place becomes worth taking.
        void Enshrine(Settlement seat, int tier)
        {
            const int Reach = 12;
            float lift = 450f * tier;
            for (int y = seat.Y - Reach; y <= seat.Y + Reach; y++)
            for (int x = seat.X - Reach; x <= seat.X + Reach; x++)
            {
                if (!_w.InBounds(x, y)) continue;
                float d = Mathf.Sqrt((x - seat.X) * (x - seat.X) + (y - seat.Y) * (y - seat.Y));
                if (d > Reach) continue;
                int i = _w.Idx(x, y);
                _w.QiBase[i] = (ushort)Mathf.Min(WorldData.MaxQi, _w.QiBase[i] + lift * (1f - d / Reach));
            }
            int x0 = seat.X - Reach, y0 = seat.Y - Reach, x1 = seat.X + Reach, y1 = seat.Y + Reach;
            QiCap.Recompute(_w, x0, y0, x1, y1);
            _sim.Qi.RebuildCapBlocks(x0, y0, x1, y1);
            _w.NotifyQiCapChanged(x0, y0, x1, y1);
        }

        // Lineage: the dead was their master, their master's master, or of their sect.
        bool IsHeir(Cultivator c, Cultivator owner)
        {
            var all = _sim.Cultivation.All;
            int m = c.MasterIdx;
            for (int depth = 0; m >= 0 && depth < 6; depth++, m = all[m].MasterIdx)
                if (m == owner.Index) return true;
            return false;
        }

        public void HashInto(ref ulong h)
        {
            foreach (var r in All)
                StateHash.Add(ref h, r.X | ((long)r.Y << 12) | ((long)r.Layers << 24) | (r.Discovered ? 1L << 30 : 0) | ((long)r.Tier << 32) ^ ((long)System.BitConverter.SingleToInt32Bits(r.Stones) << 36));
        }
    }
}
