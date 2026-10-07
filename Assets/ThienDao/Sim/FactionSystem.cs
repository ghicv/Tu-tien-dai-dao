using System;
using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.World;
using UnityEngine;

namespace ThienDao.Sim
{
    public enum Stance : byte { Neutral, Allied, Hostile, War, Truce }

    // A power on the map. For now every faction is a sect (tông môn); its id is the seat settlement's id,
    // which is also what cultivators carry as SectId. Mortal kingdoms and clans can join the same model later.
    public sealed class Faction
    {
        public int Id;
        public bool Alive = true;
        public bool Demonic;          // ma đạo
        public long FoundedTick;
        public int ParentId = -1;     // the sect it broke away from
        public string FounderName;
        public float Treasury;        // hạ phẩm linh thạch
        public float LastIncome;
        public float Power;           // weighted by realm; Nguyên Anh dwarfs a hall of Luyện Khí
        public int Members;
        public int Tiles, LeyTiles;
        public int Wars;
        public int BattlesWon, BattlesLost, Fallen;
        public Color32 Color;
    }

    public sealed class Relation
    {
        public int A, B;              // faction ids, A < B
        public float Opinion;         // -100 … 100
        public Stance Stance;
        public long Since;
        public long TruceUntil;
        public int Battles;
        public int Contested;         // border tiles fought over this year
        public int Wars;              // wars fought between the two, ever
        public int Other(int id) => id == A ? B : A;
    }

    // An abstract clash over one territory tile; the fighters really fly there and some die there.
    public readonly struct BattleInfo
    {
        public readonly int Attacker, Defender;
        public readonly float X, Y;
        public readonly long ResolveTick;
        public BattleInfo(int a, int d, float x, float y, long t) { Attacker = a; Defender = d; X = x; Y = y; ResolveTick = t; }
    }

    // Thế lực: territory over coarse tiles, linh thạch, opinion and stances between sects, wars fought as
    // battles over tiles (linh mạch first), sects founded by strong tán tu and by elders who break away.
    public sealed class FactionSystem
    {
        public const int Tile = 16;
        public const float PillCost = 30f;
        const int MaxReachTiles = 10;
        const int MaxFactions = 24;
        const float MaxTreasury = 6000f;
        const int BattleDays = 30;

        static readonly float[] RealmWeight = { 0f, 1f, 5f, 30f, 200f, 1500f };

        sealed class Battle
        {
            public int Attacker, Defender, Tile;
            public float X, Y;
            public long ResolveTick;
            public bool Siege;
            public readonly List<Cultivator> A = new List<Cultivator>(), D = new List<Cultivator>();
        }

        readonly Simulation _sim;
        readonly WorldData _w;
        public readonly int TW, TH;
        public readonly int[] TileOwner;      // faction id + 1, 0 = unclaimed
        readonly float[] _tileQi;             // mean qi cap, 0..1 of MaxQi
        readonly float[] _tileLand;           // share of land cells
        readonly bool[] _tileLey;
        readonly bool[] _tileDirty;
        bool _anyDirty;

        public readonly List<Faction> All = new List<Faction>();
        readonly Dictionary<int, Faction> _byId = new Dictionary<int, Faction>();
        public readonly List<Relation> Relations = new List<Relation>();
        readonly Dictionary<long, Relation> _relByKey = new Dictionary<long, Relation>();
        readonly List<Battle> _battles = new List<Battle>();
        readonly List<Battle> _due = new List<Battle>();
        readonly Dictionary<long, int> _border = new Dictionary<long, int>();

        public event Action TerritoryChanged;
        public int AliveCount { get; private set; }
        public int WarCount { get; private set; }

        public FactionSystem(Simulation sim)
        {
            _sim = sim;
            _w = sim.World;
            TW = _w.W / Tile;
            TH = _w.H / Tile;
            int n = TW * TH;
            TileOwner = new int[n];
            _tileQi = new float[n];
            _tileLand = new float[n];
            _tileLey = new bool[n];
            _tileDirty = new bool[n];
            for (int t = 0; t < n; t++) RefreshTile(t);
            _w.TerrainChanged += MarkDirty;
            _w.QiCapChanged += MarkDirty;

            var rng = new DetRandom(_w.Seed ^ 0xFAC7u);
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && s.Sect) Create(s, 0, -1, null, rng.Range(150f, 400f));
            Census();
            // Starting sects already hold their lands: grow them a tile at a time, strongest first, until settled.
            for (int round = 0; round < 48; round++)
                if (!ExpandAll(0, ref rng, 1, false)) break;
            CountTiles();
            for (int i = 0; i < All.Count; i++)
            for (int j = i + 1; j < All.Count; j++)
            {
                var a = All[i];
                var b = All[j];
                if (SeatDist2(a, b) > 500 * 500) continue;
                var r = GetRelation(a.Id, b.Id, true);
                r.Opinion = AlignmentBias(a, b) * 6f + rng.Range(-20f, 20f);
                UpdateStance(r, 0, ref rng, false);
            }
        }

        // ---------------------------------------------------------------- queries

        public Faction Get(int id) => _byId.TryGetValue(id, out var f) ? f : null;

        public string NameOf(int id) => id >= 0 && id < _sim.Settlements.All.Count ? _sim.Settlements.All[id].BaseName : "?";

        public int TileOf(float x, float y) =>
            Mathf.Clamp((int)y / Tile, 0, TH - 1) * TW + Mathf.Clamp((int)x / Tile, 0, TW - 1);

        public Faction OwnerAt(float x, float y)
        {
            int o = TileOwner[TileOf(x, y)];
            return o > 0 ? Get(o - 1) : null;
        }

        public bool IsLeyTile(int tile) => _tileLey[tile];

        // The sect whose land this is (it protects the villages there and takes their gifted children).
        public Settlement ProtectorOf(int x, int y)
        {
            var f = OwnerAt(x, y);
            if (f == null || !f.Alive) return null;
            var s = _sim.Settlements.All[f.Id];
            return s.Alive && s.Sect ? s : null;
        }

        public Relation RelationOf(int a, int b) => GetRelation(a, b, false);

        public Stance StanceBetween(int a, int b) => GetRelation(a, b, false)?.Stance ?? Stance.Neutral;

