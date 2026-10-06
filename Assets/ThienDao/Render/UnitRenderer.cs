using System.Collections.Generic;
using ThienDao.Sim;
using ThienDao.World;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = ThienDao.World.Terrain;
using Unit = ThienDao.Render.SpriteLibrary.Unit;

namespace ThienDao.Render
{
    // Draws every visible moving thing as one dynamic quad mesh (a single draw call).
    // Villagers strolling between houses and fields are presentation only: they are not simulation state.
    public sealed class UnitRenderer : MonoBehaviour
    {
        const int MaxQuads = 30000;
        const float HideBelowPixelsPerCell = 1.5f;
        const float VillagersFromPixelsPerCell = 3f;

        struct Villager
        {
            public float X, Y, TX, TY, Wait;
            public Unit Look;
            public Cultivator Who; // the real sect member this stand-in shows; null for mortals
        }

        struct Token
        {
            public float X, Y, TX, TY, Wait;
            public int Kind;
            public float Pinned; // real seconds left: spawned by the player, kept on screen beyond the usual cap
        }

        const int ExtraSpawnTokens = 8;   // per region and kind, on top of MaxTokensPerRegion
        const float SpawnTokenSeconds = 60f;

        // Thiên Đạo just dropped animals here: show one right where the player clicked.
        public void ShowSpawn(Species s, float x, float y)
        {
            if (_sim == null || !_sim.World.IsWalkable(x, y)) return; // the sim drops them in the sea as well
            int kind = System.Array.IndexOf(WildlifeSystem.Kinds, s);
            if (kind < 0) return;
            int region = _sim.Wildlife.RegionOf(x, y);
            if (!_tokens.TryGetValue(region, out var list))
            {
                list = new List<Token>();
                _tokens[region] = list;
            }
            int pinned = 0, oldest = -1;
            for (int k = 0; k < list.Count; k++)
            {
                if (list[k].Kind != kind || list[k].Pinned <= 0f) continue;
                pinned++;
                if (oldest < 0 || list[k].Pinned < list[oldest].Pinned) oldest = k;
            }
            var t = new Token { X = x, Y = y, TX = x, TY = y, Wait = 0.5f + (float)_rand.NextDouble(), Kind = kind, Pinned = SpawnTokenSeconds };
            if (pinned >= ExtraSpawnTokens) list[oldest] = t; // holding the brush keeps moving the newest ones under it
            else list.Add(t);
        }

        // Click target for something drawn this frame.
        public struct Hit
        {
            public Rect Box;
            public Cultivator Cultivator;
            public Settlement Settlement; // a mortal stand-in's village
            public int Entity;            // an individual entity (migrants), else -1
            public int Region, Kind;      // a wildlife token's region and kind, else -1
        }

        readonly List<Hit> _hits = new List<Hit>();

        // Decorative wildlife: one token stands for this many animals, capped per region and kind.
        static readonly float[] AnimalsPerToken = { 20f, 30f, 3f };
        static readonly int[] MaxTokensPerRegion = { 3, 3, 2 };
        static readonly Unit[] TokenLook = { Unit.Deer, Unit.Rabbit, Unit.Wolf };
        static readonly float[] TokenSpeed = { 1.2f, 1f, 1.5f }; // cells per real second
        readonly Dictionary<int, List<Token>> _tokens = new Dictionary<int, List<Token>>();
        readonly List<int> _staleRegions = new List<int>();

        Simulation _sim;
        Mesh _mesh;
        MeshRenderer _renderer;
        Material _material;
        int[] _triangles;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<Color32> _colors = new List<Color32>();
        readonly Dictionary<int, List<Villager>> _villagers = new Dictionary<int, List<Villager>>();
        readonly List<int> _staleVillages = new List<int>();
        System.Random _rand = new System.Random(1);

        public int QuadsLastFrame { get; private set; }

