using System.Collections.Generic;
using ThienDao.Player;
using ThienDao.Render;
using ThienDao.Sim;
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

        static readonly string[] OverlayNames = { "Không", "Linh khí", "Độ cao", "Nhiệt độ", "Độ ẩm", "Thức ăn", "Lãnh thổ" };

        // Colour grade at the middle of each season; lerped through the year.
        static readonly Color[] SeasonTints =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 0.985f, 0.93f),
            new Color(1f, 0.92f, 0.8f),
            new Color(0.86f, 0.9f, 1f),
        };

        const double SimBudgetMs = 8.0;

        WorldData _world;
        Simulation _sim;
        int _speedBeforePause = 1;
        Rect _timeRect;
        Rect _sidePanelRect;
        readonly List<Cultivator> _ranking = new List<Cultivator>();
        WorldRenderer _renderer;
        UnitRenderer _units;
        FxRenderer _fx;
        CameraController _cam;
        GUIStyle _label;
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
            _units = GetComponent<UnitRenderer>();
            if (_units == null) _units = gameObject.AddComponent<UnitRenderer>();
            _fx = GetComponent<FxRenderer>();
            if (_fx == null) _fx = gameObject.AddComponent<FxRenderer>();
            _cursor = CreateCursor();
            _seedInput = Seed;
        }

        void Start() => Generate(_seedInput);

        public void Generate(string seedText)
        {
            if (string.IsNullOrWhiteSpace(seedText)) seedText = "0";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _world = MapGenerator.Generate(seedText.Trim());
            _selected = null;
            _selectedSettlement = null;
            _follow = false;
            int speed = _sim?.SpeedIndex ?? 1;
            _sim = new Simulation(_world) { SpeedIndex = speed };
            _genMs = (float)sw.Elapsed.TotalMilliseconds;
            _renderer.Init(_world, _sim);
            _units.Init(_sim);
            _fx.Init(_sim);

            var prev = _brush;
            _brush = new WorldBrush(_sim);
            if (prev != null)
            {
                _brush.Tool = prev.Tool;
                _brush.Size = prev.Size;
            }

            _cam.WorldSize = new Vector2(_world.W, _world.H);
            var focus = new Vector2(_world.W * 0.5f, _world.H * 0.5f);
            if (_sim.Settlements.All.Count > 0) focus = new Vector2(_sim.Settlements.All[0].X, _sim.Settlements.All[0].Y);
            _cam.Focus(focus, Screen.height / (2f * StartPixelsPerCell));
            Debug.Log($"[ThienDao] World '{seedText}' (seed {_world.Seed}) generated in {_genMs:0} ms: {_world.Objects.AliveCount} objects, " +
                      $"{_sim.Settlements.AliveCount} villages ({_sim.Settlements.TotalPopulation} people), {_sim.Entities.Alive} creatures.");
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
            _pointerOverUI = _showUI && (_panelRect.Contains(gui) || _timeRect.Contains(gui) || _sidePanelRect.Contains(gui) ||
                                         (HasSelection && _popupRect.Contains(gui)));

            Vector3 wp = _cam.Cam.ScreenToWorldPoint(mp);
            _hoverX = Mathf.FloorToInt(wp.x);
            _hoverY = Mathf.FloorToInt(wp.y);

            HandleKeys();

            if (!_pointerOverUI && mouse.leftButton.wasPressedThisFrame && _brush.Tool == BrushTool.Inspect) Select(wp.x, wp.y);
            if (!_pointerOverUI && mouse.leftButton.isPressed)
                _brush.Apply(_hoverX, _hoverY, mouse.leftButton.wasPressedThisFrame, Time.unscaledDeltaTime);

            _sim.RunFrame(Time.unscaledDeltaTime, SimBudgetMs);
            UpdateCursor();
        }

        // ---------------------------------------------------------------- selection popup

        Cultivator _selected;
        Settlement _selectedSettlement;
        bool _follow;
        Rect _popupRect = new Rect(0, 0, 360, 10);
        readonly List<string> _selectedEvents = new List<string>();

        bool HasSelection => _selected != null || _selectedSettlement != null;

        void Select(float x, float y)
        {
            _follow = false;
            _selected = _sim.Cultivation.FindShownNear(x, y, 1.5f);
            _selectedSettlement = _selected == null && _world.InBounds((int)x, (int)y) ? _sim.Settlements.Owning(_world.Idx((int)x, (int)y)) : null;
            if (!HasSelection) return;
            float px = Mathf.Clamp(Screen.width / _uiScale - 360f - 360f, 320f, 99999f);
            _popupRect = new Rect(px, 130f, 360f, 10f);
        }

        void FollowSelection()
        {
            if (!_follow || _selected == null) return;
            var e = _sim.Entities;
            Vector2 target = _sim.Cultivation.IsShownOnMap(_selected)
                ? new Vector2(e.X[_selected.Entity], e.Y[_selected.Entity])
                : new Vector2(_selected.HomeX, _selected.HomeY);
            var t = _cam.transform;
            var p = Vector2.Lerp(t.position, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            t.position = new Vector3(p.x, p.y, t.position.z);
        }

        void DrawPopup(int id)
        {
            if (_selected != null) DrawCultivatorPopup(_selected);
            else if (_selectedSettlement != null) DrawSettlementPopup(_selectedSettlement);
            GUILayout.BeginHorizontal();
            if (_selected != null && GUILayout.Button(_follow ? "Bỏ theo dõi" : "Theo dõi")) _follow = !_follow;
            if (GUILayout.Button("Đóng"))
            {
                _selected = null;
                _selectedSettlement = null;
                _follow = false;
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }

        void DrawCultivatorPopup(Cultivator c)
        {
            long tick = _sim.Clock.Tick;
            GUILayout.Label($"{c.Title}{(c.Demonic ? "  ·  MA TU" : "")}", _title);
            GUILayout.Label(c.SectId >= 0 ? $"{_sim.Cultivation.Role(c)} · {_sim.Cultivation.SectName(c)}" : _sim.Cultivation.Role(c));
            if (!c.Alive)
            {
                GUILayout.Label("Đã vẫn lạc.");
            }
            else
            {
                float need = Realms.Need(c.Realm, c.Stage);
                GUILayout.Label($"Cảnh giới: {c.RealmText}");
                ProgressBar(Mathf.Clamp01(c.Progress / need), new Color(0.45f, 0.85f, 1f));
                GUILayout.Label($"Tuổi: {c.AgeYears(tick):0} / thọ nguyên {c.LifespanYears} năm");
                ProgressBar(Mathf.Clamp01(c.AgeYears(tick) / c.LifespanYears), new Color(1f, 0.6f, 0.4f));
                GUILayout.Label($"Linh căn: {SpiritRoots.Kind(c.Roots)} ({SpiritRoots.Elements(c.Roots)}) · tốc độ ×{SpiritRoots.SpeedMultiplier(c.Roots):0.0}");
                GUILayout.Label($"Ngộ tính {c.Comprehension * 100f:0} · Tâm cảnh {c.DaoHeart * 100f:0} · Khí vận {c.Luck * 100f:0}");
                GUILayout.Label($"Hiện tại: {c.Activity}" + (c.FailedAttempts > 0 ? $" · đột phá thất bại {c.FailedAttempts} lần" : ""));
                GUILayout.Label($"Linh khí nơi ở: {_sim.Qi.SampleQi((int)c.HomeX, (int)c.HomeY):0} (cần {Realms.RequiredQi[(int)c.Realm]:0})");
            }

            _selectedEvents.Clear();
            var events = _sim.Events.Recent;
            for (int k = events.Count - 1; k >= 0 && _selectedEvents.Count < 6; k--)
                if (events[k].Text.Contains(c.Name)) _selectedEvents.Add($"Năm {events[k].Tick / Core.SimClock.DaysPerYear + 1}: {events[k].Text}");
            if (_selectedEvents.Count > 0)
            {
                GUILayout.Label("Sự kiện gần đây:");
                foreach (var line in _selectedEvents) GUILayout.Label("• " + line);
            }
        }

        void DrawSettlementPopup(Settlement s)
        {
            int pop = Mathf.Max(1, s.Population);
            GUILayout.Label(s.Name, _title);
            GUILayout.Label(s.Alive
                ? $"{s.Population} người · {s.Houses.Count} nhà · {s.Farms.Count} ô ruộng\nLương thực {s.Food / pop:0.0} tháng · năm qua sinh {s.BirthsLastYear}, mất {s.DeathsLastYear}"
                : "Đã bị bỏ hoang.");
            GUILayout.Label($"Lập năm {s.FoundedTick / Core.SimClock.DaysPerYear + 1}" +
                            (s.ParentId >= 0 ? $" bởi di dân từ {_sim.Settlements.All[s.ParentId].Name}" : ""));
            if (!s.Sect) return;
            var count = new int[(int)Realm.Count];
            int members = 0;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.SectId != s.Id) continue;
                count[(int)c.Realm]++;
                members++;
            }
            GUILayout.Label($"Tông môn có {members} tu sĩ: Luyện Khí {count[1]} · Trúc Cơ {count[2]} · Kết Đan {count[3]} · Nguyên Anh {count[4]} · Hóa Thần {count[5]}");
        }

        static void ProgressBar(float value, Color fill)
        {
            var r = GUILayoutUtility.GetRect(300f, 10f, GUILayout.ExpandWidth(true));
            GUI.Box(r, GUIContent.none);
            var prev = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * value, r.height - 2f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void LateUpdate()
        {
            if (_world == null) return;
            FollowSelection();
            _renderer.SetTint(SeasonTint(_sim.Clock.YearFraction));
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

        void SetSpeed(int index)
        {
            if (index > 0) _speedBeforePause = index;
            _sim.SpeedIndex = index;
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
            if (kb.spaceKey.wasPressedThisFrame) SetSpeed(_sim.Paused ? _speedBeforePause : 0);
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
                _timeRect = Rect.zero;
                _sidePanelRect = Rect.zero;
                GUI.Label(new Rect(10, 10, 300, 24), "F1: hiện giao diện");
                return;
            }

            DrawVillageLabels();
            DrawCultivatorLabels();
            DrawTimeBar();
            DrawSidePanel();
            if (HasSelection) _popupRect = GUILayout.Window(77, _popupRect, DrawPopup, "Thông tin", GUILayout.Width(360f));

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
            GUILayout.Label("THIÊN ĐẠO  ·  M3 Tu tiên", _title);

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
            GUILayout.Label($"Vật thể: {_world.Objects.AliveCount:N0}   ·   Lệnh đã ghi: {_sim.Log.Count}");
            GUILayout.Label("Chuột phải/giữa: kéo   ·   Lăn: zoom\nWASD: di chuyển (Shift nhanh)   ·   Esc: Xem\nSpace: dừng/chạy   ·   1–4: tốc độ   ·   F1: ẩn UI");
            GUILayout.EndArea();
        }

        void DrawTimeBar()
        {
            const float h = 104f;
            float w = Mathf.Clamp(Screen.width / _uiScale - 320f - 360f, 380f, 600f); // between the two side panels
            float x = 320f;
            _timeRect = new Rect(x, 10, w, h);
            GUILayout.BeginArea(_timeRect, GUI.skin.box);
            GUILayout.Label(_sim.Clock.DateText, _title);
            GUILayout.BeginHorizontal();
            int speed = GUILayout.Toolbar(_sim.SpeedIndex, Simulation.SpeedNames, GUILayout.Width(330));
            if (speed != _sim.SpeedIndex) SetSpeed(speed);
            GUILayout.Label($"  {Simulation.SpeedDaysPerSecond[_sim.SpeedIndex]:0} ngày/giây · {_sim.TicksLastFrame} tick/frame");
            GUILayout.EndHorizontal();
            var wild = _sim.Wildlife;
            var st = _sim.Settlements;
            GUILayout.Label($"Dân: {st.TotalPopulation:N0} · {st.AliveCount} làng · {st.MigrantGroups} đoàn di dân   |   " +
                            $"Hươu {wild.Total(Species.Deer):N0} · Thỏ {wild.Total(Species.Rabbit):N0} · Sói {wild.Total(Species.Wolf):N0}");
            var cr = _sim.Cultivation.CountByRealm;
            GUILayout.Label($"Tu sĩ: {_sim.Cultivation.AliveCount} · Luyện Khí {cr[(int)Realm.LuyenKhi]} · Trúc Cơ {cr[(int)Realm.TrucCo]} · " +
                            $"Kết Đan {cr[(int)Realm.KetDan]} · Nguyên Anh {cr[(int)Realm.NguyenAnh]} · Hóa Thần {cr[(int)Realm.HoaThan]}");
            GUILayout.EndArea();
        }

        void DrawSidePanel()
        {
            const float w = 340f;
            float h = Mathf.Min(560f, Screen.height / _uiScale - 20f);
            _sidePanelRect = new Rect(Screen.width / _uiScale - w - 10f, 10f, w, h);
            GUILayout.BeginArea(_sidePanelRect, GUI.skin.box);

            GUILayout.Label("BẢNG CƯỜNG GIẢ", _title);
            _ranking.Clear();
            foreach (var c in _sim.Cultivation.All)
                if (c.Alive) _ranking.Add(c);
            _ranking.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            for (int k = 0; k < Mathf.Min(6, _ranking.Count); k++)
            {
                var c = _ranking[k];
                string where = c.SectId >= 0 ? $"{_sim.Cultivation.Role(c)} {_sim.Cultivation.SectName(c)}" : _sim.Cultivation.Role(c);
                GUILayout.Label($"{k + 1}. {c.Title} · {c.RealmText}{(c.Demonic ? " (ma tu)" : "")}\n    {where} · {c.AgeYears(_sim.Clock.Tick):0}/{c.LifespanYears} tuổi");
            }

            GUILayout.Space(6);
            GUILayout.Label("SỰ KIỆN", _title);
            var events = _sim.Events.Recent;
            int shown = 0;
            for (int k = events.Count - 1; k >= 0 && shown < 8; k--)
            {
                var ev = events[k];
                if (ev.Importance < 1) continue;
                GUILayout.Label($"Năm {ev.Tick / Core.SimClock.DaysPerYear + 1}: {ev.Text}");
                shown++;
            }
            GUILayout.EndArea();
        }

        void DrawCultivatorLabels()
        {
            if (_cam.PixelsPerCell < 3f) return;
            var cam = _cam.Cam;
            var e = _sim.Entities;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Realm < Realm.KetDan) continue;
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(e.X[c.Entity], e.Y[c.Entity] + 2.6f, 0f));
                if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                var r = new Rect(sp.x / _uiScale - 100f, (Screen.height - sp.y) / _uiScale - 10f, 200f, 20f);
                string text = $"{c.Title} · {Realms.Names[(int)c.Realm]}";
                var prev = GUI.color;
                GUI.color = Color.black;
                GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, _label);
                GUI.color = c.Demonic ? new Color(1f, 0.5f, 0.5f) : new Color(1f, 0.92f, 0.6f);
                GUI.Label(r, text, _label);
                GUI.color = prev;
            }
        }

        void DrawVillageLabels()
        {
            if (_cam.PixelsPerCell < 3f) return;
            if (_label == null)
                _label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 13 };
            var cam = _cam.Cam;
            foreach (var s in _sim.Settlements.All)
            {
                if (!s.Alive) continue;
                Vector3 sp = cam.WorldToScreenPoint(new Vector3(s.X + 0.5f, s.Y + 5f, 0f));
                if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                var r = new Rect(sp.x / _uiScale - 90f, (Screen.height - sp.y) / _uiScale - 12f, 180f, 24f);
                string text = $"{s.Name} ({s.Population})";
                var shadow = new Rect(r.x + 1f, r.y + 1f, r.width, r.height);
                var prev = GUI.color;
                GUI.color = Color.black;
                GUI.Label(shadow, text, _label);
                GUI.color = prev;
                GUI.Label(r, text, _label);
            }
        }

        void DrawHoverInfo()
        {
            if (!_world.InBounds(_hoverX, _hoverY))
            {
                GUILayout.Label("Ô: (ngoài bản đồ)");
                return;
            }
            int i = _world.Idx(_hoverX, _hoverY);
            float temp01 = _world.Temperature[i] / 255f + _sim.Clock.SeasonalTemperatureOffset;
            float tempC = -20f + temp01 * 60f;
            string obj = "—";
            int id = _world.Objects.CellObject[i];
            if (id >= 0) obj = ObjectInfo.Names[(int)_world.Objects.Get(id).Type];
            GUILayout.Label(
                $"Ô ({_hoverX}, {_hoverY}):  {TerrainInfo.Names[(int)_world.Terrain[i]]}\n" +
                $"Độ cao {_world.Height[i]:0.00}   ·   Nhiệt {tempC:0}°C   ·   Ẩm {_world.Moisture[i] * 100 / 255}%\n" +
                $"Linh khí {_sim.Qi.SampleQi(_hoverX, _hoverY):0} / trần {_world.QiCap[i]}{(_world.LeyLine[i] ? "   ·   LINH MẠCH" : "")}\n" +
                $"Vật thể: {obj}   ·   Cỏ {_sim.Forage.At(_hoverX, _hoverY):0}/{_sim.Forage.CapAt(_hoverX, _hoverY):0}");

            var s = _sim.Settlements.Owning(i);
            if (s != null)
            {
                int pop = Mathf.Max(1, s.Population);
                GUILayout.Label(
                    $"{s.Name}{(s.Alive ? "" : " (đã bỏ hoang)")}: {s.Population} người · {s.Houses.Count} nhà · {s.Farms.Count} ô ruộng\n" +
                    $"Lương thực {s.Food / pop:0.0} tháng · thu hoạch {s.LastHarvest:0} · săn {s.LastHunt:0}\n" +
                    $"Năm qua: sinh {s.BirthsLastYear} · mất {s.DeathsLastYear} · lập năm {s.FoundedTick / Core.SimClock.DaysPerYear + 1}");
            }

            var shown = _sim.Cultivation.FindShownNear(_hoverX + 0.5f, _hoverY + 0.5f, 1.5f);
            if (shown != null)
                GUILayout.Label($"{shown.Title} · {shown.RealmText} · {shown.Activity}  (click để xem)");

            var wild = _sim.Wildlife;
            int region = wild.RegionOf(_hoverX, _hoverY);
            GUILayout.Label($"Thú hoang trong vùng: Hươu {wild.At(Species.Deer, region):0} · Thỏ {wild.At(Species.Rabbit, region):0} · Sói {wild.At(Species.Wolf, region):0}");

            int c = _sim.Creatures.FindNearest(_hoverX + 0.5f, _hoverY + 0.5f, 1.5f, CreatureSystem.AnyMask);
            if (c >= 0)
            {
                var e = _sim.Entities;
                var cu = _sim.Cultivation.ForEntity(c);
                if (cu == null)
                {
                    GUILayout.Label($"{SpeciesInfo.Names[(int)e.Species[c]]}: đã đi {_sim.Clock.Tick - e.BirthTick[c]:0} ngày");
                }
            }
        }
    }
}
