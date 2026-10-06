using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using ThienDao.Core;
using ThienDao.Sim;
using ThienDao.World;
using UnityEngine;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao.Render
{
    public enum OverlayMode { None, Qi, Height, Temperature, Moisture, Forage, Territory, Calamity }

    public sealed class WorldRenderer : MonoBehaviour
    {
        public const int CellPx = 8;
        public const int ChunkCells = 32;
        public const int ChunkPx = CellPx * ChunkCells;
        const int ChunkShift = 8; // log2(ChunkPx)

        [Tooltip("Below this many screen pixels per cell, only the 1-texel-per-cell overview is drawn.")]
        public float OverviewBelowPixelsPerCell = 2.5f;
        public int MaxResidentChunks = 420;
        public float RedrawBudgetMs = 6f;

        sealed class Chunk
        {
            public Texture2D Tex;
            public Sprite Sprite;
            public SpriteRenderer Renderer;
            public bool Dirty = true;
            public int LastSeen = -1;
            public readonly List<int> Objects = new List<int>();
        }

        WorldData _world;
        QiSystem _qi;
        FactionSystem _factions;
        ForageSystem _forage;
        DisasterSystem _disasters;
        Color _tint = Color.white;
        int _chunksX, _chunksY;
        Chunk[] _chunks;
        Color32[] _cellColor;
        readonly Color32[] _buffer = new Color32[ChunkPx * ChunkPx];
        readonly List<int> _sortTmp = new List<int>();
        readonly List<int> _dirtyVisible = new List<int>();
        Comparison<int> _drawOrder;

        Transform _chunkRoot;
        Material _material;
        Texture2D _overviewTex;
        Sprite _overviewSprite;
        Color32[] _overviewPx;
        SpriteRenderer _overviewRenderer;
        bool _overviewDirty;
        float _overviewApplyTimer;

        Texture2D _overlayTex;
        Sprite _overlaySprite;
        SpriteRenderer _overlayRenderer;
        bool _overlayDirty;
        float _overlayTimer;

        public OverlayMode Overlay { get; private set; }
        public bool OverviewMode { get; private set; }
        public int RedrawsLastFrame { get; private set; }
        public int ResidentChunks { get; private set; }
        public int ChunkCount => _chunks?.Length ?? 0;

        public void Init(WorldData world, Simulation sim)
        {
            Clear();
            _world = world;
            _qi = sim.Qi;
            _forage = sim.Forage;
            _disasters = sim.Disasters;
            _factions = sim.Factions;
            _factions.TerritoryChanged += HandleTerritoryChanged;
            _world.Objects.Added += OnObjectAdded;
            _world.Objects.Removed += OnObjectRemoved;
            _world.TerrainChanged += HandleTerrainChanged;
            _world.QiCapChanged += HandleQiCapChanged;
            _qi.Changed += HandleQiChanged;

            if (_material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                _material = new Material(shader);
            }

            _chunksX = world.W / ChunkCells;
            _chunksY = world.H / ChunkCells;
            _chunks = new Chunk[_chunksX * _chunksY];
            for (int i = 0; i < _chunks.Length; i++) _chunks[i] = new Chunk();

            _chunkRoot = new GameObject("Chunks").transform;
            _chunkRoot.SetParent(transform, false);

            _drawOrder = (a, b) =>
            {
                ref var oa = ref _world.Objects.Get(a);
                ref var ob = ref _world.Objects.Get(b);
                int c = ob.Y.CompareTo(oa.Y); // higher rows first so nearer objects overlap them
                return c != 0 ? c : oa.X.CompareTo(ob.X);
            };

            _cellColor = new Color32[world.W * world.H];
            for (int y = 0; y < world.H; y++)
            for (int x = 0; x < world.W; x++)
                _cellColor[world.Idx(x, y)] = ComputeCellColor(x, y);

            var objects = world.Objects;
            for (int id = 0; id < objects.Capacity; id++)
                if (objects.IsAlive(id)) LinkObject(id, objects.Get(id), true);

            BuildOverview();
            SetOverlay(Overlay);
        }

        void Clear()
        {
            if (_world != null)
            {
                _world.Objects.Added -= OnObjectAdded;
                _world.Objects.Removed -= OnObjectRemoved;
                _world.TerrainChanged -= HandleTerrainChanged;
                _world.QiCapChanged -= HandleQiCapChanged;
            }
            if (_qi != null) _qi.Changed -= HandleQiChanged;
            if (_factions != null) _factions.TerritoryChanged -= HandleTerritoryChanged;
            if (_chunks != null)
                foreach (var ch in _chunks) ReleaseTexture(ch);
            if (_chunkRoot != null) Destroy(_chunkRoot.gameObject);
            DestroyAsset(ref _overviewSprite);
            DestroyAsset(ref _overviewTex);
            DestroyAsset(ref _overlaySprite);
            DestroyAsset(ref _overlayTex);
            ResidentChunks = 0;
        }

        void OnDestroy()
        {
            Clear();
            if (_material != null) Destroy(_material);
        }

        static void DestroyAsset<T>(ref T obj) where T : UnityEngine.Object
        {
            if (obj != null) Destroy(obj);
            obj = null;
        }

        // ---------------------------------------------------------------- per-frame

        public void Tick(Camera cam)
        {
            if (_world == null) return;
            float ppc = Screen.height / (2f * cam.orthographicSize);
            bool overview = ppc < OverviewBelowPixelsPerCell;
            if (overview != OverviewMode)
            {
                OverviewMode = overview;
                _chunkRoot.gameObject.SetActive(!overview);
            }

            _overviewApplyTimer -= Time.unscaledDeltaTime;
            if (_overviewDirty && _overviewApplyTimer <= 0f)
            {
                _overviewTex.SetPixelData(_overviewPx, 0);
                _overviewTex.Apply(true);
                _overviewDirty = false;
                _overviewApplyTimer = 0.25f;
            }

            _overlayTimer -= Time.unscaledDeltaTime;
            // Grazing, droughts and epidemics have no change event; refresh on the timer.
            if (Overlay == OverlayMode.Forage || Overlay == OverlayMode.Calamity) _overlayDirty = true;
            if (_overlayDirty && _overlayTimer <= 0f)
            {
                RebuildOverlay();
                _overlayTimer = 0.15f;
            }

            RedrawsLastFrame = 0;
            if (overview) return;

            Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
            Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));
            int cx0 = Mathf.Clamp(Mathf.FloorToInt(bl.x / ChunkCells), 0, _chunksX - 1);
            int cy0 = Mathf.Clamp(Mathf.FloorToInt(bl.y / ChunkCells), 0, _chunksY - 1);
            int cx1 = Mathf.Clamp(Mathf.FloorToInt(tr.x / ChunkCells), 0, _chunksX - 1);
            int cy1 = Mathf.Clamp(Mathf.FloorToInt(tr.y / ChunkCells), 0, _chunksY - 1);
            int frame = Time.frameCount;

            _dirtyVisible.Clear();
            for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int ci = cy * _chunksX + cx;
                var ch = _chunks[ci];
                ch.LastSeen = frame;
                if (ch.Tex == null) AllocateTexture(ch, cx, cy);
                if (ch.Dirty) _dirtyVisible.Add(ci);
            }

            if (_dirtyVisible.Count > 0)
            {
                Vector3 center = cam.transform.position;
                float ccx = center.x / ChunkCells - 0.5f, ccy = center.y / ChunkCells - 0.5f;
                _dirtyVisible.Sort((a, b) =>
                {
                    float da = Sq(a % _chunksX - ccx) + Sq(a / _chunksX - ccy);
                    float db = Sq(b % _chunksX - ccx) + Sq(b / _chunksX - ccy);
                    return da.CompareTo(db);
                });

                var sw = Stopwatch.StartNew();
                foreach (int ci in _dirtyVisible)
                {
                    if (RedrawsLastFrame > 0 && sw.Elapsed.TotalMilliseconds > RedrawBudgetMs) break;
                    Compose(ci % _chunksX, ci / _chunksX);
                    RedrawsLastFrame++;
                }
            }

            if (ResidentChunks > MaxResidentChunks) Evict(frame);
        }

        static float Sq(float v) => v * v;

        void AllocateTexture(Chunk ch, int cx, int cy)
        {
            ch.Tex = new Texture2D(ChunkPx, ChunkPx, TextureFormat.RGBA32, true)
            {
                name = $"Chunk_{cx}_{cy}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            ch.Sprite = Sprite.Create(ch.Tex, new Rect(0, 0, ChunkPx, ChunkPx), Vector2.zero, CellPx, 0, SpriteMeshType.FullRect);
            if (ch.Renderer == null)
            {
                var go = new GameObject($"Chunk_{cx}_{cy}");
                go.transform.SetParent(_chunkRoot, false);
                go.transform.position = new Vector3(cx * ChunkCells, cy * ChunkCells, 0f);
                ch.Renderer = go.AddComponent<SpriteRenderer>();
                ch.Renderer.sharedMaterial = _material;
                ch.Renderer.sortingOrder = 0;
            }
            ch.Renderer.sprite = ch.Sprite;
            ch.Renderer.color = _tint;
            ch.Renderer.enabled = false; // stays hidden until first compose; overview shows through meanwhile
            ch.Dirty = true;
            ResidentChunks++;
        }

        void ReleaseTexture(Chunk ch)
        {
            if (ch.Tex == null) return;
            if (ch.Renderer != null)
            {
                ch.Renderer.sprite = null;
                ch.Renderer.enabled = false;
            }
            Destroy(ch.Sprite);
            Destroy(ch.Tex);
            ch.Sprite = null;
            ch.Tex = null;
            ch.Dirty = true;
            ResidentChunks--;
        }

        void Evict(int frame)
        {
            var candidates = new List<Chunk>();
            foreach (var ch in _chunks)
                if (ch.Tex != null && ch.LastSeen != frame) candidates.Add(ch);
            candidates.Sort((a, b) => a.LastSeen.CompareTo(b.LastSeen));
            int target = (int)(MaxResidentChunks * 0.85f);
            for (int i = 0; i < candidates.Count && ResidentChunks > target; i++) ReleaseTexture(candidates[i]);
        }

        // ---------------------------------------------------------------- chunk compose

        void Compose(int cx, int cy)
        {
            var ch = _chunks[cy * _chunksX + cx];
            int w = _world.W, h = _world.H;
            var terr = _world.Terrain;
            var tier = TerrainInfo.Tier;
            uint seed = _world.Seed ^ 0x5EEDu;
            int x0 = cx * ChunkCells, y0 = cy * ChunkCells;
            var foam = new Color32(236, 246, 255, 255);
            var soil = new Color32(128, 92, 56, 255);

            for (int ly = 0; ly < ChunkCells; ly++)
            {
                int y = y0 + ly;
                for (int lx = 0; lx < ChunkCells; lx++)
                {
                    int x = x0 + lx;
                    int i = y * w + x;
                    Terrain t = terr[i];
                    int tr = tier[(int)t];
                    bool water = TerrainInfo.IsWater(t);
                    Terrain tb = y > 0 ? terr[i - w] : t;
                    Terrain ta = y < h - 1 ? terr[i + w] : t;
                    Terrain tl = x > 0 ? terr[i - 1] : t;
                    Terrain trr = x < w - 1 ? terr[i + 1] : t;
                    int tierB = tier[(int)tb], tierA = tier[(int)ta], tierL = tier[(int)tl], tierR = tier[(int)trr];
                    bool foamB = water && TerrainInfo.IsLand(tb);
                    bool foamA = water && TerrainInfo.IsLand(ta);
                    bool foamL = water && TerrainInfo.IsLand(tl);
                    bool foamR = water && TerrainInfo.IsLand(trr);
                    bool veg = TerrainInfo.IsVegetated(t);
                    bool farm = t == Terrain.Farmland;
                    float amp = TerrainInfo.PixelNoiseAmp(t) * 2f;
                    Color32 baseC = _cellColor[i];
                    int rowBase = ly * CellPx * ChunkPx + lx * CellPx;

                    for (int py = 0; py < CellPx; py++)
                    for (int px = 0; px < CellPx; px++)
                    {
                        int wx = x * CellPx + px, wy = y * CellPx + py;
                        uint n = Hash.U32(seed, wx, wy);
                        float f = 1f + ((n & 255) / 255f - 0.5f) * amp;
                        if (veg && ((n >> 8) & 15) == 0) f *= 0.86f;

                        Color32 c;
                        if (water)
                        {
                            if ((wy + (wx >> 2)) % 9 == 0 && ((n >> 8) & 3) == 0) f *= 1.07f;
                            c = SpriteLibrary.Shade(baseC, f);
                            if ((foamB && py == 0) || (foamA && py == CellPx - 1) || (foamL && px == 0) || (foamR && px == CellPx - 1))
                                c = Color32.Lerp(c, foam, 0.45f);
                        }
                        else
                        {
                            if (tierB < tr) { if (py == 0) f *= 0.66f; else if (py == 1) f *= 0.82f; }
                            if (tierA < tr && py == CellPx - 1) f *= 1.12f;
                            if (tierL < tr && px == 0) f *= 1.06f;
                            if (tierR < tr && px == CellPx - 1) f *= 0.86f;
                            // Crop rows run across cells so neighbouring fields read as one field.
                            c = farm && (wy & 3) == 0 ? SpriteLibrary.Shade(soil, f) : SpriteLibrary.Shade(baseC, f);
                        }
                        _buffer[rowBase + py * ChunkPx + px] = c;
                    }
                }
            }

            _sortTmp.Clear();
            _sortTmp.AddRange(ch.Objects);
            _sortTmp.Sort(_drawOrder);
            int chunkPx0 = x0 * CellPx, chunkPy0 = y0 * CellPx;
            foreach (int id in _sortTmp)
            {
                ref var o = ref _world.Objects.Get(id);
                var sp = SpriteLibrary.Get(o.Type, o.Variant);
                Blit(sp, o.X * CellPx + sp.OffX - chunkPx0, o.Y * CellPx + sp.OffY - chunkPy0);
            }

            ch.Tex.SetPixelData(_buffer, 0);
            ch.Tex.Apply(true);
            ch.Renderer.enabled = true;
            ch.Dirty = false;
        }

        void Blit(PixelSprite sp, int ox, int oy)
        {
            for (int sy = 0; sy < sp.H; sy++)
            {
                int ty = oy + sy;
                if ((uint)ty >= ChunkPx) continue;
                int srcRow = sy * sp.W;
                int dstRow = ty * ChunkPx;
                for (int sx = 0; sx < sp.W; sx++)
                {
                    int tx = ox + sx;
                    if ((uint)tx >= ChunkPx) continue;
                    var c = sp.Px[srcRow + sx];
                    if (c.a == 0) continue;
                    int bi = dstRow + tx;
                    _buffer[bi] = c.a == 255 ? c : SpriteLibrary.Shade(_buffer[bi], 0.7f);
                }
            }
        }

        // ---------------------------------------------------------------- change notification

        // Named Handle*, not On*: Unity reserves OnTerrainChanged as a MonoBehaviour message.
        void HandleTerrainChanged(int x0, int y0, int x1, int y1)
        {
            // Neighbours change too: their edge shading and foam depend on this cell.
            x0 = Mathf.Max(0, x0 - 1);
            y0 = Mathf.Max(0, y0 - 1);
            x1 = Mathf.Min(_world.W - 1, x1 + 1);
            y1 = Mathf.Min(_world.H - 1, y1 + 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = _world.Idx(x, y);
                _cellColor[i] = ComputeCellColor(x, y);
                _overviewPx[i] = OverviewColor(i);
            }
            _overviewDirty = true;
            MarkChunks(x0 / ChunkCells, y0 / ChunkCells, x1 / ChunkCells, y1 / ChunkCells);
            if (Overlay != OverlayMode.None) _overlayDirty = true;
        }

        void HandleQiCapChanged(int x0, int y0, int x1, int y1)
        {
            if (Overlay == OverlayMode.Qi) _overlayDirty = true;
        }

        void HandleQiChanged()
        {
            if (Overlay == OverlayMode.Qi) _overlayDirty = true;
        }

        void HandleTerritoryChanged()
        {
            if (Overlay == OverlayMode.Territory) _overlayDirty = true;
        }

        // Seasonal colour grade, multiplied onto every terrain renderer.
        public void SetTint(Color tint)
        {
            if (Mathf.Abs(tint.r - _tint.r) + Mathf.Abs(tint.g - _tint.g) + Mathf.Abs(tint.b - _tint.b) < 0.003f) return;
            _tint = tint;
            if (_chunks != null)
                foreach (var ch in _chunks)
                    if (ch.Renderer != null) ch.Renderer.color = tint;
            if (_overviewRenderer != null) _overviewRenderer.color = tint;
        }

        void MarkChunks(int cx0, int cy0, int cx1, int cy1)
        {
            cx0 = Mathf.Clamp(cx0, 0, _chunksX - 1);
            cx1 = Mathf.Clamp(cx1, 0, _chunksX - 1);
            cy0 = Mathf.Clamp(cy0, 0, _chunksY - 1);
            cy1 = Mathf.Clamp(cy1, 0, _chunksY - 1);
            for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
                _chunks[cy * _chunksX + cx].Dirty = true;
        }

        void OnObjectAdded(int id)
        {
            var o = _world.Objects.Get(id);
            LinkObject(id, o, true);
            UpdateFootprintOverview(o);
        }

        void OnObjectRemoved(int id, WorldObject o)
        {
            LinkObject(id, o, false);
            UpdateFootprintOverview(o);
        }

        // Registers the object with every chunk its sprite touches, including overhang into neighbours.
        void LinkObject(int id, in WorldObject o, bool add)
        {
            var sp = SpriteLibrary.Get(o.Type, o.Variant);
            int px0 = o.X * CellPx + sp.OffX, py0 = o.Y * CellPx + sp.OffY;
            int cx0 = Mathf.Clamp(px0 >> ChunkShift, 0, _chunksX - 1);
            int cy0 = Mathf.Clamp(py0 >> ChunkShift, 0, _chunksY - 1);
            int cx1 = Mathf.Clamp((px0 + sp.W - 1) >> ChunkShift, 0, _chunksX - 1);
            int cy1 = Mathf.Clamp((py0 + sp.H - 1) >> ChunkShift, 0, _chunksY - 1);
            for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                var ch = _chunks[cy * _chunksX + cx];
                if (add) ch.Objects.Add(id);
                else ch.Objects.Remove(id);
                ch.Dirty = true;
            }
        }

        void UpdateFootprintOverview(in WorldObject o)
        {
            int fw = ObjectInfo.FootprintW[(int)o.Type], fh = ObjectInfo.FootprintH[(int)o.Type];
            for (int y = o.Y; y < o.Y + fh; y++)
            for (int x = o.X; x < o.X + fw; x++)
                _overviewPx[_world.Idx(x, y)] = OverviewColor(_world.Idx(x, y));
            _overviewDirty = true;
        }

        // ---------------------------------------------------------------- colors

        Color32 ComputeCellColor(int x, int y)
        {
            int i = _world.Idx(x, y);
            Terrain t = _world.Terrain[i];
            float hv = _world.Height[i];
            float f;
            if (TerrainInfo.IsWater(t)) f = 0.85f + hv * 0.35f;
            else if (TerrainInfo.IsHighland(t)) f = 0.92f + (hv - 0.7f) * 0.5f;
            else f = 1.03f - (hv - 0.5f) * 0.35f;
            f *= 0.97f + (Hash.U32(_world.Seed ^ 0x99u, x, y) & 255) / 255f * 0.06f;
            if (t == Terrain.Lava) f = 0.85f + (Hash.U32(_world.Seed ^ 0x1A7Au, x, y) & 255) / 255f * 0.35f; // glowing, crusted in patches
            var c = SpriteLibrary.Shade(TerrainInfo.Colors[(int)t], f);
            // Thương lộ: packed earth where caravans have worn a road.
            if ((_world.Zone[i] & ZoneFlags.Road) != 0 && TerrainInfo.IsLand(t)) c = Color32.Lerp(c, new Color32(186, 150, 104, 255), 0.6f);
            // Lôi địa: the ground keeps a violet sheen while lôi khí lingers.
            if ((_world.Zone[i] & ZoneFlags.Thunder) != 0) c = Color32.Lerp(c, new Color32(150, 112, 230, 255), 0.3f);
            return c;
        }

        Color32 OverviewColor(int i)
        {
            var c = _cellColor[i];
            int id = _world.Objects.CellObject[i];
            if (id < 0) return c;
            ref var o = ref _world.Objects.Get(id);
            return Color32.Lerp(c, SpriteLibrary.Get(o.Type, o.Variant).MapColor, 0.65f);
        }

        void BuildOverview()
        {
            int n = _world.W * _world.H;
            _overviewPx = new Color32[n];
            for (int i = 0; i < n; i++) _overviewPx[i] = OverviewColor(i);
            _overviewTex = new Texture2D(_world.W, _world.H, TextureFormat.RGBA32, true)
            {
                name = "WorldOverview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            _overviewTex.SetPixelData(_overviewPx, 0);
            _overviewTex.Apply(true);
            _overviewSprite = Sprite.Create(_overviewTex, new Rect(0, 0, _world.W, _world.H), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            if (_overviewRenderer == null)
            {
                var go = new GameObject("Overview");
                go.transform.SetParent(transform, false);
                _overviewRenderer = go.AddComponent<SpriteRenderer>();
                _overviewRenderer.sharedMaterial = _material;
                _overviewRenderer.sortingOrder = -1;
            }
            _overviewRenderer.sprite = _overviewSprite;
            _overviewDirty = false;
        }

        // ---------------------------------------------------------------- overlay

        public void SetOverlay(OverlayMode mode)
        {
            Overlay = mode;
            if (_overlayRenderer == null)
            {
                var go = new GameObject("Overlay");
                go.transform.SetParent(transform, false);
                _overlayRenderer = go.AddComponent<SpriteRenderer>();
                _overlayRenderer.sharedMaterial = _material;
                _overlayRenderer.sortingOrder = 10;
            }
            _overlayRenderer.enabled = mode != OverlayMode.None;
            if (mode != OverlayMode.None) RebuildOverlay();
        }

        void RebuildOverlay()
        {
            _overlayDirty = false;
            if (_world == null || Overlay == OverlayMode.None) return;
            int n = _world.W * _world.H;
            if (_overlayTex == null)
            {
                _overlayTex = new Texture2D(_world.W, _world.H, TextureFormat.RGBA32, false)
                {
                    name = "WorldOverlay",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _overlaySprite = Sprite.Create(_overlayTex, new Rect(0, 0, _world.W, _world.H), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                _overlayRenderer.sprite = _overlaySprite;
            }
            if (_overlayPx == null || _overlayPx.Length != n) _overlayPx = new Color32[n];
            var px = _overlayPx;
            int w = _world.W;
            Parallel.For(0, _world.H, y =>
            {
                for (int x = 0; x < w; x++) px[y * w + x] = OverlayColor(x, y, y * w + x);
            });
            _overlayTex.SetPixelData(px, 0);
            _overlayTex.Apply(false);
        }

        Color32[] _overlayPx;

        Color32 OverlayColor(int x, int y, int i)
        {
            switch (Overlay)
            {
                case OverlayMode.Qi:
                {
                    if (_world.LeyLine[i]) return new Color32(210, 255, 255, 230);
                    float v = _qi.SampleQi(x, y) / WorldData.MaxQi;
                    Color32 c;
                    if (v < 0.5f) c = Color32.Lerp(new Color32(40, 10, 90, 255), new Color32(120, 70, 240, 255), v * 2f);
                    else if (v < 1f) c = Color32.Lerp(new Color32(120, 70, 240, 255), new Color32(130, 255, 255, 255), (v - 0.5f) * 2f);
                    else c = Color32.Lerp(new Color32(130, 255, 255, 255), new Color32(255, 255, 255, 255), v - 1f); // over-saturated after infusion
                    c.a = (byte)Mathf.Clamp(60 + v * 160f, 0, 235);
                    return c;
                }
                case OverlayMode.Height:
                {
                    byte g = (byte)(_world.Height[i] * 255f);
                    return new Color32(g, g, g, 210);
                }
                case OverlayMode.Temperature:
                {
                    float v = _world.Temperature[i] / 255f;
                    var c = Color32.Lerp(new Color32(40, 90, 255, 255), new Color32(255, 60, 30, 255), v);
                    c.a = 170;
                    return c;
                }
                case OverlayMode.Moisture:
                {
                    float v = _world.Moisture[i] / 255f;
                    var c = Color32.Lerp(new Color32(170, 110, 50, 255), new Color32(30, 120, 255, 255), v);
                    c.a = 170;
                    return c;
                }
                case OverlayMode.Forage:
                {
                    float cap = _forage.CapAt(x, y);
                    if (cap <= 0f) return default;
                    float v = _forage.At(x, y) / cap;
                    var c = Color32.Lerp(new Color32(200, 60, 40, 255), new Color32(60, 230, 70, 255), v);
                    c.a = 150;
                    return c;
                }
                case OverlayMode.Territory:
                {
                    // Sect lands in the sect's colour with a bright border; village fields and houses show through lighter.
                    const int T = FactionSystem.Tile;
                    var fs = _factions;
                    int tile = (y / T) * fs.TW + x / T;
                    int owner = fs.TileOwner[tile];
                    bool village = _world.Owner[i] != 0;
                    if (owner == 0) return village ? new Color32(235, 225, 190, 45) : default;
                    var f = fs.Get(owner - 1);
                    if (f == null) return default;
                    var c = f.Color;
                    int lx = x % T, ly = y % T;
                    bool edge = (lx == 0 && (x == 0 || fs.TileOwner[tile - 1] != owner)) ||
                                (lx == T - 1 && (x == _world.W - 1 || fs.TileOwner[tile + 1] != owner)) ||
                                (ly == 0 && (y == 0 || fs.TileOwner[tile - fs.TW] != owner)) ||
                                (ly == T - 1 && (y == _world.H - 1 || fs.TileOwner[tile + fs.TW] != owner));
                    if (edge) return new Color32((byte)(c.r * 0.7f), (byte)(c.g * 0.7f), (byte)(c.b * 0.7f), 255);
                    c.a = (byte)(village ? 190 : 140);
                    return c;
                }
                case OverlayMode.Calamity:
                {
                    // Lava, then epidemics, cold, drought and rain over the land; lôi địa shows through underneath.
                    if (_world.Terrain[i] == Terrain.Lava) return new Color32(255, 110, 30, 220);
                    int f = _disasters.ClimateAt(x, y);
                    if ((f & 8) != 0) return new Color32(120, 220, 90, 160);
                    if ((f & 4) != 0) return new Color32(235, 245, 255, 150);
                    if ((f & 1) != 0) return new Color32(240, 160, 60, 130);
                    if ((f & 2) != 0) return new Color32(80, 150, 255, 110);
                    return (_world.Zone[i] & ZoneFlags.Thunder) != 0 ? new Color32(170, 120, 255, 150) : default;
                }
                default: return default;
            }
        }
    }
}