        public void RelationsOf(int id, List<Relation> into)
        {
            into.Clear();
            foreach (var r in Relations)
                if (r.A == id || r.B == id) into.Add(r);
        }

        public void ActiveBattles(List<BattleInfo> into)
        {
            into.Clear();
            foreach (var b in _battles) into.Add(new BattleInfo(b.Attacker, b.Defender, b.X, b.Y, b.ResolveTick));
        }

        public bool TrySpend(int sectId, float cost)
        {
            var f = Get(sectId);
            if (f == null || !f.Alive || f.Treasury < cost) return false;
            f.Treasury -= cost;
            return true;
        }

        public static float Weight(Cultivator c) => RealmWeight[(int)c.Realm] * (1f + c.Stage * 0.15f);

        // ---------------------------------------------------------------- tiles

        void MarkDirty(int x0, int y0, int x1, int y1)
        {
            int tx0 = Mathf.Clamp(x0 / Tile, 0, TW - 1), tx1 = Mathf.Clamp(x1 / Tile, 0, TW - 1);
            int ty0 = Mathf.Clamp(y0 / Tile, 0, TH - 1), ty1 = Mathf.Clamp(y1 / Tile, 0, TH - 1);
            for (int ty = ty0; ty <= ty1; ty++)
            for (int tx = tx0; tx <= tx1; tx++)
                _tileDirty[ty * TW + tx] = true;
            _anyDirty = true;
        }

        void RefreshTile(int t)
        {
            int ox = t % TW * Tile, oy = t / TW * Tile;
            int land = 0;
            float qi = 0f;
            bool ley = false;
            for (int y = oy; y < oy + Tile; y++)
            for (int x = ox; x < ox + Tile; x++)
            {
                int i = _w.Idx(x, y);
                if (TerrainInfo.IsLand(_w.Terrain[i])) land++;
                qi += _w.QiCap[i];
                ley |= _w.LeyLine[i];
            }
            _tileLand[t] = land / (float)(Tile * Tile);
            _tileQi[t] = qi / (Tile * Tile) / WorldData.MaxQi;
            _tileLey[t] = ley;
            _tileDirty[t] = false;
        }

        void RefreshDirtyTiles()
        {
            if (!_anyDirty) return;
            _anyDirty = false;
            bool lost = false;
            for (int t = 0; t < _tileDirty.Length; t++)
            {
                if (!_tileDirty[t]) continue;
                RefreshTile(t);
                // Land sunk under the sea is nobody's territory.
                if (TileOwner[t] != 0 && _tileLand[t] < 0.15f)
                {
                    TileOwner[t] = 0;
                    lost = true;
                }
            }
            if (lost) TerritoryChanged?.Invoke();
        }

        bool Claimable(int t) => _tileLand[t] >= 0.3f;

        int SeatTile(Faction f)
        {
            var s = _sim.Settlements.All[f.Id];
            return TileOf(s.X, s.Y);
        }

        int TileDist(int a, int b) => Mathf.Max(Mathf.Abs(a % TW - b % TW), Mathf.Abs(a / TW - b / TW));

        float Score(int t, int seat) => _tileQi[t] * 3f + (_tileLey[t] ? 2f : 0f) + _tileLand[t] * 0.5f - TileDist(t, seat) * 0.12f;

        void CountTiles()
        {
            foreach (var f in All)
            {
                f.Tiles = 0;
                f.LeyTiles = 0;
            }
            for (int t = 0; t < TileOwner.Length; t++)
            {
                int o = TileOwner[t];
                if (o == 0) continue;
                var f = Get(o - 1);
                if (f == null) continue;
                f.Tiles++;
                if (_tileLey[t]) f.LeyTiles++;
            }
        }

        int DesiredTiles(Faction f) => Mathf.Clamp(2 + (int)(Mathf.Sqrt(f.Power) * 0.9f), 2, 48);

        static readonly int[] NX = { 1, -1, 0, 0 }, NY = { 0, 0, 1, -1 };