        public void Init(Simulation sim)
        {
            _sim = sim;
            _villagers.Clear();
            _tokens.Clear();
            _rand = new System.Random((int)sim.World.Seed);
            if (_mesh != null) return;

            _mesh = new Mesh { name = "Units", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _triangles = new int[MaxQuads * 6];
            for (int q = 0; q < MaxQuads; q++)
            {
                int v = q * 4, t = q * 6;
                _triangles[t] = v;
                _triangles[t + 1] = v + 1;
                _triangles[t + 2] = v + 2;
                _triangles[t + 3] = v;
                _triangles[t + 4] = v + 2;
                _triangles[t + 5] = v + 3;
            }
            var go = new GameObject("Units");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { mainTexture = SpriteLibrary.UnitAtlas };
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = 5;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        public void Tick(Camera cam, float pixelsPerCell)
        {
            if (_sim == null) return;
            _verts.Clear();
            _uvs.Clear();
            _colors.Clear();
            _hits.Clear();

            if (pixelsPerCell >= HideBelowPixelsPerCell)
            {
                Vector3 bl = cam.ViewportToWorldPoint(Vector3.zero), tr = cam.ViewportToWorldPoint(Vector3.one);
                var view = Rect.MinMaxRect(bl.x - 2f, bl.y - 2f, tr.x + 2f, tr.y + 2f);
                DrawCreatures(view);
                DrawWildlife(view);
                if (pixelsPerCell >= VillagersFromPixelsPerCell) DrawVillagers(view);
                else _villagers.Clear();
            }

            _mesh.Clear();
            QuadsLastFrame = _verts.Count / 4;
            if (QuadsLastFrame == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0, QuadsLastFrame * 6, 0, false);
            _mesh.bounds = new Bounds(new Vector3(512f, 512f, 0f), new Vector3(4096f, 4096f, 10f));
        }

        static Unit UnitFor(Species s)
        {
            switch (s)
            {
                case Species.Deer: return Unit.Deer;
                case Species.Rabbit: return Unit.Rabbit;
                case Species.Wolf: return Unit.Wolf;
                default: return Unit.Migrants;
            }
        }

        static readonly Color32[] AuraTint =
        {
            default, default, default,
            new Color32(255, 214, 110, 255), // Kết Đan: gold
            new Color32(200, 140, 255, 255), // Nguyên Anh: violet
            new Color32(220, 240, 255, 255), // Hóa Thần: silver
        };

        static Unit CultivatorLook(Cultivator c)
        {
            if (c.Demonic) return Unit.CultivatorDemonic;
            switch (c.Realm)
            {
                case Realm.TrucCo: return Unit.CultivatorTC;
                case Realm.KetDan: return Unit.CultivatorKD;
                case Realm.NguyenAnh: return Unit.CultivatorNA;
                case Realm.HoaThan: return Unit.CultivatorHT;
                default: return Unit.CultivatorLK;
            }
        }

        void DrawCreatures(Rect view)
        {
            var e = _sim.Entities;
            float frac = _sim.TickFraction;
            float time = Time.time;
            for (int id = 0; id < e.Count; id++)
            {
                var s = e.Species[id];
                if (s == Species.None) continue;
                float x = Mathf.Lerp(e.PrevX[id], e.X[id], frac);
                float y = Mathf.Lerp(e.PrevY[id], e.Y[id], frac);
                if (!view.Contains(new Vector2(x, y))) continue;
                bool moving = e.X[id] != e.PrevX[id] || e.Y[id] != e.PrevY[id];
                bool left = e.TX[id] < e.X[id] - 0.01f;
                int frame = moving && !_sim.Paused ? ((int)(time * 6f) + id) & 1 : 0;

                if (s != Species.Cultivator)
                {
                    AddQuad(UnitFor(s), frame, x, y, left);
                    AddHit(UnitFor(s), x, y, new Hit { Entity = id, Region = -1, Kind = -1 });
                    continue;
                }

                var c = _sim.Cultivation.ForEntity(id);
                if (c == null || !_sim.Cultivation.IsShownOnMap(c)) continue;
                bool flying = e.Flying[id] && moving;
                if (!flying && !_sim.World.IsWalkable(x, y)) continue; // the sim moves them ashore within the month
                // Hover above the ground while flying, with a gentle bob.
                float lift = flying ? 0.7f + Mathf.Sin(time * 3f + id) * 0.08f : 0f;
                if (c.Realm >= Realm.KetDan)
                {
                    var tint = c.Demonic ? new Color32(255, 70, 70, 255) : AuraTint[(int)c.Realm];
                    AddQuadCentered(Unit.Aura, ((int)(time * 2f) + id) & 1, x, y + lift + 0.7f, tint);
                }
                if (flying) AddQuadCentered(Unit.FlyingSword, ((int)(time * 8f) + id) & 1, x, y + lift - 0.1f, White, left);
                AddQuad(CultivatorLook(c), flying ? 0 : frame, x, y + lift, left);
                AddHit(CultivatorLook(c), x, y + lift, new Hit { Cultivator = c, Entity = -1, Region = -1, Kind = -1 });
            }
        }

        void DrawVillagers(Rect view)
        {
            var settlements = _sim.Settlements.All;
            var objects = _sim.World.Objects;
            float dt = _sim.Paused ? 0f : Time.deltaTime;
            float time = Time.time;

            _staleVillages.Clear();
            foreach (var key in _villagers.Keys) _staleVillages.Add(key);

            foreach (var s in settlements)
            {
                if (!s.Alive || !view.Overlaps(new Rect(s.X - 30f, s.Y - 30f, 60f, 60f))) continue;
                _staleVillages.Remove(s.Id);
                if (!_villagers.TryGetValue(s.Id, out var list))
                {
                    list = new List<Villager>();
                    _villagers[s.Id] = list;
                }
                // Wanted stand-ins: mortals for the population; at a sect also a few disciples and elders who are home.
                _wantLooks.Clear();
                _wantWho.Clear();
                int mortals = Mathf.Clamp(s.Population / 5, 1, 20);
                for (int k = 0; k < mortals; k++) Want(Unit.Villager0, null);
                if (s.Sect) AddSectStandIns(s.Id);
                SyncStandIns(list, s, objects);

                for (int k = list.Count - 1; k >= 0; k--)
                {
                    var v = list[k];
                    if (!_sim.World.IsWalkable(v.X, v.Y))
                    {
                        list.RemoveAt(k); // ground changed under them; a replacement spawns on dry land
                        continue;
                    }
                    float dx = v.TX - v.X, dy = v.TY - v.Y, d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool moving = false;
                    if (v.Wait > 0f) v.Wait -= dt;
                    else if (d < 0.05f || !PathClear(v.X, v.Y, v.TX, v.TY))
                    {
                        var p = RandomSpot(s, objects, v.Look);
                        if (PathClear(v.X, v.Y, p.x, p.y))
                        {
                            v.TX = p.x;
                            v.TY = p.y;
                        }
                        else
                        {
                            v.TX = v.X;
                            v.TY = v.Y;
                        }
                        v.Wait = 0.5f + (float)_rand.NextDouble() * 2.5f;
                    }
                    else
                    {
                        float step = Mathf.Min(d, 1.6f * dt);
                        v.X += dx / d * step;
                        v.Y += dy / d * step;
                        moving = dt > 0f;
                    }
                    list[k] = v;
                    if (!view.Contains(new Vector2(v.X, v.Y))) continue;
                    int frame = moving ? ((int)(time * 7f) + k) & 1 : 0;
                    AddQuad(v.Look, frame, v.X, v.Y, dx < 0f);
                    AddHit(v.Look, v.X, v.Y, new Hit { Cultivator = v.Who, Settlement = s, Entity = -1, Region = -1, Kind = -1 });
                }
            }
            foreach (int id in _staleVillages) _villagers.Remove(id);
        }

        readonly List<Unit> _wantLooks = new List<Unit>();
        readonly List<Cultivator> _wantWho = new List<Cultivator>();
        readonly List<Cultivator> _disciples = new List<Cultivator>();
        readonly List<Cultivator> _elders = new List<Cultivator>();

        static bool IsVillagerLook(Unit u) => u >= Unit.Villager0 && u <= Unit.Villager3;

        void Want(Unit look, Cultivator who)
        {
            _wantLooks.Add(look);
            _wantWho.Add(who);
        }

        // Each sect stand-in is a real member who is at home, so clicking it opens that person's card.
        void AddSectStandIns(int sectId)
        {
            _disciples.Clear();
            _elders.Clear();
            foreach (var c in _sim.Cultivation.All)
            {
                if (c.SectId != sectId || !_sim.Cultivation.IsAtHome(c)) continue;
                if (c.Realm == Realm.LuyenKhi) _disciples.Add(c);
                else _elders.Add(c);
            }
            int shown = Mathf.Min(6, (_disciples.Count + 2) / 3);
            for (int k = 0; k < shown; k++) Want(CultivatorLook(_disciples[k]), _disciples[k]);
            _elders.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            for (int k = 0; k < Mathf.Min(2, _elders.Count); k++) Want(CultivatorLook(_elders[k]), _elders[k]);
        }

        // Keep the stand-ins in step with what is wanted, reusing existing ones so they don't jump around.
        void SyncStandIns(List<Villager> list, Settlement s, WorldObjects objects)
        {
            for (int k = list.Count - 1; k >= 0; k--)
            {
                var v = list[k];
                int idx = v.Who == null ? _wantLooks.IndexOf(Unit.Villager0) : _wantWho.IndexOf(v.Who);
                if (idx < 0)
                {
                    list.RemoveAt(k);
                    continue;
                }
                if (v.Who != null && v.Look != _wantLooks[idx])
                {
                    v.Look = _wantLooks[idx]; // broke through or turned demonic while at home
                    list[k] = v;
                }
                _wantLooks.RemoveAt(idx);
                _wantWho.RemoveAt(idx);
            }
            for (int k = 0; k < _wantLooks.Count; k++)
            {
                var want = _wantLooks[k];
                var look = want == Unit.Villager0 ? (Unit)((int)Unit.Villager0 + _rand.Next(4)) : want;
                var p = RandomSpot(s, objects, look);
                if (!_sim.World.IsWalkable(p.x, p.y)) continue; // no dry ground found this frame
                list.Add(new Villager { X = p.x, Y = p.y, TX = p.x, TY = p.y, Wait = (float)_rand.NextDouble() * 2f, Look = look, Who = _wantWho[k] });
            }
        }

        // Decorative walkers only take straight paths that stay on land.
        bool PathClear(float x0, float y0, float x1, float y1)
        {
            float d = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Mathf.Max(1, Mathf.CeilToInt(d * 2f));
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;
                if (!_sim.World.IsWalkable(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t)) return false;
            }
            return true;
        }

        void DrawWildlife(Rect view)
        {
            var wild = _sim.Wildlife;
            const int size = WildlifeSystem.Region;
            float dt = _sim.Paused ? 0f : Time.deltaTime;
            float time = Time.time;

            _staleRegions.Clear();
            foreach (var key in _tokens.Keys) _staleRegions.Add(key);

            int rx0 = Mathf.Clamp((int)view.xMin / size, 0, wild.RW - 1), rx1 = Mathf.Clamp((int)view.xMax / size, 0, wild.RW - 1);
            int ry0 = Mathf.Clamp((int)view.yMin / size, 0, wild.RH - 1), ry1 = Mathf.Clamp((int)view.yMax / size, 0, wild.RH - 1);
            for (int ry = ry0; ry <= ry1; ry++)
            for (int rx = rx0; rx <= rx1; rx++)
            {
                int region = ry * wild.RW + rx;
                _staleRegions.Remove(region);
                if (!_tokens.TryGetValue(region, out var list))
                {
                    list = new List<Token>();
                    _tokens[region] = list;
                }

                for (int kind = 0; kind < WildlifeSystem.Kinds.Length; kind++)
                {
                    float pop = wild.At(WildlifeSystem.Kinds[kind], region);
                    int want = pop < 1f ? 0 : Mathf.Min(MaxTokensPerRegion[kind], Mathf.CeilToInt(pop / AnimalsPerToken[kind]));
                    int have = 0;
                    for (int k = list.Count - 1; k >= 0; k--)
                    {
                        if (list[k].Kind != kind) continue;
                        // Freshly spawned ones stay while their kind still lives here, even past the cap.
                        if (list[k].Pinned > 0f && pop >= 1f) { have++; continue; }
                        if (have >= want) list.RemoveAt(k);
                        else have++;
                    }
                    for (; have < want; have++)
                    {
                        if (!RandomWildSpot(rx * size, ry * size, size, out var p)) break;
                        list.Add(new Token { X = p.x, Y = p.y, TX = p.x, TY = p.y, Wait = (float)_rand.NextDouble() * 3f, Kind = kind });
                    }
                }

                for (int k = list.Count - 1; k >= 0; k--)
                {
                    var t = list[k];
                    if (!_sim.World.IsWalkable(t.X, t.Y))
                    {
                        list.RemoveAt(k);
                        continue;
                    }
                    if (t.Pinned > 0f) t.Pinned -= Time.unscaledDeltaTime;
                    float dx = t.TX - t.X, dy = t.TY - t.Y, d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool moving = false;
                    if (t.Wait > 0f) t.Wait -= dt;
                    else if (d < 0.05f || !PathClear(t.X, t.Y, t.TX, t.TY))
                    {
                        float nx = t.X + (float)(_rand.NextDouble() - 0.5) * 10f, ny = t.Y + (float)(_rand.NextDouble() - 0.5) * 10f;
                        bool inRegion = nx >= rx * size && nx < (rx + 1) * size && ny >= ry * size && ny < (ry + 1) * size;
                        if (inRegion && PathClear(t.X, t.Y, nx, ny))
                        {
                            t.TX = nx;
                            t.TY = ny;
                        }
                        else
                        {
                            t.TX = t.X;
                            t.TY = t.Y;
                        }
                        t.Wait = 1f + (float)_rand.NextDouble() * 4f;
                    }
                    else
                    {
                        float step = Mathf.Min(d, TokenSpeed[t.Kind] * dt);
                        t.X += dx / d * step;
                        t.Y += dy / d * step;
                        moving = dt > 0f;
                    }
                    list[k] = t;
                    if (!view.Contains(new Vector2(t.X, t.Y))) continue;
                    int frame = moving ? ((int)(time * 6f) + k) & 1 : 0;
                    AddQuad(TokenLook[t.Kind], frame, t.X, t.Y, dx < 0f);
                    AddHit(TokenLook[t.Kind], t.X, t.Y, new Hit { Entity = -1, Region = region, Kind = t.Kind });
                }
            }
            foreach (int region in _staleRegions) _tokens.Remove(region);
        }

        bool RandomWildSpot(int x0, int y0, int size, out Vector2 p)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float x = x0 + (float)_rand.NextDouble() * size, y = y0 + (float)_rand.NextDouble() * size;
                int i = _sim.World.Idx((int)x, (int)y);
                if (_sim.World.IsWalkable(x, y) && _sim.World.Terrain[i] != Terrain.Farmland && _sim.World.Owner[i] == 0)
                {
                    p = new Vector2(x, y);
                    return true;
                }
            }
            p = default;
            return false;
        }

