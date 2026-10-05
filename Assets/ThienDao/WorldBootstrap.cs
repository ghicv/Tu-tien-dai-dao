using ThienDao.Player;
using ThienDao.Render;
using ThienDao.World;
using UnityEngine;
using UnityEngine.InputSystem;
using Terrain = ThienDao.World.Terrain;

namespace ThienDao
{
    public sealed class WorldBootstrap : MonoBehaviour
    {
        public string Seed = "ThienDao";
        [Tooltip("Initial zoom, in screen pixels per cell.")]
        public float StartPixelsPerCell = 4f;

        static readonly string[] OverlayNames = { "Không", "Linh khí", "Độ cao", "Nhiệt độ", "Độ ẩm" };

        WorldData _world;
        WorldRenderer _renderer;
        CameraController _cam;
        WorldBrush _brush;
        SpriteRenderer _cursor;

        string _seedInput;
        float _genMs;
        bool _showUI = true;
        Rect _panelRect;
        float _uiScale = 1f;
        float _fps;
        int _hoverX = -1, _hoverY = -1;
        bool _pointerOverUI;
        GUIStyle _title;

        void Awake()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = TerrainInfo.Colors[(int)Terrain.DeepOcean];
            // Unity's fake-null objects defeat ??, so check explicitly.
            _cam = cam.GetComponent<CameraController>();
            if (_cam == null) _cam = cam.gameObject.AddComponent<CameraController>();
            _cam.PointerOverUI = () => _pointerOverUI;
            _cam.KeyboardBlocked = () => GUIUtility.keyboardControl != 0;

            _renderer = GetComponent<WorldRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<WorldRenderer>();
            _cursor = CreateCursor();
            _seedInput = Seed;
        }

        void Start() => Generate(_seedInput);

        public void Generate(string seedText)
        {
            if (string.IsNullOrWhiteSpace(seedText)) seedText = "0";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _world = MapGenerator.Generate(seedText.Trim());
            _genMs = (float)sw.Elapsed.TotalMilliseconds;
            _renderer.Init(_world);

            var prev = _brush;
            _brush = new WorldBrush(_world, _renderer);
            if (prev != null)
            {
                _brush.Tool = prev.Tool;
                _brush.Size = prev.Size;
            }

            _cam.WorldSize = new Vector2(_world.W, _world.H);
            _cam.Focus(new Vector2(_world.W * 0.5f, _world.H * 0.5f), Screen.height / (2f * StartPixelsPerCell));
            Debug.Log($"[ThienDao] World '{seedText}' (seed {_world.Seed}) generated in {_genMs:0} ms, {_world.Objects.AliveCount} objects.");
        }

        static string RandomSeed() => Random.Range(0, int.MaxValue).ToString();

        void Update()
        {
            if (_world == null) return;
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(1e-5f, Time.unscaledDeltaTime), 0.05f);

            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mp = mouse.position.ReadValue();
            var gui = new Vector2(mp.x / _uiScale, (Screen.height - mp.y) / _uiScale);
            _pointerOverUI = _showUI && _panelRect.Contains(gui);

            Vector3 wp = _cam.Cam.ScreenToWorldPoint(mp);
            _hoverX = Mathf.FloorToInt(wp.x);
            _hoverY = Mathf.FloorToInt(wp.y);

            HandleKeys();

            if (!_pointerOverUI && mouse.leftButton.isPressed)
                _brush.Apply(_hoverX, _hoverY, mouse.leftButton.wasPressedThisFrame, Time.unscaledDeltaTime);

