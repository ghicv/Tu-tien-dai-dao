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

        public Cultivator Selected { get; private set; }
        public Settlement SelectedSettlement { get; private set; }
        public bool Follow;

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
            if (!overUI && mouse.leftButton.wasPressedThisFrame && Brush.Tool == BrushTool.Inspect) Select(wp.x, wp.y);
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

        void Select(float x, float y)
        {
            Follow = false;
            Selected = Sim.Cultivation.FindShownNear(x, y, 1.5f);
            SelectedSettlement = Selected == null && World.InBounds((int)x, (int)y) ? Sim.Settlements.Owning(World.Idx((int)x, (int)y)) : null;
        }

        public void ClearSelection()
        {
            Selected = null;
            SelectedSettlement = null;
            Follow = false;
        }

        void FollowSelection()
        {
            if (!Follow || Selected == null || !Selected.Alive) return;
            var e = Sim.Entities;
            Vector2 target = Sim.Cultivation.IsShownOnMap(Selected)
                ? new Vector2(e.X[Selected.Entity], e.Y[Selected.Entity])
                : new Vector2(Selected.HomeX, Selected.HomeY);
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
            _cursor.enabled = !overUI && inside;
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