        // Each faction claims (up to `claims`) its best free frontier tile; returns whether anything changed.
        // A richer tile held by a non-ally next door is noted as contested, which sours relations.
        bool ExpandAll(long tick, ref DetRandom rng, int claims, bool contest)
        {
            bool changed = false;
            var order = new List<Faction>();
            foreach (var f in All)
                if (f.Alive) order.Add(f);
            order.Sort((a, b) => b.Power != a.Power ? b.Power.CompareTo(a.Power) : a.Id.CompareTo(b.Id));
            CountTiles();

            foreach (var f in order)
            {
                int seat = SeatTile(f);
                int own = f.Id + 1;
                if (TileOwner[seat] != own)
                {
                    int prev = TileOwner[seat];
                    TileOwner[seat] = own; // a sect always holds the ground its hall stands on
                    if (prev > 0) Contest(f.Id, prev - 1);
                    changed = true;
                    f.Tiles++;
                }
                int want = DesiredTiles(f);
                for (int c = 0; c < claims && f.Tiles < want; c++)
                {
                    int best = -1, rival = -1, rivalOwner = 0;
                    float bestScore = float.MinValue, rivalScore = float.MinValue;
                    for (int t = 0; t < TileOwner.Length; t++)
                    {
                        if (TileOwner[t] != own) continue;
                        int tx = t % TW, ty = t / TW;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = tx + NX[k], ny = ty + NY[k];
                            if (nx < 0 || ny < 0 || nx >= TW || ny >= TH) continue;
                            int nt = ny * TW + nx;
                            if (!Claimable(nt) || TileDist(nt, seat) > MaxReachTiles) continue;
                            float sc = Score(nt, seat);
                            int o = TileOwner[nt];
                            if (o == 0)
                            {
                                if (sc > bestScore) { bestScore = sc; best = nt; }
                            }
                            else if (o != own && sc > rivalScore && StanceBetween(f.Id, o - 1) != Stance.Allied)
                            {
                                rivalScore = sc;
                                rival = nt;
                                rivalOwner = o;
                            }
                        }
                    }
                    if (contest && rival >= 0 && rivalScore > bestScore + 0.4f)
                        Contest(f.Id, rivalOwner - 1, rival, tick);
                    if (best < 0) break;
                    TileOwner[best] = own;
                    f.Tiles++;
                    changed = true;
                }
                // Too weak to hold it all: the farthest, poorest land slips away.
                if (f.Tiles > want + 3)
                {
                    int worst = -1;
                    float worstScore = float.MaxValue;
                    for (int t = 0; t < TileOwner.Length; t++)
                    {
                        if (TileOwner[t] != own || t == seat) continue;
                        float sc = Score(t, seat);
                        if (sc < worstScore) { worstScore = sc; worst = t; }
                    }
                    if (worst >= 0)
                    {
                        TileOwner[worst] = 0;
                        f.Tiles--;
                        changed = true;
                    }
                }
            }
            return changed;
        }

        void Contest(int a, int b, int tile = -1, long tick = 0)
        {
            var r = GetRelation(a, b, true);
            r.Contested++;
            if (tile >= 0 && _tileLey[tile] && r.Contested == 1 && r.Stance != Stance.War)
            {
                float x = tile % TW * Tile + Tile * 0.5f, y = tile / TW * Tile + Tile * 0.5f;
                _sim.Events.Add(tick, EventKind.War, 1, $"{NameOf(a)} và {NameOf(b)} tranh đoạt linh mạch.", x, y, Fx.None, -1, -1, a, b);
            }
        }

        // ---------------------------------------------------------------- lifecycle

        Faction Create(Settlement s, long tick, int parentId, Cultivator founder, float treasury)
        {
            var f = new Faction
            {
                Id = s.Id,
                FoundedTick = tick,
                ParentId = parentId,
                FounderName = founder?.Name,
                Treasury = treasury,
                Demonic = LoreDatabase.IsDemonicSect(s.BaseName) || (founder != null && founder.Demonic)
            };
            float hue = f.Demonic ? 0.78f + Hash.Float01(0xC0102u, s.Id, 1) * 0.2f : 0.05f + Hash.Float01(0xC0102u, s.Id, 2) * 0.6f;
            f.Color = UnityEngine.Color.HSVToRGB(hue % 1f, 0.75f, 0.95f);
            All.Add(f);
            _byId[f.Id] = f;
            AliveCount++;
            TileOwner[TileOf(s.X, s.Y)] = f.Id + 1;
            return f;
        }

        // The sect is gone (abandoned, drowned, starved or destroyed): its land and its quarrels go with it.
        public void SectGone(int id, long tick)
        {
            var f = Get(id);
            if (f == null || !f.Alive) return;
            f.Alive = false;
            AliveCount--;
            for (int t = 0; t < TileOwner.Length; t++)
                if (TileOwner[t] == id + 1) TileOwner[t] = 0;
            for (int k = Relations.Count - 1; k >= 0; k--)
            {
                var r = Relations[k];
                if (r.A != id && r.B != id) continue;
                Relations.RemoveAt(k);
                _relByKey.Remove(Key(r.A, r.B));
            }
            for (int k = _battles.Count - 1; k >= 0; k--)
            {
                var b = _battles[k];
                if (b.Attacker != id && b.Defender != id) continue;
                _battles.RemoveAt(k);
                foreach (var c in b.A) _sim.Cultivation.ReturnHome(c);
                foreach (var c in b.D) _sim.Cultivation.ReturnHome(c);
            }
            RecountWars();
            TerritoryChanged?.Invoke();
        }

        // Thiên phạt on a whole sect: everyone in the mountain gate perishes, the hall burns, the land is freed and
        // the sky over it stays charged (lôi địa). Members out on the road live on as tán tu.
        public bool Annihilate(int id, long tick)
        {
            var f = Get(id);
            if (f == null || !f.Alive) return false;
            var s = _sim.Settlements.All[id];
            string name = NameOf(id);
            int dead = 0;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.SectId != id || !_sim.Cultivation.IsAtHome(c)) continue;
                _sim.Cultivation.Perish(c, tick, $"{c.Title} vẫn lạc khi thiên phạt san bằng {name}.", c.Realm >= Realm.KetDan ? 2 : 1, Fx.Lightning);
                dead++;
            }
            _sim.Events.Add(tick, EventKind.Divine, 3, $"Thiên Đạo giáng thiên phạt xuống {name}: {dead} tu sĩ vẫn lạc, sơn môn hóa tro tàn.",
                s.X + 0.5f, s.Y + 0.5f, Fx.Tribulation, -1, -1, id);
            _sim.Settlements.Strike(s.X, s.Y, tick);
            var rng = new DetRandom(Hash.U32(_w.Seed ^ 0xA77Au, (int)tick, id));
            Destroy(f, null, tick, ref rng);
            _sim.Disasters.WrathScar(s.X, s.Y, 8, tick, $"thiên phạt diệt {name} năm {tick / SimClock.DaysPerYear + 1}");
            return true;
        }

        // Diệt môn: some lesser disciples bow to the victor, the rest scatter, the hall burns.
        void Destroy(Faction loser, Faction winner, long tick, ref DetRandom rng)
        {
            var s = _sim.Settlements.All[loser.Id];
            float hx = s.X + 0.5f, hy = s.Y + 0.5f;
            int surrendered = 0;
            if (winner != null && winner.Alive)
            {
                var seat = _sim.Settlements.All[winner.Id];
                foreach (var c in _sim.Cultivation.All)
                {
                    if (!c.Alive || c.SectId != loser.Id || c.Realm > Realm.TrucCo || c.AtWar) continue;
                    if (c.Demonic != winner.Demonic || rng.NextFloat() >= 0.35f) continue;
                    _sim.Cultivation.JoinSect(c, seat, tick);
                    surrendered++;
                }
            }
            int[] lostTiles = new int[TileOwner.Length];
            int lost = 0;
            for (int t = 0; t < TileOwner.Length; t++)
                if (TileOwner[t] == loser.Id + 1) lostTiles[lost++] = t;

            _sim.Events.Add(tick, EventKind.Destruction, 3,
                $"{(winner != null ? NameOf(winner.Id) + " công phá sơn môn, " : "")}{NameOf(loser.Id)} bị diệt môn!" +
                (surrendered > 0 ? $" {surrendered} đệ tử quy hàng." : ""), hx, hy, Fx.Explosion, -1, -1, winner?.Id ?? -1, loser.Id);
            _sim.Stories?.OnFactionDestroyed(winner, loser, tick);
            _sim.Relics?.OnSectDestroyed(s, NameOf(loser.Id), tick);
            SectGone(loser.Id, tick);
            _sim.Settlements.ConvertSectToVillage(s, tick);
            // The victor takes what lies within its reach.
            if (winner != null && winner.Alive)
            {
                int wseat = SeatTile(winner);
                for (int k = 0; k < lost; k++)
                    if (TileOwner[lostTiles[k]] == 0 && TileDist(lostTiles[k], wseat) <= MaxReachTiles) TileOwner[lostTiles[k]] = winner.Id + 1;
            }
            TerritoryChanged?.Invoke();
        }

        void SyncWithSettlements(long tick)
        {
            foreach (var s in _sim.Settlements.All)
                if (s.Alive && s.Sect && Get(s.Id) == null) Create(s, tick, -1, null, 100f);
            foreach (var f in All)
            {
                if (!f.Alive) continue;
                var s = _sim.Settlements.All[f.Id];
                if (!s.Alive || !s.Sect) SectGone(f.Id, tick);
            }
        }

        void Census()
        {
            foreach (var f in All)
            {
                f.Members = 0;
                f.Power = 0f;
            }
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.SectId < 0) continue;
                var f = Get(c.SectId);
                if (f == null) continue;
                f.Members++;
                f.Power += Weight(c);
            }
        }

        // ---------------------------------------------------------------- yearly

        public void YearlyStep(long tick)
        {
            var rng = new DetRandom(Hash.U32(_w.Seed ^ 0xFAC71u, (int)tick, 0));
            RefreshDirtyTiles();
            SyncWithSettlements(tick);
            Census();
            Decline(tick);
            Income();
            Patronage(tick, ref rng);
            if (ExpandAll(tick, ref rng, 2, true)) TerritoryChanged?.Invoke();
            CountTiles();
            Diplomacy(tick, ref rng);
            Schisms(tick, ref rng);
            Foundings(tick, ref rng);
            Census();
            CountTiles();
        }

        // A sect with no cultivators left fades into an ordinary town.
        void Decline(long tick)
        {
            for (int k = 0; k < All.Count; k++)
            {
                var f = All[k];
                if (!f.Alive || f.Members > 0) continue;
                var s = _sim.Settlements.All[f.Id];
                _sim.Events.Add(tick, EventKind.Destruction, 2, $"{NameOf(f.Id)} không còn truyền nhân, tông môn suy tàn.", s.X + 0.5f, s.Y + 0.5f, Fx.None, -1, -1, f.Id);
                SectGone(f.Id, tick);
                _sim.Settlements.ConvertSectToVillage(s, tick);
            }
        }

        // Linh thạch from the sect's own linh mạch and tribute from the mortal villages it protects.
        void Income()
        {
            CountTiles();
            foreach (var f in All)
                if (f.Alive) f.LastIncome = 4f + f.LeyTiles * 6f;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive || s.Sect) continue;
                var f = OwnerAt(s.X, s.Y);
                if (f != null && f.Alive) f.LastIncome += s.Population * 0.03f;
            }
            foreach (var f in All)
            {
                if (!f.Alive) continue;
                f.LastIncome -= f.Power * 0.12f; // pills, artefacts and formations for its members
                f.Treasury = Mathf.Clamp(f.Treasury + f.LastIncome, 0f, MaxTreasury);
            }
        }

        // A rich sect buys the loyalty of a strong tán tu as khách khanh.
        void Patronage(long tick, ref DetRandom rng)
        {
            foreach (var f in All)
            {
                if (!f.Alive || f.Treasury < 800f || rng.NextFloat() >= 0.5f) continue;
                var seat = _sim.Settlements.All[f.Id];
                Cultivator best = null;
                foreach (var c in _sim.Cultivation.All)
                {
                    if (!c.Alive || c.SectId >= 0 || c.Realm < Realm.TrucCo || c.Demonic != f.Demonic || !_sim.Cultivation.IsAtHome(c)) continue;
                    float dx = c.HomeX - seat.X, dy = c.HomeY - seat.Y;
                    if (dx * dx + dy * dy > 200f * 200f) continue;
                    if (best == null || c.Rank > best.Rank) best = c;
                }
                if (best == null) continue;
                float cost = 100f + 150f * ((int)best.Realm - 1);
                if (f.Treasury < cost) continue;
                f.Treasury -= cost;
                _sim.Cultivation.JoinSect(best, seat, tick);
                _sim.Events.Add(tick, EventKind.Patronage, best.Realm >= Realm.KetDan ? 2 : 0,
                    $"{best.Title} được {NameOf(f.Id)} chiêu mộ làm khách khanh ({cost:0} linh thạch).", -1f, -1f, Fx.None, best.Index, -1, f.Id);
            }
        }

        // ---------------------------------------------------------------- diplomacy

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        Relation GetRelation(int a, int b, bool create)
        {
            if (a == b) return null;
            long key = Key(a, b);
            if (_relByKey.TryGetValue(key, out var r) || !create) return r;
            r = new Relation { A = Mathf.Min(a, b), B = Mathf.Max(a, b) };
            Relations.Add(r);
            _relByKey[key] = r;
            return r;
        }

        int SeatDist2(Faction a, Faction b)
        {
            var sa = _sim.Settlements.All[a.Id];
            var sb = _sim.Settlements.All[b.Id];
            return (sa.X - sb.X) * (sa.X - sb.X) + (sa.Y - sb.Y) * (sa.Y - sb.Y);
        }

        // Chính and ma distrust each other; ma đạo sects distrust everyone a little, each other included.
        static float AlignmentBias(Faction a, Faction b) => a.Demonic != b.Demonic ? -5f : a.Demonic ? -2f : 2f;

        void ComputeBorders()
        {
            _border.Clear();
            for (int ty = 0; ty < TH; ty++)
            for (int tx = 0; tx < TW; tx++)
            {
                int o = TileOwner[ty * TW + tx];
                if (o == 0) continue;
                if (tx + 1 < TW) AddBorder(o, TileOwner[ty * TW + tx + 1]);
                if (ty + 1 < TH) AddBorder(o, TileOwner[(ty + 1) * TW + tx]);
            }
        }

        void AddBorder(int a, int b)
        {
            if (b == 0 || b == a) return;
            long k = Key(a - 1, b - 1);
            _border.TryGetValue(k, out int n);
            _border[k] = n + 1;
        }

        void Diplomacy(long tick, ref DetRandom rng)
        {
            ComputeBorders();
            for (int i = 0; i < All.Count; i++)
            {
                var a = All[i];
                if (!a.Alive) continue;
                for (int j = i + 1; j < All.Count; j++)
                {
                    var b = All[j];
                    if (!b.Alive) continue;
                    _border.TryGetValue(Key(a.Id, b.Id), out int border);
                    int d2 = SeatDist2(a, b);
                    var r = GetRelation(a.Id, b.Id, border > 0 || d2 < 400 * 400);
                    if (r == null) continue;

                    // Opinion drifts toward what the two sects' situation makes natural; contested land, the dead
                    // and schisms push it away from there, and time slowly brings it back.
                    float target = AlignmentBias(a, b) * 6f - 1.5f * Mathf.Min(border, 15);
                    if (a.ParentId == b.Id || b.ParentId == a.Id) target -= 20f; // the bitterness of a schism lingers
                    // A sect without linh thạch covets a neighbour sitting on linh mạch.
                    if (border > 0 && ((a.Treasury < 50f && b.LeyTiles >= 3) || (b.Treasury < 50f && a.LeyTiles >= 3))) target -= 15f;
                    float common = 0f;
                    foreach (var c in All) // a common enemy, or a common threat, draws sects together
                    {
                        if (!c.Alive || c == a || c == b) continue;
                        var sa = StanceBetween(a.Id, c.Id);
                        var sb = StanceBetween(b.Id, c.Id);
                        if (sa == Stance.War && sb == Stance.War) common += 40f;
                        else if ((sa == Stance.War || sa == Stance.Hostile) && (sb == Stance.War || sb == Stance.Hostile)) common += 20f;
                    }
                    target += Mathf.Min(common, 60f);
                    r.Opinion += (target - r.Opinion) * 0.12f - 8f * r.Contested + rng.Range(-4f, 4f);
                    r.Opinion = Mathf.Clamp(r.Opinion, -100f, 100f);
                    r.Contested = 0;
                    UpdateStance(r, tick, ref rng, true, border > 0 || d2 < 260 * 260);
                }
            }
            RecountWars();
        }

        void UpdateStance(Relation r, long tick, ref DetRandom rng, bool announce, bool neighbours = true)
        {
            var a = Get(r.A);
            var b = Get(r.B);
            switch (r.Stance)
            {
                case Stance.War:
                {
                    float years = (tick - r.Since) / (float)SimClock.DaysPerYear;
                    float weak = Mathf.Min(a.Power, b.Power) / Mathf.Max(1f, Mathf.Max(a.Power, b.Power));
                    float chance = 0.1f + 0.08f * years + (weak < 0.35f ? 0.15f : 0f) + r.Battles * 0.03f;
                    if (years >= 1f && rng.NextFloat() < chance)
                    {
                        SetStance(r, Stance.Truce, tick);
                        r.TruceUntil = tick + 20L * SimClock.DaysPerYear;
                        r.Opinion = -25f;
                        if (announce)
                            _sim.Events.Add(tick, EventKind.Peace, 2, $"{NameOf(r.A)} và {NameOf(r.B)} giảng hòa sau {years:0} năm chinh chiến.", -1f, -1f, Fx.None, -1, -1, r.A, r.B);
                    }
                    return;
                }
                case Stance.Truce:
                    if (tick < r.TruceUntil) return;
                    SetStance(r, Stance.Neutral, tick);
                    break;
            }

            // With hysteresis: an alliance or a feud, once formed, takes a real change of heart to undo.
            var next = r.Opinion > 40f || (r.Stance == Stance.Allied && r.Opinion > 15f) ? Stance.Allied
                : r.Opinion < -30f || (r.Stance == Stance.Hostile && r.Opinion < -15f) ? Stance.Hostile
                : Stance.Neutral;
            if (next != r.Stance)
            {
                if (announce && next == Stance.Allied)
                    _sim.Events.Add(tick, EventKind.Alliance, 1, $"{NameOf(r.A)} và {NameOf(r.B)} kết minh.", -1f, -1f, Fx.None, -1, -1, r.A, r.B);
                SetStance(r, next, tick);
            }

            if (r.Stance == Stance.Hostile && r.Opinion < -50f && neighbours && a.Wars < 2 && b.Wars < 2 &&
                announce && rng.NextFloat() < 0.25f)
            {
                var attacker = a.Power >= b.Power ? a : b;
                var defender = attacker == a ? b : a;
                SetStance(r, Stance.War, tick);
                r.Battles = 0;
                a.Wars++;
                b.Wars++;
                var sd = _sim.Settlements.All[defender.Id];
                _sim.Events.Add(tick, EventKind.War, 3, $"{NameOf(attacker.Id)} tuyên chiến với {NameOf(defender.Id)}!", sd.X + 0.5f, sd.Y + 0.5f, Fx.None, -1, -1, attacker.Id, defender.Id);
                r.Wars++;
                _sim.Stories?.OnWarDeclared(attacker, defender, r, tick);
            }
        }

        static void SetStance(Relation r, Stance s, long tick)
        {
            r.Stance = s;
            r.Since = tick;
        }

        void RecountWars()
        {
            foreach (var f in All) f.Wars = 0;
            WarCount = 0;
            foreach (var r in Relations)
            {
                if (r.Stance != Stance.War) continue;
                WarCount++;
                var a = Get(r.A);
                var b = Get(r.B);
                if (a != null) a.Wars++;
                if (b != null) b.Wars++;
            }
        }

        // Thiên Đạo (and tests) can push two sects straight into war.
        public bool DeclareWar(int a, int b, long tick)
        {
            var fa = Get(a);
            var fb = Get(b);
            if (fa == null || fb == null || !fa.Alive || !fb.Alive || a == b) return false;
            var r = GetRelation(a, b, true);
            if (r.Stance == Stance.War) return false;
            r.Opinion = -100f;
            SetStance(r, Stance.War, tick);
            r.Battles = 0;
            RecountWars();
            var sd = _sim.Settlements.All[b];
            _sim.Events.Add(tick, EventKind.War, 3, $"{NameOf(a)} tuyên chiến với {NameOf(b)}!", sd.X + 0.5f, sd.Y + 0.5f, Fx.None, -1, -1, a, b);
            r.Wars++;
            return true;
        }

        // ---------------------------------------------------------------- battles (monthly)

        public void MonthlyStep(long tick)
        {
            var rng = new DetRandom(Hash.U32(_w.Seed ^ 0xBA771Eu, (int)tick, 0));
            // Take the due battles out first: resolving one can destroy a sect and cancel others.
            _due.Clear();
            for (int k = 0; k < _battles.Count; k++)
                if (tick >= _battles[k].ResolveTick) _due.Add(_battles[k]);
            foreach (var b in _due) _battles.Remove(b);
            foreach (var b in _due) Resolve(b, tick, ref rng);
            for (int k = 0; k < Relations.Count; k++)
            {
                var r = Relations[k];
                if (r.Stance != Stance.War || rng.NextFloat() >= 1f / 3f) continue;
                bool busy = false;
                foreach (var b in _battles)
                    if ((b.Attacker == r.A && b.Defender == r.B) || (b.Attacker == r.B && b.Defender == r.A)) busy = true;
                if (!busy) StartBattle(r, tick, ref rng);
            }
        }

        void StartBattle(Relation r, long tick, ref DetRandom rng)
        {
            var a = Get(r.A);
            var b = Get(r.B);
            if (a == null || b == null || !a.Alive || !b.Alive) return;
            var att = rng.NextFloat() < a.Power / Mathf.Max(1f, a.Power + b.Power) ? a : b;
            var def = att == a ? b : a;

            int dseat = SeatTile(def), aseat = SeatTile(att);
            int target = -1;
            float best = float.MinValue;
            bool seatOnly = true;
            for (int t = 0; t < TileOwner.Length; t++)
            {
                if (TileOwner[t] != def.Id + 1) continue;
                bool adjacent = false;
                int tx = t % TW, ty = t / TW;
                for (int k = 0; k < 4; k++)
                {
                    int nx = tx + NX[k], ny = ty + NY[k];
                    if (nx >= 0 && ny >= 0 && nx < TW && ny < TH && TileOwner[ny * TW + nx] == att.Id + 1) adjacent = true;
                }
                // Frontier tiles first, the richest (linh mạch) above all; otherwise an expedition to the nearest.
                float sc = Score(t, aseat) + (adjacent ? 10f : 0f) - (t == dseat ? 5f : 0f);
                if (t != dseat) seatOnly = false;
                if (sc > best) { best = sc; target = t; }
            }
            if (target < 0) return;
            bool siege = target == dseat;
            if (siege && !seatOnly && def.Tiles > 1 && rng.NextFloat() < 0.7f) return; // not yet ready to storm the gate

            if (!FindField(target, out float fx, out float fy)) return;
            var battle = new Battle { Attacker = att.Id, Defender = def.Id, Tile = target, X = fx, Y = fy, ResolveTick = tick + BattleDays, Siege = siege };
            PickFighters(att, battle.A, siege ? 4 : 3, siege, ref rng);
            PickFighters(def, battle.D, siege ? 4 : 3, siege, ref rng);
            if (battle.A.Count == 0) return;
            att.Treasury = Mathf.Max(0f, att.Treasury - 25f); // war is paid for in linh thạch
            def.Treasury = Mathf.Max(0f, def.Treasury - 15f);
            long hold = battle.ResolveTick + 60;
            foreach (var c in battle.A) _sim.Cultivation.SendToBattle(c, fx - 1.5f + rng.Range(-1f, 1f), fy + rng.Range(-1f, 1f), hold);
            foreach (var c in battle.D) _sim.Cultivation.SendToBattle(c, fx + 1.5f + rng.Range(-1f, 1f), fy + rng.Range(-1f, 1f), hold);
            _battles.Add(battle);
            _sim.Events.Add(tick, EventKind.Battle, siege ? 2 : 0,
                siege ? $"{NameOf(att.Id)} kéo quân vây công sơn môn {NameOf(def.Id)}!" : $"{NameOf(att.Id)} xuất quân đánh {NameOf(def.Id)}.", fx, fy);
        }

        // Trúc Cơ and above who are home take the field; the tông chủ only to storm or defend the gate.
        void PickFighters(Faction f, List<Cultivator> into, int max, bool siege, ref DetRandom rng)
        {
            var master = _sim.Cultivation.MasterOf(f.Id);
            var pool = new List<Cultivator>();
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.SectId == f.Id && c.Realm >= Realm.TrucCo && _sim.Cultivation.IsAtHome(c) && (c != master || siege || rng.NextFloat() < 0.2f))
                    pool.Add(c);
            pool.Sort((x, y) => y.Rank != x.Rank ? y.Rank.CompareTo(x.Rank) : x.Index.CompareTo(y.Index));
            // Not always the very best: pick among the top few.
            while (into.Count < max && pool.Count > 0)
            {
                int k = rng.Range(0, Mathf.Min(3, pool.Count));
                into.Add(pool[k]);
                pool.RemoveAt(k);
            }
        }

        bool FindField(int tile, out float x, out float y)
        {
            int cx = tile % TW * Tile + Tile / 2, cy = tile / TW * Tile + Tile / 2;
            for (int r = 0; r < Tile / 2; r++)
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                x = cx + dx + 0.5f;
                y = cy + dy + 0.5f;
                if (_w.IsWalkable(x, y) && _w.IsWalkable(x - 2.5f, y) && _w.IsWalkable(x + 2.5f, y)) return true;
            }
            x = y = 0f;
            return false;
        }

        float SideStrength(Faction f, List<Cultivator> fighters, bool defending, bool siege, out Realm top)
        {
            float s = 0f;
            top = Realm.Mortal;
            foreach (var c in fighters)
            {
                if (!c.Alive || c.SectId != f.Id || !c.AtWar) continue;
                s += Weight(c) * (0.7f + 0.3f * c.DaoHeart) * (c.Demonic ? 1.15f : 1f);
                if (c.Realm > top) top = c.Realm;
            }
            // Luyện Khí disciples fill the ranks; at home they also man the hộ sơn đại trận.
            int lk = 0;
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive && c.SectId == f.Id && c.Realm == Realm.LuyenKhi) lk++;
            s += lk * (defending ? 1f : 0.4f);
            if (defending) s *= siege ? 1.35f : 1.1f;
            return s;
        }

        void Resolve(Battle b, long tick, ref DetRandom rng)
        {
            var att = Get(b.Attacker);
            var def = Get(b.Defender);
            if (att == null || def == null || !att.Alive || !def.Alive || StanceBetween(b.Attacker, b.Defender) != Stance.War)
            {
                foreach (var c in b.A) _sim.Cultivation.ReturnHome(c);
                foreach (var c in b.D) _sim.Cultivation.ReturnHome(c);
                return;
            }
            string place = _tileLey[b.Tile] ? "linh mạch" : "vùng đất";

            // The battle is a set of đấu pháp: fighters pair off, strongest against strongest; the fallen and the
            // routed no longer count for their side.
            var fa = Present(att, b.A);
            var fd = Present(def, b.D);
            int dead = 0;
            bool greatLoss = false;
            for (int k = 0; k < Mathf.Min(fa.Count, fd.Count); k++)
            {
                var w = _sim.Combat.Duel(fa[k], fd[k], tick, 0.3f, $"giao chiến trong trận tranh {place}", ref rng);
                var l = w == fa[k] ? fd[k] : fa[k];
                if (l.Alive) continue;
                dead++;
                (l == fa[k] ? att : def).Fallen++;
                if (l.Realm >= Realm.KetDan) greatLoss = true;
            }

            float sa = SideStrength(att, b.A, false, b.Siege, out _) * rng.Range(0.7f, 1.3f);
            float sd = SideStrength(def, b.D, true, b.Siege, out _) * rng.Range(0.7f, 1.3f);
            bool attackerWins = sa > sd;
            var winner = attackerWins ? att : def;
            var loser = attackerWins ? def : att;
            var r = GetRelation(att.Id, def.Id, false);
            r.Battles++;
            winner.BattlesWon++;
            loser.BattlesLost++;
            r.Opinion = Mathf.Max(-100f, r.Opinion - 8f * dead);

            _sim.Events.Add(tick, EventKind.Battle, b.Siege ? 3 : greatLoss ? 2 : 1,
                $"{NameOf(winner.Id)} đánh bại {NameOf(loser.Id)} trong trận tranh {place}{(dead > 0 ? $", {dead} tu sĩ vẫn lạc" : "")}.",
                b.X, b.Y, Fx.Explosion, -1, -1, winner.Id, loser.Id);

            foreach (var c in b.A) _sim.Cultivation.ReturnHome(c);
            foreach (var c in b.D) _sim.Cultivation.ReturnHome(c);

            if (attackerWins && TileOwner[b.Tile] == def.Id + 1)
            {
                if (b.Siege || b.Tile == SeatTile(def)) Destroy(def, att, tick, ref rng);
                else
                {
                    TileOwner[b.Tile] = att.Id + 1;
                    TerritoryChanged?.Invoke();
                }
            }
            CountTiles();
        }

        // Fighters of a side who actually made it to the field, strongest first.
        static List<Cultivator> Present(Faction f, List<Cultivator> fighters)
        {
            var list = new List<Cultivator>();
            foreach (var c in fighters)
                if (c.Alive && c.SectId == f.Id && c.AtWar) list.Add(c);
            list.Sort((x, y) => y.Rank != x.Rank ? y.Rank.CompareTo(x.Rank) : x.Index.CompareTo(y.Index));
            return list;
        }

        // ---------------------------------------------------------------- new sects

        void Foundings(long tick, ref DetRandom rng)
        {
            int count = _sim.Cultivation.All.Count;
            for (int k = 0; k < count && AliveCount < MaxFactions; k++)
            {
                var c = _sim.Cultivation.All[k];
                if (!c.Alive || c.SectId >= 0 || !_sim.Cultivation.IsAtHome(c)) continue;
                float chance = c.Realm >= Realm.NguyenAnh ? 0.12f : c.Realm == Realm.KetDan ? 0.05f :
                    c.Realm == Realm.TrucCo && Realms.IsPeak(c.Realm, c.Stage) ? 0.01f : 0f;
                if (chance <= 0f || rng.NextFloat() >= chance * (0.4f + c.Ambition)) continue;
                FoundBy(c, tick, ref rng);
            }
        }

        // A nhân vật chính decides to found a sect: same rules as for anyone else.
        public Faction TryFound(Cultivator c, long tick)
        {
            if (c == null || !c.Alive || c.SectId >= 0 || AliveCount >= MaxFactions) return null;
            var rng = new DetRandom(Hash.U32(_w.Seed ^ 0xF0D5u, (int)tick, c.Index));
            return FoundBy(c, tick, ref rng);
        }

        Faction FoundBy(Cultivator c, long tick, ref DetRandom rng)
        {
            {
                if (!FindSite(c.HomeX, c.HomeY, 120f, 0f, ref rng, out int x, out int y)) return null;
                var f = Found(c, x, y, null, tick, ref rng);
                if (f == null) return null;

                // Tán tu nearby may follow a new sect master.
                int followers = 0;
                foreach (var o in _sim.Cultivation.All)
                {
                    if (!o.Alive || o.SectId >= 0 || o == c || !_sim.Cultivation.IsAtHome(o) || o.Rank >= c.Rank) continue;
                    float dx = o.HomeX - x, dy = o.HomeY - y;
                    if (dx * dx + dy * dy > 150f * 150f || o.Demonic != f.Demonic || rng.NextFloat() >= 0.35f) continue;
                    _sim.Cultivation.JoinSect(o, _sim.Settlements.All[f.Id], tick);
                    followers++;
                }
                _sim.Events.Add(tick, EventKind.Founding, c.Realm >= Realm.KetDan ? 3 : 2,
                    $"{c.Title} khai tông lập phái, sáng lập {NameOf(f.Id)}{(f.Demonic ? " (ma đạo)" : "")}" +
                    (followers > 0 ? $", {followers} tán tu theo về." : "."), x + 0.5f, y + 0.5f, Fx.LightPillar, c.Index, -1, f.Id);
                return f;
            }
        }

        void Schisms(long tick, ref DetRandom rng)
        {
            int n = All.Count;
            for (int k = 0; k < n; k++)
            {
                var f = All[k];
                if (!f.Alive || f.Members < 6) continue;
                var master = _sim.Cultivation.MasterOf(f.Id);

                // A master gone demonic may drag the whole sect onto the demonic path.
                if (master != null && master.Demonic && !f.Demonic && rng.NextFloat() < 0.2f && _sim.Rules.DemonicAllowed)
                {
                    f.Demonic = true;
                    _sim.Events.Add(tick, EventKind.Schism, 2, $"Dưới tay {master.Title}, {NameOf(f.Id)} sa vào ma đạo.", -1f, -1f, Fx.None, master.Index, -1, f.Id);
                }

                bool freshSuccession = tick - _sim.Cultivation.LastSuccession(f.Id) < 2L * SimClock.DaysPerYear;
                foreach (var c in _sim.Cultivation.All)
                {
                    if (!c.Alive || c.SectId != f.Id || c == master || c.Realm < Realm.KetDan || !_sim.Cultivation.IsAtHome(c)) continue;
                    bool misfit = c.Demonic != f.Demonic;
                    float chance = misfit ? 0.12f : 0.004f * (0.3f + c.Ambition) * (1f + f.Members / 40f);
                    if (!misfit && freshSuccession && master != null && c.Rank >= master.Rank - 12f) chance += 0.08f; // the succession was contested
                    if (AliveCount >= MaxFactions) chance *= 0.3f; // no room for a new sect: fewer leave
                    if (rng.NextFloat() >= chance) continue;
                    BreakAway(f, c, misfit, tick, ref rng);
                    break; // one schism per sect per year
                }
            }
        }

        void BreakAway(Faction from, Cultivator leader, bool misfit, long tick, ref DetRandom rng)
        {
            var home = _sim.Settlements.All[from.Id];
            Faction f = null;
            int x = -1, y = -1;
            if (AliveCount < MaxFactions && FindSite(home.X, home.Y, 250f, 40f, ref rng, out x, out y))
                f = Found(leader, x, y, from, tick, ref rng);
            if (f == null)
            {
                _sim.Cultivation.LeaveSect(leader);
                _sim.Events.Add(tick, EventKind.Schism, 2,
                    misfit ? $"{leader.Title} bị trục xuất khỏi {NameOf(from.Id)}, trở thành tán tu." : $"{leader.Title} rời bỏ {NameOf(from.Id)}, trở thành tán tu.", -1f, -1f, Fx.None, leader.Index, -1, from.Id);
                return;
            }

            int followers = 0, cap = Mathf.Max(1, from.Members * 35 / 100);
            var seat = _sim.Settlements.All[f.Id];
            var master = _sim.Cultivation.MasterOf(from.Id);
            foreach (var c in _sim.Cultivation.All)
            {
                if (followers >= cap) break;
                if (!c.Alive || c.SectId != from.Id || c == master || c.Rank >= leader.Rank || !_sim.Cultivation.IsAtHome(c)) continue;
                float p = misfit && c.Demonic == leader.Demonic ? 0.6f : 0.18f;
                if (rng.NextFloat() >= p) continue;
                _sim.Cultivation.JoinSect(c, seat, tick);
                followers++;
            }
            var r = GetRelation(from.Id, f.Id, true);
            r.Opinion = misfit ? -90f : -70f;
            SetStance(r, Stance.Hostile, tick);
            _sim.Events.Add(tick, EventKind.Schism, 3,
                $"{leader.Title} phản xuất {NameOf(from.Id)}, dẫn {followers} đệ tử lập ra {NameOf(f.Id)}{(f.Demonic ? " (ma đạo)" : "")}.",
                x + 0.5f, y + 0.5f, misfit ? Fx.DemonBlast : Fx.LightPillar, leader.Index, -1, from.Id, f.Id);
        }

        Faction Found(Cultivator leader, int x, int y, Faction parent, long tick, ref DetRandom rng)
        {
            string name = _sim.Settlements.NewSectName(leader.Demonic, ref rng);
            var s = _sim.Settlements.FoundSect(x, y, name, tick);
            if (s == null) return null;
            var f = Create(s, tick, parent?.Id ?? -1, leader, 40f + 60f * (int)leader.Realm);
            _sim.Cultivation.JoinSect(leader, s, tick);
            _sim.Cultivation.SetMaster(s.Id, leader);
            TerritoryChanged?.Invoke(); // holds its seat tile now; it grows with the yearly expansion
            return f;
        }

        // Good ground for a new sect: unclaimed land with rich qi, room for the hall, away from `minDist` of the origin.
        bool FindSite(float ox, float oy, float reach, float minDist, ref DetRandom rng, out int bx, out int by)
        {
            bx = by = -1;
            float bestQi = 2000f;
            for (int k = 0; k < 16; k++)
            {
                float fx = k == 0 && minDist <= 0f ? ox : ox + rng.Range(-reach, reach);
                float fy = k == 0 && minDist <= 0f ? oy : oy + rng.Range(-reach, reach);
                int x = (int)fx, y = (int)fy;
                if (!_w.InBounds(x, y)) continue;
                if ((fx - ox) * (fx - ox) + (fy - oy) * (fy - oy) < minDist * minDist) continue;
                if (TileOwner[TileOf(x, y)] != 0 || !_sim.Settlements.CanFoundSectAt(x, y)) continue;
                float qi = _sim.Qi.SampleQi(x, y);
                if (qi > bestQi) { bestQi = qi; bx = x; by = y; }
            }
            return bx >= 0;
        }

        // ---------------------------------------------------------------- hashing

        public void HashInto(ref ulong h)
        {
            foreach (var f in All)
            {
                StateHash.Add(ref h, f.Id | (f.Alive ? 1L << 32 : 0) | (f.Demonic ? 1L << 33 : 0));
                StateHash.Add(ref h, BitConverter.SingleToInt32Bits(f.Treasury));
            }
            for (int t = 0; t < TileOwner.Length; t++) StateHash.Add(ref h, TileOwner[t]);
            foreach (var r in Relations)
            {
                StateHash.Add(ref h, r.A | ((long)r.B << 24) | ((long)r.Stance << 48));
                StateHash.Add(ref h, BitConverter.SingleToInt32Bits(r.Opinion));
            }
            foreach (var b in _battles) StateHash.Add(ref h, b.Attacker | ((long)b.Defender << 24) | (b.ResolveTick << 40));
        }
    }
}