        // A house doorstep or a field cell of the settlement.
        Vector2 RandomSpot(Settlement s, WorldObjects objects, Unit look)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                var p = RandomSpotOnce(s, objects, look);
                if (_sim.World.IsWalkable(p.x, p.y)) return p;
            }
            return new Vector2(-1f, -1f); // callers treat off-map as "nowhere"
        }

        Vector2 RandomSpotOnce(Settlement s, WorldObjects objects, Unit look)
        {
            // Disciples and elders keep to the sect hall grounds; mortals go between fields and houses.
            if (!IsVillagerLook(look))
                return new Vector2(s.X + 0.5f + (float)(_rand.NextDouble() - 0.5) * 8f, s.Y - 1f + (float)(_rand.NextDouble() - 0.5) * 4f);
            if (s.Farms.Count > 0 && (_rand.NextDouble() < 0.6 || s.Houses.Count == 0))
            {
                int cell = s.Farms[_rand.Next(s.Farms.Count)];
                int w = _sim.World.W;
                return new Vector2(cell % w + (float)_rand.NextDouble(), cell / w + (float)_rand.NextDouble());
            }
            if (s.Houses.Count > 0)
            {
                var h = objects.Get(s.Houses[_rand.Next(s.Houses.Count)]);
                return new Vector2(h.X + 1.5f + (float)(_rand.NextDouble() - 0.5), h.Y - 0.1f);
            }
            return new Vector2(s.X + 0.5f, s.Y + 0.5f);
        }

        static readonly Color32 White = new Color32(255, 255, 255, 255);

        // Same box as AddQuad draws (feet at (x, y)).
        void AddHit(Unit unit, float x, float y, Hit hit)
        {
            var sp = SpriteLibrary.UnitSprite(unit, 0);
            float w = sp.W / (float)WorldRenderer.CellPx, h = sp.H / (float)WorldRenderer.CellPx;
            hit.Box = new Rect(x - w * 0.5f, y - 0.15f, w, h);
            _hits.Add(hit);
        }

        // Where a cultivator (travelling or as a sect stand-in) or an entity was drawn last frame.
        public bool BoxOf(Cultivator c, int entity, out Rect box)
        {
            foreach (var hit in _hits)
            {
                if (c != null ? hit.Cultivator != c : hit.Entity != entity) continue;
                box = hit.Box;
                return true;
            }
            box = default;
            return false;
        }

        // What was drawn under (or within slop of) a world point last frame. Cultivators beat mortals and
        // migrants, which beat animals; among equals the sprite whose centre is nearest wins.
        public bool Pick(Vector2 p, float slop, out Hit result)
        {
            result = default;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int k = 0; k < _hits.Count; k++)
            {
                var hit = _hits[k];
                var b = hit.Box;
                float ox = Mathf.Max(0f, Mathf.Max(b.xMin - p.x, p.x - b.xMax));
                float oy = Mathf.Max(0f, Mathf.Max(b.yMin - p.y, p.y - b.yMax));
                if (ox > slop || oy > slop) continue;
                if (hit.Cultivator != null && !hit.Cultivator.Alive) continue;
                float rank = hit.Cultivator != null ? 0f : hit.Settlement != null || hit.Entity >= 0 ? 1000f : 2000f;
                float score = rank + (b.center - p).sqrMagnitude;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = k;
                }
            }
            if (best < 0) return false;
            result = _hits[best];
            return true;
        }

        // Feet at (x, y).
        void AddQuad(Unit unit, int frame, float x, float y, bool flip) => AddQuadAt(unit, frame, x, y, flip, White, false);

        void AddQuadCentered(Unit unit, int frame, float x, float y, Color32 color, bool flip = false) =>
            AddQuadAt(unit, frame, x, y, flip, color, true);

        void AddQuadAt(Unit unit, int frame, float x, float y, bool flip, Color32 color, bool centered)
        {
            if (_verts.Count >= MaxQuads * 4) return;
            var sp = SpriteLibrary.UnitSprite(unit, frame);
            var uv = SpriteLibrary.UnitUv(unit, frame);
            float w = sp.W / (float)WorldRenderer.CellPx, h = sp.H / (float)WorldRenderer.CellPx;
            float x0 = x - w * 0.5f, y0 = centered ? y - h * 0.5f : y - 0.15f;
            _verts.Add(new Vector3(x0, y0, 0f));
            _verts.Add(new Vector3(x0, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0, 0f));
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
            _uvs.Add(new Vector2(u0, uv.yMin));
            _uvs.Add(new Vector2(u0, uv.yMax));
            _uvs.Add(new Vector2(u1, uv.yMax));
            _uvs.Add(new Vector2(u1, uv.yMin));
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
        }
    }
}
