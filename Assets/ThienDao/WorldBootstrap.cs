using ThienDao.Player;
using ThienDao.Render;
using ThienDao.Sim;
using ThienDao.UI;
using ThienDao.World;
using UnityEngine;
using UnityEngine.InputSystem;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao
{
    public enum InspectKind { None, Cultivator, Settlement, Migrants, Animal, Object, Cell }

    // Anything the Xem tool can point at: a person, a village, an animal herd, an object or a bare cell.
    public struct InspectTarget
    {
        public InspectKind Kind;
        public Cultivator Cultivator;
        public Settlement Settlement;
        public int Entity;        // migrants
        public int Region, Animal; // wildlife region and index into WildlifeSystem.Kinds
        public int ObjectId;
        public int CellX, CellY;  // the cell under the pointer when picked (all kinds)
        public Rect? HoverBox;    // what to outline while hovering

        public bool Same(in InspectTarget o) =>
            Kind == o.Kind && Cultivator == o.Cultivator && Settlement == o.Settlement && Entity == o.Entity &&
            Region == o.Region && Animal == o.Animal && ObjectId == o.ObjectId &&
            (Kind != InspectKind.Cell || (CellX == o.CellX && CellY == o.CellY));
    }

    // Wires the world, simulation, renderers, input and UI together. All on-screen interface lives in GameUI.
    public sealed class WorldBootstrap : MonoBehaviour
    {
        public string Seed = "ThienDao";
        [Tooltip("Initial zoom, in screen pixels per cell.")]
        public float StartPixelsPerCell = 4f;

        // Colour grade at the middle of each season; lerped through the year.
        static readonly Color[] SeasonTints =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 0.985f, 0.93f),
            new Color(1f, 0.92f, 0.8f),
            new Color(0.86f, 0.9f, 1f),
        };

        const double SimBudgetMs = 8.0;
        const int OverlayCount = 7;

        WorldRenderer _renderer;
        UnitRenderer _units;
        FxRenderer _fx;
        CameraController _cam;
        SpriteRenderer _cursor;
        GameUI _ui;
        int _speedBeforePause = 1;

        public WorldData World { get; private set; }
        public Simulation Sim { get; private set; }
        public WorldBrush Brush { get; private set; }
        public WorldRenderer Renderer => _renderer;
        public CameraController Camera => _cam;
        public string SeedText { get; private set; }
        public float GenerationMs { get; private set; }
        public float Fps { get; private set; }
        public int HoverX { get; private set; } = -1;
        public int HoverY { get; private set; } = -1;
        public Vector2 HoverWorld { get; private set; }

        public InspectTarget Selection { get; private set; }
        public InspectTarget Hovered { get; private set; }
        public Cultivator Selected => Selection.Kind == InspectKind.Cultivator ? Selection.Cultivator : null;
        public Settlement SelectedSettlement => Selection.Kind == InspectKind.Settlement ? Selection.Settlement : null;
        public bool Follow;

        HighlightRenderer _highlight;
        static readonly Color HoverColor = new Color(1f, 1f, 1f, 0.9f);
        static readonly Color SelectColor = new Color(1f, 0.85f, 0.3f, 1f);

        void Awake()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<UnityEngine.Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = TerrainInfo.Colors[(int)Terrain.DeepOcean];
            // Unity's fake-null objects defeat ??, so check explicitly.
            _cam = cam.GetComponent<CameraController>();
            if (_cam == null) _cam = cam.gameObject.AddComponent<CameraController>();
            _cam.PointerOverUI = () => _ui != null && _ui.PointerOverUI;
            _cam.KeyboardBlocked = () => _ui != null && _ui.KeyboardBlocked;

            _renderer = GetComponent<WorldRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<WorldRenderer>();
            _units = GetComponent<UnitRenderer>();
            if (_units == null) _units = gameObject.AddComponent<UnitRenderer>();
            _fx = GetComponent<FxRenderer>();
            if (_fx == null) _fx = gameObject.AddComponent<FxRenderer>();
            _highlight = GetComponent<HighlightRenderer>();
            if (_highlight == null) _highlight = gameObject.AddComponent<HighlightRenderer>();
            _highlight.Init();
            _cursor = CreateCursor();
        }

        void Start()
        {
            Generate(Seed);
            _ui = gameObject.AddComponent<GameUI>();
            _ui.Init(this);
        }

        public void Generate(string seedText)
        {
            if (string.IsNullOrWhiteSpace(seedText)) seedText = "0";
            SeedText = seedText.Trim();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            World = MapGenerator.Generate(SeedText);
            ClearSelection();
            int speed = Sim?.SpeedIndex ?? 1;
            Sim = new Simulation(World) { SpeedIndex = speed };
            GenerationMs = (float)sw.Elapsed.TotalMilliseconds;
            _renderer.Init(World, Sim);
            _units.Init(Sim);
            _fx.Init(Sim);

            var prev = Brush;
            Brush = new WorldBrush(Sim);
            Brush.Spawned += _units.ShowSpawn;
            if (prev != null)
            {
                Brush.Tool = prev.Tool;
                Brush.Size = prev.Size;
            }

            _cam.WorldSize = new Vector2(World.W, World.H);
            var focus = new Vector2(World.W * 0.5f, World.H * 0.5f);
            if (Sim.Settlements.All.Count > 0) focus = new Vector2(Sim.Settlements.All[0].X, Sim.Settlements.All[0].Y);
            _cam.Focus(focus, Screen.height / (2f * StartPixelsPerCell));
            _ui?.OnWorldChanged();
            Debug.Log($"[ThienDao] World '{SeedText}' (seed {World.Seed}) generated in {GenerationMs:0} ms: {World.Objects.AliveCount} objects, " +
                      $"{Sim.Settlements.AliveCount} villages ({Sim.Settlements.TotalPopulation} people), {Sim.Cultivation.AliveCount} cultivators.");
        }

        public static string RandomSeed() => Random.Range(0, int.MaxValue).ToString();

        void Update()
        {
            if (World == null) return;
            Fps = Mathf.Lerp(Fps, 1f / Mathf.Max(1e-5f, Time.unscaledDeltaTime), 0.05f);

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mp = mouse.position.ReadValue();
            Vector3 wp = _cam.Cam.ScreenToWorldPoint(mp);
            HoverWorld = wp;
            HoverX = Mathf.FloorToInt(wp.x);
            HoverY = Mathf.FloorToInt(wp.y);

            HandleKeys();

            bool overUI = _ui != null && _ui.PointerOverUI;
            Hovered = overUI || !World.InBounds(HoverX, HoverY) ? default : PickAt(wp);
            if (!overUI && mouse.leftButton.wasPressedThisFrame && Brush.Tool == BrushTool.Inspect) Select(Hovered);
            if (!overUI && mouse.leftButton.wasPressedThisFrame && WorldBrush.IsDivineTool(Brush.Tool)) ActOn(WorldBrush.ActFor(Brush.Tool), Hovered);
            if (!overUI && mouse.leftButton.isPressed)
                Brush.Apply(HoverX, HoverY, mouse.leftButton.wasPressedThisFrame, Time.unscaledDeltaTime);

            Sim.RunFrame(Time.unscaledDeltaTime, SimBudgetMs);
            UpdateCursor(overUI);
        }

        void LateUpdate()
        {
            if (World == null) return;
            FollowSelection();
            _renderer.SetTint(SeasonTint(Sim.Clock.YearFraction));
            _renderer.Tick(_cam.Cam);
            _units.Tick(_cam.Cam, _cam.PixelsPerCell);
            _fx.Tick(_cam.Cam);
            UpdateHighlights();
        }

        static Color SeasonTint(float yearFraction)
        {
            float f = Mathf.Repeat(yearFraction - 0.125f, 1f) * 4f; // 0 = mid-Xuân
            int a = Mathf.FloorToInt(f) % 4;
            return Color.Lerp(SeasonTints[a], SeasonTints[(a + 1) % 4], f - Mathf.Floor(f));
        }

        public void SetSpeed(int index)
        {
            if (index > 0) _speedBeforePause = index;
            Sim.SpeedIndex = index;
        }

        // ---------------------------------------------------------------- selection

        // Priority: a person or animal drawn under the pointer, then a building (its village), then any
        // other object, then the bare cell. Sprites get a few screen pixels of slop so small ones stay clickable.
        InspectTarget PickAt(Vector2 wp)
        {
            int cx = HoverX, cy = HoverY, cell = World.Idx(cx, cy);
            var t = new InspectTarget { Entity = -1, Region = -1, Animal = -1, ObjectId = -1, CellX = cx, CellY = cy };
            float slop = 6f / Mathf.Max(0.01f, _cam.PixelsPerCell);

            if (_units.Pick(wp, slop, out var hit))
            {
                t.HoverBox = hit.Box;
                if (hit.Cultivator != null) { t.Kind = InspectKind.Cultivator; t.Cultivator = hit.Cultivator; }
                else if (hit.Settlement != null) { t.Kind = InspectKind.Settlement; t.Settlement = hit.Settlement; }
                else if (hit.Entity >= 0) { t.Kind = InspectKind.Migrants; t.Entity = hit.Entity; }
                else { t.Kind = InspectKind.Animal; t.Region = hit.Region; t.Animal = hit.Kind; }
                return t;
            }
            // Zoomed too far out for sprites: still let travelling cultivators be found.
            var c = Sim.Cultivation.FindShownNear(wp.x, wp.y, Mathf.Max(1.5f, slop * 1.5f));
            if (c != null)
            {
                var e = Sim.Entities;
                t.Kind = InspectKind.Cultivator;
                t.Cultivator = c;
                t.HoverBox = new Rect(e.X[c.Entity] - 0.6f, e.Y[c.Entity] - 0.2f, 1.2f, 1.6f);
                return t;
            }

            int obj = World.Objects.CellObject[cell];
            if (obj >= 0 && World.Objects.IsAlive(obj))
            {
                var o = World.Objects.Get(obj);
                t.ObjectId = obj;
                t.HoverBox = new Rect(o.X, o.Y, ObjectInfo.FootprintW[(int)o.Type], ObjectInfo.FootprintH[(int)o.Type]);
                var owner = ObjectInfo.IsBuilding(o.Type) ? Sim.Settlements.OwnerOfObject(obj) : null;
                if (owner != null && owner.Alive)
                {
                    t.Kind = InspectKind.Settlement;
                    t.Settlement = owner;
                }
                else t.Kind = InspectKind.Object;
                return t;
            }

            t.Kind = InspectKind.Cell;
            t.HoverBox = new Rect(cx, cy, 1f, 1f);
            return t;
        }

        // Thiên Đạo acts on exactly the one pointed at (or selected): that cultivator, or a mortal of that village.
        public void ActOn(DivineAct act, InspectTarget t)
        {
            int target = t.Kind == InspectKind.Cultivator && t.Cultivator != null ? t.Cultivator.Index : -1;
            int village = t.Kind == InspectKind.Settlement && t.Settlement != null ? t.Settlement.Id : -1;
            if (act == DivineAct.GrantRoot && target < 0 && village < 0) return; // nobody chosen
            int x = t.CellX, y = t.CellY;
            if (t.Cultivator != null && Sim.Cultivation.IsShownOnMap(t.Cultivator))
            {
                x = (int)Sim.Entities.X[t.Cultivator.Entity];
                y = (int)Sim.Entities.Y[t.Cultivator.Entity];
            }
            Sim.Enqueue(new DivineActCommand(act, x, y, target, village));
        }

        // From the ranking: select that expert; if they are out on the map, the camera follows them there.
        // Returns false when they are in seclusion (nothing on the map to follow).
        public bool FocusCultivator(Cultivator c)
        {
            if (c == null || !c.Alive) return false;
            var e = Sim.Entities;
            Select(new InspectTarget
            {
                Kind = InspectKind.Cultivator, Cultivator = c, Entity = -1, Region = -1, Animal = -1, ObjectId = -1,
                CellX = (int)e.X[c.Entity], CellY = (int)e.Y[c.Entity]
            });
            if (!Sim.Cultivation.IsShownOnMap(c)) return false;
            Follow = true;
            // Jump straight there, close enough to see them, then keep following.
            var t = _cam.transform;
            t.position = new Vector3(e.X[c.Entity], e.Y[c.Entity], t.position.z);
            if (_cam.PixelsPerCell < 6f) _cam.Focus(new Vector2(e.X[c.Entity], e.Y[c.Entity]), Screen.height / (2f * 8f));
            return true;
        }

        public void ActOnSelected(DivineAct act)
        {
            if (Selection.Kind != InspectKind.None) ActOn(act, Selection);
        }

        void Select(InspectTarget target)
        {
            Follow = false;
            Selection = target;
        }

        public void ClearSelection()
        {
            Selection = default;
            Follow = false;
        }

        // Where the current selection is right now (people and herds move; villages grow).
        Rect? SelectionBox()
        {
            var s = Selection;
            switch (s.Kind)
            {
                case InspectKind.Cultivator:
                    if (!s.Cultivator.Alive) return null;
                    if (_units.BoxOf(s.Cultivator, -1, out var cb)) return cb;
                    if (Sim.Cultivation.IsShownOnMap(s.Cultivator))
                    {
                        var e = Sim.Entities;
                        return new Rect(e.X[s.Cultivator.Entity] - 0.6f, e.Y[s.Cultivator.Entity] - 0.2f, 1.2f, 1.6f);
                    }
                    return new Rect(s.Cultivator.HomeX - 2.5f, s.Cultivator.HomeY - 2.5f, 5f, 5f); // inside the sect
                case InspectKind.Settlement:
                    return s.Settlement.Alive ? SettlementBounds(s.Settlement) : (Rect?)null;
                case InspectKind.Migrants:
                    if (Sim.Entities.Species[s.Entity] != Species.Migrants) return null;
                    return _units.BoxOf(null, s.Entity, out var mb) ? mb : (Rect?)null;
                case InspectKind.Animal:
                {
                    const int size = WildlifeSystem.Region;
                    int rw = Sim.Wildlife.RW;
                    return new Rect(s.Region % rw * size, s.Region / rw * size, size, size); // the herd's whole range
                }
                case InspectKind.Object:
                {
                    if (!World.Objects.IsAlive(s.ObjectId)) return null;
                    var o = World.Objects.Get(s.ObjectId);
                    return new Rect(o.X, o.Y, ObjectInfo.FootprintW[(int)o.Type], ObjectInfo.FootprintH[(int)o.Type]);
                }
                case InspectKind.Cell:
                    return new Rect(s.CellX, s.CellY, 1f, 1f);
                default:
                    return null;
            }
        }

        Rect SettlementBounds(Settlement s)
        {
            float x0 = s.X - 2f, y0 = s.Y - 2f, x1 = s.X + 3f, y1 = s.Y + 3f;
            foreach (int h in s.Houses)
            {
                if (!World.Objects.IsAlive(h)) continue;
                var o = World.Objects.Get(h);
                x0 = Mathf.Min(x0, o.X);
                y0 = Mathf.Min(y0, o.Y);
                x1 = Mathf.Max(x1, o.X + ObjectInfo.FootprintW[(int)o.Type]);
                y1 = Mathf.Max(y1, o.Y + ObjectInfo.FootprintH[(int)o.Type]);
            }
            return Rect.MinMaxRect(x0, y0, x1, y1);
        }

        void UpdateHighlights()
        {
            float ppc = _cam.PixelsPerCell;
            Rect? sel = SelectionBox();
            _highlight.Selection(sel, SelectColor, ppc);
            // No hover outline over what is already selected, nor while painting with another tool.
            bool aiming = Brush.Tool == BrushTool.Inspect || WorldBrush.IsDivineTool(Brush.Tool);
            bool hover = aiming && Hovered.Kind != InspectKind.None && !Hovered.Same(Selection);
            _highlight.Hover(hover ? Hovered.HoverBox : null, HoverColor, ppc);
        }

        void FollowSelection()
        {
            if (!Follow || Selection.Kind == InspectKind.None) return;
            Rect? box = SelectionBox();
            if (!box.HasValue) return;
            Vector2 target = box.Value.center;
            var t = _cam.transform;
            var p = Vector2.Lerp(t.position, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            t.position = new Vector3(p.x, p.y, t.position.z);
        }

        // ---------------------------------------------------------------- input

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) _ui?.ToggleVisible();
            if (kb.hKey.wasPressedThisFrame && !(_ui != null && _ui.KeyboardBlocked)) _ui?.ToggleChronicle();
            if (_ui != null && _ui.KeyboardBlocked) return;
            if (kb.leftBracketKey.wasPressedThisFrame) Brush.Size = Mathf.Max(1, Brush.Size - 1);
            if (kb.rightBracketKey.wasPressedThisFrame) Brush.Size = Mathf.Min(40, Brush.Size + 1);
            if (kb.escapeKey.wasPressedThisFrame)
            {
                Brush.Tool = BrushTool.Inspect;
                ClearSelection();
            }
            if (kb.tabKey.wasPressedThisFrame) _renderer.SetOverlay((OverlayMode)(((int)_renderer.Overlay + 1) % OverlayCount));
            if (kb.spaceKey.wasPressedThisFrame) SetSpeed(Sim.Paused ? _speedBeforePause : 0);
            if (kb.digit1Key.wasPressedThisFrame) SetSpeed(1);
            if (kb.digit2Key.wasPressedThisFrame) SetSpeed(2);
            if (kb.digit3Key.wasPressedThisFrame) SetSpeed(3);
            if (kb.digit4Key.wasPressedThisFrame) SetSpeed(4);
        }

        SpriteRenderer CreateCursor()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[64];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                px[y * 8 + x] = x == 0 || y == 0 || x == 7 || y == 7 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 28);
            tex.SetPixelData(px, 0);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f, 0, SpriteMeshType.FullRect, new Vector4(1, 1, 1, 1));
            var go = new GameObject("BrushCursor");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            sr.sharedMaterial = new Material(shader);
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 20;
            return sr;
        }

        void UpdateCursor(bool overUI)
        {
            bool inside = World.InBounds(HoverX, HoverY);
            // Xem and the Thiên Đạo acts use the hover outline on their target instead.
            _cursor.enabled = !overUI && inside && Brush.Tool != BrushTool.Inspect && !WorldBrush.IsDivineTool(Brush.Tool);
            if (!_cursor.enabled) return;

            if (WorldBrush.IsBuildingTool(Brush.Tool))
            {
                var type = WorldBrush.BuildingFor(Brush.Tool);
                int fw = ObjectInfo.FootprintW[(int)type], fh = ObjectInfo.FootprintH[(int)type];
                int ox = HoverX - fw / 2, oy = HoverY - fh / 2;
                _cursor.size = new Vector2(fw, fh);
                _cursor.transform.position = new Vector3(ox + fw * 0.5f, oy + fh * 0.5f, 0f);
                _cursor.color = new Color(1f, 0.95f, 0.6f);
            }
            else
            {
                bool point = Brush.Tool == BrushTool.Inspect || Brush.Tool == BrushTool.FoundVillage || Brush.Tool >= BrushTool.SpawnDeer;
                float s = point ? 1f : Brush.Size;
                _cursor.size = new Vector2(s, s);
                _cursor.transform.position = new Vector3(HoverX + 0.5f, HoverY + 0.5f, 0f);
                _cursor.color = Brush.Tool == BrushTool.Smite ? new Color(1f, 0.6f, 0.4f) : Color.white;
            }
        }
    }
}