            UpdateCursor();
        }

        void LateUpdate()
        {
            if (_world != null) _renderer.Tick(_cam.Cam);
        }

        void HandleKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) _showUI = !_showUI;
            if (GUIUtility.keyboardControl != 0) return;
            if (kb.leftBracketKey.wasPressedThisFrame) _brush.Size = Mathf.Max(1, _brush.Size - 1);
            if (kb.rightBracketKey.wasPressedThisFrame) _brush.Size = Mathf.Min(40, _brush.Size + 1);
            if (kb.escapeKey.wasPressedThisFrame) _brush.Tool = BrushTool.Inspect;
            if (kb.tabKey.wasPressedThisFrame)
                _renderer.SetOverlay((OverlayMode)(((int)_renderer.Overlay + 1) % OverlayNames.Length));
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

        void UpdateCursor()
        {
            bool show = !_pointerOverUI && _brush.Tool != BrushTool.Inspect && _world.InBounds(_hoverX, _hoverY);
            _cursor.enabled = show || (_brush.Tool == BrushTool.Inspect && _world.InBounds(_hoverX, _hoverY) && !_pointerOverUI);
            if (!_cursor.enabled) return;

            if (WorldBrush.IsBuildingTool(_brush.Tool))
            {
                var type = WorldBrush.BuildingFor(_brush.Tool);
                int fw = ObjectInfo.FootprintW[(int)type], fh = ObjectInfo.FootprintH[(int)type];
                int ox = _hoverX - fw / 2, oy = _hoverY - fh / 2;
                _cursor.size = new Vector2(fw, fh);
                _cursor.transform.position = new Vector3(ox + fw * 0.5f, oy + fh * 0.5f, 0f);
                _cursor.color = new Color(1f, 0.95f, 0.6f);
            }
            else
            {
                float s = _brush.Tool == BrushTool.Inspect ? 1f : _brush.Size;
                _cursor.size = new Vector2(s, s);
                _cursor.transform.position = new Vector3(_hoverX + 0.5f, _hoverY + 0.5f, 0f);
                _cursor.color = Color.white;
            }
        }

        void OnGUI()
        {
            if (_world == null) return;
            _uiScale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));
            if (_title == null) _title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, richText = true };

            if (!_showUI)
            {
                _panelRect = Rect.zero;
                GUI.Label(new Rect(10, 10, 300, 24), "F1: hiện giao diện");
                return;
            }

            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "seed")
            {
                Generate(_seedInput);
                GUI.FocusControl(null);
                e.Use();
            }

            _panelRect = new Rect(10, 10, 300, Screen.height / _uiScale - 20);
            GUILayout.BeginArea(_panelRect, GUI.skin.box);
            GUILayout.Label("THIÊN ĐẠO  ·  M0 Bản đồ", _title);

            GUILayout.Label("Seed (chữ hoặc số):");
            GUI.SetNextControlName("seed");
            _seedInput = GUILayout.TextField(_seedInput, 64);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Tạo thế giới")) Generate(_seedInput);
            if (GUILayout.Button("Seed ngẫu nhiên"))
            {
                _seedInput = RandomSeed();
                Generate(_seedInput);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Seed số: {_world.Seed}   ·   tạo trong {_genMs:0} ms");

            GUILayout.Space(8);
            GUILayout.Label("Quyền năng Thiên Đạo (chuột trái):");
            _brush.Tool = (BrushTool)GUILayout.SelectionGrid((int)_brush.Tool, WorldBrush.ToolNames, 3);
            GUILayout.Label($"Cỡ cọ: {_brush.Size}   ( [  /  ] )");
            _brush.Size = Mathf.RoundToInt(GUILayout.HorizontalSlider(_brush.Size, 1, 40));

            GUILayout.Space(8);
            GUILayout.Label("Lớp phủ (Tab):");
            int ov = GUILayout.SelectionGrid((int)_renderer.Overlay, OverlayNames, 3);
            if (ov != (int)_renderer.Overlay) _renderer.SetOverlay((OverlayMode)ov);

            GUILayout.Space(8);
            DrawHoverInfo();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"FPS {_fps:0}   ·   {_cam.PixelsPerCell:0.0} px/ô   ·   {(_renderer.OverviewMode ? "tổng quan" : "chi tiết")}");
            GUILayout.Label($"Chunk: {_renderer.ResidentChunks}/{_renderer.ChunkCount} đang giữ   ·   vẽ lại {_renderer.RedrawsLastFrame}/frame");
            GUILayout.Label($"Vật thể: {_world.Objects.AliveCount:N0}");
            GUILayout.Label("Chuột phải/giữa: kéo   ·   Lăn: zoom\nWASD: di chuyển (Shift nhanh)   ·   Esc: Xem   ·   F1: ẩn UI");
            GUILayout.EndArea();
        }

        void DrawHoverInfo()
        {
            if (!_world.InBounds(_hoverX, _hoverY))
            {
                GUILayout.Label("Ô: (ngoài bản đồ)");
                return;
            }
            int i = _world.Idx(_hoverX, _hoverY);
            float tempC = -20f + _world.Temperature[i] / 255f * 60f;
            string obj = "—";
            int id = _world.Objects.CellObject[i];
            if (id >= 0) obj = ObjectInfo.Names[(int)_world.Objects.Get(id).Type];
            GUILayout.Label(
                $"Ô ({_hoverX}, {_hoverY}):  {TerrainInfo.Names[(int)_world.Terrain[i]]}\n" +
                $"Độ cao {_world.Height[i]:0.00}   ·   Nhiệt {tempC:0}°C   ·   Ẩm {_world.Moisture[i] * 100 / 255}%\n" +
                $"Linh khí {_world.Qi[i]}/{_world.QiCap[i]}{(_world.LeyLine[i] ? "   ·   LINH MẠCH" : "")}\n" +
                $"Vật thể: {obj}");
        }
    }
}
