using System.Collections.Generic;
using System.Text;
using ThienDao.Core;
using ThienDao.Player;
using ThienDao.Render;
using ThienDao.Sim;
using ThienDao.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Terrain = ThienDao.World.Terrain;
using Unit = ThienDao.Render.SpriteLibrary.Unit;

namespace ThienDao.UI
{
    // WorldBox-style interface: a power toolbar with tabs at the bottom, a compact clock top-left,
    // window toggles top-right, and a character card when something is clicked.
    public sealed class GameUI : MonoBehaviour
    {
        WorldBootstrap _game;
        Canvas _canvas, _labelCanvas;
        RectTransform _root, _labelRoot;
        bool _visible = true;

        // top-left clock
        Text _date, _summary;
        readonly List<Ui.IconButton> _speedButtons = new List<Ui.IconButton>();
        RectTransform _ticker;
        readonly List<(Text text, float shownAt)> _tickerLines = new List<(Text, float)>();
        long _tickerSeen;

        // bottom toolbar
        RectTransform _tabsRow, _toolsRow;
        readonly List<Ui.IconButton> _tabButtons = new List<Ui.IconButton>();
        readonly List<(Ui.IconButton button, BrushTool tool)> _toolButtons = new List<(Ui.IconButton, BrushTool)>();
        readonly List<(Ui.IconButton button, OverlayMode mode)> _overlayButtons = new List<(Ui.IconButton, OverlayMode)>();
        Ui.IconButton _inspectButton;
        Text _brushSize;
        int _tab;
        InputField _seedField;
        Ui.IconButton _labelsToggle;
        public bool ShowLabels = true;

        // windows
        RectTransform _windowStack;
        Window _ranking, _events, _stats;
        float _slowRefresh;
        readonly List<Cultivator> _rank = new List<Cultivator>();

        // inspector card
        RectTransform _card;
        Text _cardTitle, _cardBody;
        Image _cardBar1, _cardBar2;
        RectTransform _cardBar1Root, _cardBar2Root;
        Ui.IconButton _followButton;

        // pointer helpers
        RectTransform _tooltip;
        Text _tooltipText;
        Text _hoverHint;
        readonly List<Text> _labels = new List<Text>();

        static readonly string[] TabNames = { "Địa hình", "Sinh linh", "Linh khí", "Thiên Đạo", "Lớp phủ", "Thế giới" };
        static readonly string[] OverlayNames = { "Không", "Linh khí", "Độ cao", "Nhiệt độ", "Độ ẩm", "Thức ăn", "Lãnh thổ" };

        static readonly Dictionary<BrushTool, string> ToolHelp = new Dictionary<BrushTool, string>
        {
            { BrushTool.Inspect, "Xem — click vào tu sĩ, làng, tông môn để xem thông tin" },
            { BrushTool.Grass, "Cỏ — biến đất thành đồng cỏ" }, { BrushTool.Sand, "Cát — bãi cát" }, { BrushTool.Desert, "Sa mạc" },
            { BrushTool.Shallow, "Nước nông — ai đứng đó sẽ chết đuối" }, { BrushTool.DeepWater, "Biển sâu — nhấn chìm mọi thứ" },
            { BrushTool.Hills, "Đồi" }, { BrushTool.Mountain, "Núi — làng bị đè sẽ phải dời đi" }, { BrushTool.Snow, "Tuyết" },
            { BrushTool.Trees, "Trồng cây theo khí hậu" }, { BrushTool.House, "Đặt nhà dân" }, { BrushTool.SectHall, "Đặt đại điện tông môn" },
            { BrushTool.FoundVillage, "Lập làng — 24 phàm nhân khai hoang" }, { BrushTool.Erase, "Xóa cây, nhà, công trình" },
            { BrushTool.SpawnDeer, "Thả hươu vào vùng" }, { BrushTool.SpawnRabbit, "Thả thỏ vào vùng" }, { BrushTool.SpawnWolf, "Thả sói vào vùng" },
            { BrushTool.LeyAdd, "Vẽ linh mạch — trần linh khí quanh đó tăng dần" }, { BrushTool.LeyErase, "Phá linh mạch" },
            { BrushTool.QiInfuse, "Rót linh khí — linh khí tràn ra rồi tản dần" }, { BrushTool.QiDrain, "Hút linh khí — vùng đó cạn kiệt" },
            { BrushTool.GrantRoot, "Ban linh căn — một đứa trẻ ở làng gần nhất được Thiên linh căn / Dị linh căn" },
            { BrushTool.Bless, "Ban cơ duyên — tu sĩ gần nhất tu vi tăng mạnh, thêm thọ" },
            { BrushTool.Smite, "Thiên phạt — sét đánh xuống; trúng tu sĩ thì hồn phi phách tán" },
        };

        public bool PointerOverUI => _visible && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public bool KeyboardBlocked
        {
            get
            {
                var go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                var field = go != null ? go.GetComponent<InputField>() : null;
                return field != null && field.isFocused;
            }
        }

        public void ToggleVisible()
        {
            _visible = !_visible;
            _root.gameObject.SetActive(_visible);
        }

        // ---------------------------------------------------------------- construction

        public void Init(WorldBootstrap game)
        {
            _game = game;
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }
            _labelCanvas = MakeCanvas("Labels", 0, out _labelRoot);
            _canvas = MakeCanvas("UI", 10, out _root);
            BuildClock();
            BuildToolbar();
            BuildWindows();
            BuildCard();
            BuildPointerHelpers();
            SelectTab(0);
        }

        Canvas MakeCanvas(string name, int order, out RectTransform root)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (order == 0) go.GetComponent<GraphicRaycaster>().enabled = false; // labels never block the map
            root = (RectTransform)go.transform;
            return canvas;
        }

        static LayoutElement Element(Component c)
        {
            // Unity's fake-null defeats ??, so check explicitly.
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            return le;
        }

        static LayoutElement Size(Component c, float w, float h)
        {
            var le = Element(c);
            le.preferredWidth = le.minWidth = w;
            le.preferredHeight = le.minHeight = h;
            return le;
        }

        // Fixes only the height; the width still comes from the layout (or stretches).
        static LayoutElement Height(Component c, float h)
        {
            var le = Element(c);
            le.preferredHeight = le.minHeight = h;
            return le;
        }

        static HorizontalLayoutGroup Row(RectTransform rt, float spacing, int padding = 0)
        {
            var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = spacing;
            row.padding = new RectOffset(padding, padding, padding, padding);
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            return row;
        }

        static VerticalLayoutGroup Column(RectTransform rt, float spacing, int padding = 0)
        {
            var col = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = spacing;
            col.padding = new RectOffset(padding, padding, padding, padding);
            col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            return col;
        }

        static void Fit(RectTransform rt, bool horizontal, bool vertical)
        {
            var f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = horizontal ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = vertical ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
        }

        void BuildClock()
        {
            var panel = Ui.Panel(_root, "Clock");
            Ui.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(440f, 132f));
            _date = Ui.Label(panel.rectTransform, "", 26, TextAnchor.UpperLeft, Ui.Gold);
            Ui.Place(_date.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(410f, 34f));

            var speeds = Ui.Node("Speeds", panel.rectTransform);
            Ui.Place(speeds, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -46f), new Vector2(300f, 48f));
            Row(speeds, 6).childAlignment = TextAnchor.MiddleLeft;
            Sprite[] icons = { Icons.Pause, Icons.Play1, Icons.Play2, Icons.Play3, Icons.Skip };
            string[] tips = { "Dừng (Space)", "x1 · 6 ngày/giây (1)", "x5 (2)", "x20 (3)", "Tua · 10 năm/giây (4)" };
            for (int k = 0; k < icons.Length; k++)
            {
                int speed = k;
                var b = Ui.Button(speeds, icons[k], tips[k], () => _game.SetSpeed(speed), 46f);
                Size(b.Frame, 46f, 46f);
                _speedButtons.Add(b);
            }

            _summary = Ui.Label(panel.rectTransform, "", 19, TextAnchor.UpperLeft, Ui.Dim);
            Ui.Place(_summary.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -98f), new Vector2(420f, 28f));

            _ticker = Ui.Node("Ticker", _root);
            Ui.Place(_ticker, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -152f), new Vector2(560f, 200f));
            var tickerCol = Column(_ticker, 4);
            tickerCol.childForceExpandWidth = false;
        }

        void BuildToolbar()
        {
            var bar = Ui.Panel(_root, "Toolbar");
            var rt = bar.rectTransform;
            Ui.Place(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(900f, 150f));
            Row(rt, 14, 12).childAlignment = TextAnchor.MiddleCenter;
            Fit(rt, true, false);

            _inspectButton = Ui.Button(rt, Icons.Eye, ToolHelp[BrushTool.Inspect], () => SetTool(BrushTool.Inspect), 72f);
            Size(_inspectButton.Frame, 72f, 72f);

            var middle = Ui.Node("Middle", rt);
            Column(middle, 8).childAlignment = TextAnchor.MiddleCenter;
            _tabsRow = Ui.Node("Tabs", middle);
            Row(_tabsRow, 6).childAlignment = TextAnchor.MiddleLeft;
            Sprite[] tabIcons = { Icons.TerrainTile(Terrain.Mountain), Icons.Unit(Unit.Deer), Icons.Orb, Icons.Bolt, Icons.Layers, Icons.Globe };
            for (int k = 0; k < TabNames.Length; k++)
            {
                int tab = k;
                var b = Ui.Button(_tabsRow, tabIcons[k], TabNames[k], () => SelectTab(tab), 44f);
                Size(b.Frame, 44f, 44f);
                _tabButtons.Add(b);
            }
            _toolsRow = Ui.Node("Tools", middle);
            Row(_toolsRow, 6).childAlignment = TextAnchor.MiddleLeft;
            Height(_toolsRow, 64f);

            var sizeBox = Ui.Node("BrushSize", rt);
            Column(sizeBox, 2).childAlignment = TextAnchor.MiddleCenter;
            var plus = Ui.Button(sizeBox, Icons.Plus, "Cọ to hơn ( ] )", () => _game.Brush.Size = Mathf.Min(40, _game.Brush.Size + 1), 40f);
            Size(plus.Frame, 40f, 40f);
            _brushSize = Ui.Label(sizeBox, "6", 20, TextAnchor.MiddleCenter);
            Size(_brushSize, 40f, 24f);
            var minus = Ui.Button(sizeBox, Icons.Minus, "Cọ nhỏ hơn ( [ )", () => _game.Brush.Size = Mathf.Max(1, _game.Brush.Size - 1), 40f);
            Size(minus.Frame, 40f, 40f);
        }

        void SelectTab(int tab)
        {
            _tab = tab;
            for (int k = 0; k < _tabButtons.Count; k++) _tabButtons[k].SetSelected(k == tab);
            for (int k = _toolsRow.childCount - 1; k >= 0; k--) Destroy(_toolsRow.GetChild(k).gameObject);
            _toolButtons.Clear();
            _overlayButtons.Clear();
            _seedField = null;
            _labelsToggle = null;

            switch (tab)
            {
                case 0:
                    AddTool(BrushTool.Grass, Icons.TerrainTile(Terrain.Grass));
                    AddTool(BrushTool.Sand, Icons.TerrainTile(Terrain.Beach));
                    AddTool(BrushTool.Desert, Icons.TerrainTile(Terrain.Desert));
                    AddTool(BrushTool.Shallow, Icons.TerrainTile(Terrain.Shallow));
                    AddTool(BrushTool.DeepWater, Icons.TerrainTile(Terrain.DeepOcean));
                    AddTool(BrushTool.Hills, Icons.TerrainTile(Terrain.Hills));
                    AddTool(BrushTool.Mountain, Icons.TerrainTile(Terrain.Mountain));
                    AddTool(BrushTool.Snow, Icons.TerrainTile(Terrain.Snow));
                    break;
                case 1:
                    AddTool(BrushTool.Trees, Icons.Object(ObjectType.TreeOak));
                    AddTool(BrushTool.House, Icons.Object(ObjectType.House));
                    AddTool(BrushTool.SectHall, Icons.Object(ObjectType.SectHall));
                    AddTool(BrushTool.FoundVillage, Icons.Unit(Unit.Migrants));
                    AddTool(BrushTool.SpawnDeer, Icons.Unit(Unit.Deer));
                    AddTool(BrushTool.SpawnRabbit, Icons.Unit(Unit.Rabbit));
                    AddTool(BrushTool.SpawnWolf, Icons.Unit(Unit.Wolf));
                    AddTool(BrushTool.Erase, Icons.Erase);
                    break;
                case 2:
                    AddTool(BrushTool.LeyAdd, Icons.LeyLine);
                    AddTool(BrushTool.LeyErase, Icons.LeyBreak);
                    AddTool(BrushTool.QiInfuse, Icons.QiUp);
                    AddTool(BrushTool.QiDrain, Icons.QiDown);
                    break;
                case 3:
                    AddTool(BrushTool.GrantRoot, Icons.Seed);
                    AddTool(BrushTool.Bless, Icons.Star);
                    AddTool(BrushTool.Smite, Icons.Bolt);
                    break;
                case 4:
                    var grads = new[]
                    {
                        Icons.Gradient("none", new Color32(60, 66, 90, 255), new Color32(60, 66, 90, 255)),
                        Icons.Gradient("qi", new Color32(40, 10, 90, 255), new Color32(130, 255, 255, 255)),
                        Icons.Gradient("height", new Color32(20, 20, 20, 255), new Color32(240, 240, 240, 255)),
                        Icons.Gradient("temp", new Color32(40, 90, 255, 255), new Color32(255, 60, 30, 255)),
                        Icons.Gradient("moist", new Color32(170, 110, 50, 255), new Color32(30, 120, 255, 255)),
                        Icons.Gradient("forage", new Color32(200, 60, 40, 255), new Color32(60, 230, 70, 255)),
                        Icons.Gradient("owner", new Color32(200, 120, 90, 255), new Color32(90, 140, 220, 255)),
                    };
                    for (int k = 0; k < OverlayNames.Length; k++)
                    {
                        var mode = (OverlayMode)k;
                        var b = Ui.Button(_toolsRow, grads[k], $"Lớp phủ: {OverlayNames[k]} (Tab)", () => _game.Renderer.SetOverlay(mode), 58f);
                        Size(b.Frame, 58f, 58f);
                        _overlayButtons.Add((b, mode));
                    }
                    break;
                default:
                    BuildWorldTab();
                    break;
            }
        }

        void AddTool(BrushTool tool, Sprite icon)
        {
            var b = Ui.Button(_toolsRow, icon, ToolHelp.TryGetValue(tool, out var help) ? help : tool.ToString(), () => SetTool(tool), 58f);
            Size(b.Frame, 58f, 58f);
            _toolButtons.Add((b, tool));
        }

        void SetTool(BrushTool tool) => _game.Brush.Tool = tool;

        void BuildWorldTab()
        {
            var bg = Ui.Panel(_toolsRow, "Seed", Ui.BarSprite);
            Size(bg, 260f, 50f);
            var textRt = Ui.Node("Text", bg.rectTransform);
            Ui.Stretch(textRt, 12, 12, 6, 6);
            var text = textRt.gameObject.AddComponent<Text>();
            text.font = Ui.Font;
            text.fontSize = 22;
            text.color = Ui.Ink;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            var phRt = Ui.Node("Placeholder", bg.rectTransform);
            Ui.Stretch(phRt, 12, 12, 6, 6);
            var ph = phRt.gameObject.AddComponent<Text>();
            ph.font = Ui.Font;
            ph.fontSize = 22;
            ph.color = Ui.Dim;
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text = "Seed (chữ hoặc số)";
            _seedField = bg.gameObject.AddComponent<InputField>();
            _seedField.textComponent = text;
            _seedField.placeholder = ph;
            _seedField.characterLimit = 64;
            _seedField.text = _game.SeedText;
            bg.gameObject.AddComponent<Tooltip>().Text = "Seed: cùng seed luôn ra cùng thế giới";
            _seedField.onEndEdit.AddListener(v =>
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) _game.Generate(v);
            });

            var create = Ui.Button(_toolsRow, null, "Tạo lại thế giới từ seed này", () => _game.Generate(_seedField.text), 58f, "Tạo thế giới");
            Size(create.Frame, 150f, 50f);
            var random = Ui.Button(_toolsRow, null, "Tạo thế giới với seed ngẫu nhiên", () => _game.Generate(WorldBootstrap.RandomSeed()), 58f, "Ngẫu nhiên");
            Size(random.Frame, 130f, 50f);
            _labelsToggle = Ui.Button(_toolsRow, null, "Hiện/ẩn tên làng và cường giả trên bản đồ", () => ShowLabels = !ShowLabels, 58f, "Nhãn tên");
            Size(_labelsToggle.Frame, 120f, 50f);
        }

        // ---------------------------------------------------------------- windows

        sealed class Window
        {
            public RectTransform Root;
            public Text Body;
            public bool Open => Root.gameObject.activeSelf;
        }

        void BuildWindows()
        {
            var buttons = Ui.Node("WindowButtons", _root);
            Ui.Place(buttons, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(200f, 60f));
            Row(buttons, 6).childAlignment = TextAnchor.MiddleRight;

            _windowStack = Ui.Node("Windows", _root);
            Ui.Place(_windowStack, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -80f), new Vector2(460f, 800f));
            var col = Column(_windowStack, 8);
            col.childAlignment = TextAnchor.UpperRight;

            _ranking = MakeWindow("Bảng cường giả");
            _events = MakeWindow("Sự kiện");
            _stats = MakeWindow("Thống kê");
            AddWindowButton(buttons, Icons.Crown, "Bảng cường giả", _ranking);
            AddWindowButton(buttons, Icons.Scroll, "Sự kiện thế giới", _events);
            AddWindowButton(buttons, Icons.Chart, "Thống kê", _stats);
        }

        void AddWindowButton(RectTransform parent, Sprite icon, string tip, Window w)
        {
            var b = Ui.Button(parent, icon, tip, () =>
            {
                w.Root.gameObject.SetActive(!w.Open);
                _slowRefresh = 0f; // fill it straight away instead of waiting for the next refresh
            }, 56f);
            Size(b.Frame, 56f, 56f);
        }

        Window MakeWindow(string title)
        {
            var panel = Ui.Panel(_windowStack, title);
            var rt = panel.rectTransform;
            Column(rt, 6, 16); // height follows the content through the stack's layout

            var header = Ui.Node("Header", rt);
            Height(header, 30f);
            var t = Ui.Label(header, title.ToUpper(), 22, TextAnchor.MiddleLeft, Ui.Gold);
            Ui.Stretch(t.rectTransform);
            var w = new Window { Root = rt };
            var close = Ui.Button(header, Icons.Close, "Đóng", () => rt.gameObject.SetActive(false), 30f);
            Ui.Place(close.Frame.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(30f, 30f));

            w.Body = Ui.Label(rt, "", 19);
            rt.gameObject.SetActive(false);
            return w;
        }

        void BuildCard()
        {
            var panel = Ui.Panel(_root, "Card");
            _card = panel.rectTransform;
            Ui.Place(_card, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 20f), new Vector2(460f, 0f));
            Column(_card, 6, 16);
            Fit(_card, false, true);

            var header = Ui.Node("Header", _card);
            Height(header, 34f);
            _cardTitle = Ui.Label(header, "", 26, TextAnchor.MiddleLeft, Ui.Gold);
            Ui.Stretch(_cardTitle.rectTransform, 0, 36, 0, 0);
            var close = Ui.Button(header, Icons.Close, "Đóng (Esc)", () => _game.ClearSelection(), 30f);
            Ui.Place(close.Frame.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(30f, 30f));

            _cardBody = Ui.Label(_card, "", 19);
            _cardBar1Root = Ui.Bar(_card, new Color(0.45f, 0.85f, 1f), out _cardBar1).rectTransform;
            Height(_cardBar1Root, 18f);
            _cardBar1Root.gameObject.AddComponent<Tooltip>().Text = "Tu vi trong tiểu cảnh giới hiện tại";
            _cardBar2Root = Ui.Bar(_card, new Color(1f, 0.6f, 0.4f), out _cardBar2).rectTransform;
            Height(_cardBar2Root, 18f);
            _cardBar2Root.gameObject.AddComponent<Tooltip>().Text = "Tuổi so với thọ nguyên";

            var buttons = Ui.Node("Buttons", _card);
            Row(buttons, 8).childAlignment = TextAnchor.MiddleLeft;
            Height(buttons, 46f);
            _followButton = Ui.Button(buttons, null, "Camera bám theo nhân vật", () => _game.Follow = !_game.Follow, 46f, "Theo dõi");
            Size(_followButton.Frame, 140f, 44f);
            _card.gameObject.SetActive(false);
        }

        void BuildPointerHelpers()
        {
            var tip = Ui.Panel(_root, "Tooltip");
            _tooltip = tip.rectTransform;
            _tooltip.pivot = new Vector2(0f, 1f);
            tip.raycastTarget = false;
            Column(_tooltip, 0, 10);
            Fit(_tooltip, true, true);
            _tooltipText = Ui.Label(_tooltip, "", 19);
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _tooltip.gameObject.SetActive(false);

            _hoverHint = Ui.Label(_labelRoot, "", 18, TextAnchor.LowerLeft, Ui.Ink);
            _hoverHint.rectTransform.pivot = new Vector2(0f, 0f);
            _hoverHint.rectTransform.sizeDelta = new Vector2(520f, 30f);
        }

        // ---------------------------------------------------------------- per frame

        void LateUpdate()
        {
            if (_game == null || _game.Sim == null) return;
            var sim = _game.Sim;

            UpdateLabels();
            if (!_visible) return;

            _date.text = sim.Clock.DateText;
            for (int k = 0; k < _speedButtons.Count; k++) _speedButtons[k].SetSelected(sim.SpeedIndex == k);
            var cr = sim.Cultivation.CountByRealm;
            _summary.text = $"Dân {sim.Settlements.TotalPopulation:N0} · {sim.Settlements.AliveCount} làng · Tu sĩ {sim.Cultivation.AliveCount}" +
                            (cr[(int)Realm.NguyenAnh] + cr[(int)Realm.HoaThan] > 0 ? $" · Nguyên Anh {cr[(int)Realm.NguyenAnh]}" : "");
            UpdateTicker();

            _inspectButton.SetSelected(_game.Brush.Tool == BrushTool.Inspect);
            foreach (var (button, tool) in _toolButtons) button.SetSelected(_game.Brush.Tool == tool);
            foreach (var (button, mode) in _overlayButtons) button.SetSelected(_game.Renderer.Overlay == mode);
            if (_labelsToggle != null) _labelsToggle.SetSelected(ShowLabels);
            _brushSize.text = _game.Brush.Size.ToString();

            _slowRefresh -= Time.unscaledDeltaTime;
            if (_slowRefresh <= 0f)
            {
                _slowRefresh = 0.25f;
                if (_ranking.Open) _ranking.Body.text = RankingText();
                if (_events.Open) _events.Body.text = EventsText();
            }
            if (_stats.Open) _stats.Body.text = StatsText();

            UpdateCard();
            UpdateTooltip();
        }

        void UpdateTicker()
        {
            var events = _game.Sim.Events;
            long fresh = events.TotalAdded - _tickerSeen;
            _tickerSeen = events.TotalAdded;
            var recent = events.Recent;
            for (int k = Mathf.Max(0, recent.Count - (int)Mathf.Min(fresh, recent.Count)); k < recent.Count; k++)
            {
                if (recent[k].Importance < 2) continue;
                var t = Ui.Label(_ticker, recent[k].Text, 20, TextAnchor.UpperLeft, recent[k].Importance >= 3 ? Ui.Gold : Ui.Ink);
                Element(t).preferredWidth = 560f; // height follows the wrapped text
                _tickerLines.Add((t, Time.unscaledTime));
                if (_tickerLines.Count > 4)
                {
                    Destroy(_tickerLines[0].text.gameObject);
                    _tickerLines.RemoveAt(0);
                }
            }
            for (int k = _tickerLines.Count - 1; k >= 0; k--)
            {
                float age = Time.unscaledTime - _tickerLines[k].shownAt;
                var (text, _) = _tickerLines[k];
                if (age > 8f)
                {
                    Destroy(text.gameObject);
                    _tickerLines.RemoveAt(k);
                    continue;
                }
                var c = text.color;
                c.a = Mathf.Clamp01((8f - age) / 1.5f);
                text.color = c;
            }
        }

        string RankingText()
        {
            var sim = _game.Sim;
            _rank.Clear();
            foreach (var c in sim.Cultivation.All)
                if (c.Alive) _rank.Add(c);
            _rank.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            var sb = new StringBuilder();
            for (int k = 0; k < Mathf.Min(8, _rank.Count); k++)
            {
                var c = _rank[k];
                string where = c.SectId >= 0 ? $"{sim.Cultivation.Role(c)} {sim.Cultivation.SectName(c)}" : sim.Cultivation.Role(c);
                sb.Append($"<color=#ffd873>{k + 1}. {c.Title}</color> · {c.RealmText}{(c.Demonic ? " <color=#ff7070>(ma tu)</color>" : "")}\n");
                sb.Append($"     {where} · {c.AgeYears(sim.Clock.Tick):0}/{c.LifespanYears} tuổi\n");
            }
            return sb.ToString().TrimEnd();
        }

        string EventsText()
        {
            var events = _game.Sim.Events.Recent;
            var sb = new StringBuilder();
            int shown = 0;
            for (int k = events.Count - 1; k >= 0 && shown < 8; k--)
            {
                var ev = events[k];
                if (ev.Importance < 1) continue;
                string color = ev.Importance >= 3 ? "#ffd873" : ev.Importance == 2 ? "#ffffff" : "#b8bccc";
                sb.Append($"<color=#8890a8>Năm {ev.Tick / SimClock.DaysPerYear + 1}</color> <color={color}>{ev.Text}</color>\n");
                shown++;
            }
            return shown == 0 ? "Chưa có chuyện gì đáng kể." : sb.ToString().TrimEnd();
        }

        string StatsText()
        {
            var sim = _game.Sim;
            var w = _game.World;
            var cr = sim.Cultivation.CountByRealm;
            var wild = sim.Wildlife;
            var sb = new StringBuilder();
            sb.Append($"Phàm nhân {sim.Settlements.TotalPopulation:N0} · {sim.Settlements.AliveCount} làng · {sim.Settlements.MigrantGroups} đoàn di dân\n");
            sb.Append($"Tu sĩ {sim.Cultivation.AliveCount}: Luyện Khí {cr[1]} · Trúc Cơ {cr[2]} · Kết Đan {cr[3]} · Nguyên Anh {cr[4]} · Hóa Thần {cr[5]}\n");
            sb.Append($"Hoang dã: Hươu {wild.Total(Species.Deer):N0} · Thỏ {wild.Total(Species.Rabbit):N0} · Sói {wild.Total(Species.Wolf):N0}\n");

            int hx = _game.HoverX, hy = _game.HoverY;
            if (w.InBounds(hx, hy))
            {
                int i = w.Idx(hx, hy);
                float tempC = -20f + (w.Temperature[i] / 255f + sim.Clock.SeasonalTemperatureOffset) * 60f;
                sb.Append($"\n<color=#ffd873>Ô ({hx}, {hy})</color> {TerrainInfo.Names[(int)w.Terrain[i]]} · cao {w.Height[i]:0.00} · {tempC:0}°C · ẩm {w.Moisture[i] * 100 / 255}%\n");
                sb.Append($"Linh khí {sim.Qi.SampleQi(hx, hy):0} / trần {w.QiCap[i]}{(w.LeyLine[i] ? " · LINH MẠCH" : "")} · cỏ {sim.Forage.At(hx, hy):0}\n");
                int region = wild.RegionOf(hx, hy);
                sb.Append($"Vùng: hươu {wild.At(Species.Deer, region):0} · thỏ {wild.At(Species.Rabbit, region):0} · sói {wild.At(Species.Wolf, region):0}\n");
            }

            var r = _game.Renderer;
            sb.Append($"\n<color=#8890a8>FPS {_game.Fps:0} · {_game.Camera.PixelsPerCell:0.0} px/ô · chunk {r.ResidentChunks}/{r.ChunkCount} · vẽ lại {r.RedrawsLastFrame}\n" +
                      $"{sim.TicksLastFrame} tick/frame · {w.Objects.AliveCount:N0} vật thể · {sim.Log.Count} lệnh · seed {w.Seed} ({_game.GenerationMs:0} ms)</color>");
            return sb.ToString();
        }

        void UpdateCard()
        {
            var sim = _game.Sim;
            var c = _game.Selected;
            var s = _game.SelectedSettlement;
            bool show = c != null || s != null;
            if (_card.gameObject.activeSelf != show) _card.gameObject.SetActive(show);
            if (!show) return;

            if (c != null)
            {
                long tick = sim.Clock.Tick;
                _cardTitle.text = c.Title + (c.Demonic ? " <color=#ff7070>· ma tu</color>" : "");
                var sb = new StringBuilder();
                sb.Append(c.SectId >= 0 ? $"{sim.Cultivation.Role(c)} · {sim.Cultivation.SectName(c)}\n" : $"{sim.Cultivation.Role(c)}\n");
                if (!c.Alive)
                {
                    sb.Append("<color=#ff9090>Đã vẫn lạc.</color>\n");
                }
                else
                {
                    sb.Append($"<color=#ffd873>{c.RealmText}</color> · {c.Activity}\n");
                    sb.Append($"Tuổi {c.AgeYears(tick):0} / thọ nguyên {c.LifespanYears} năm\n");
                    sb.Append($"{SpiritRoots.Kind(c.Roots)} ({SpiritRoots.Elements(c.Roots)}) · tốc độ ×{SpiritRoots.SpeedMultiplier(c.Roots):0.0}\n");
                    sb.Append($"Ngộ tính {c.Comprehension * 100f:0} · Tâm cảnh {c.DaoHeart * 100f:0} · Khí vận {c.Luck * 100f:0}\n");
                    sb.Append($"Linh khí nơi ở {sim.Qi.SampleQi((int)c.HomeX, (int)c.HomeY):0} (cần {Realms.RequiredQi[(int)c.Realm]:0})" +
                              (c.FailedAttempts > 0 ? $" · đột phá thất bại {c.FailedAttempts} lần" : ""));
                }
                int n = 0;
                var events = sim.Events.Recent;
                for (int k = events.Count - 1; k >= 0 && n < 5; k--)
                {
                    if (!events[k].Text.Contains(c.Name)) continue;
                    if (n == 0) sb.Append("\n\n<color=#ffd873>Chuyện đời</color>");
                    sb.Append($"\n<color=#8890a8>Năm {events[k].Tick / SimClock.DaysPerYear + 1}</color> {events[k].Text}");
                    n++;
                }
                _cardBody.text = sb.ToString();
                _cardBar1Root.gameObject.SetActive(c.Alive);
                _cardBar2Root.gameObject.SetActive(c.Alive);
                if (c.Alive)
                {
                    _cardBar1.fillAmount = Mathf.Clamp01(c.Progress / Realms.Need(c.Realm, c.Stage));
                    _cardBar2.fillAmount = Mathf.Clamp01(c.AgeYears(tick) / c.LifespanYears);
                }
                _followButton.Frame.gameObject.SetActive(c.Alive);
                _followButton.SetSelected(_game.Follow);
                return;
            }

            int pop = Mathf.Max(1, s.Population);
            _cardTitle.text = s.Name;
            var body = new StringBuilder();
            if (!s.Alive) body.Append("<color=#ff9090>Đã bị bỏ hoang.</color>\n");
            else
            {
                body.Append($"{s.Population} người · {s.Houses.Count} nhà · {s.Farms.Count} ô ruộng\n");
                body.Append($"Lương thực {s.Food / pop:0.0} tháng · năm qua sinh {s.BirthsLastYear}, mất {s.DeathsLastYear}\n");
            }
            body.Append($"Lập năm {s.FoundedTick / SimClock.DaysPerYear + 1}" + (s.ParentId >= 0 ? $" bởi di dân từ {sim.Settlements.All[s.ParentId].Name}" : ""));
            if (s.Sect)
            {
                var count = new int[(int)Realm.Count];
                foreach (var m in sim.Cultivation.All)
                    if (m.Alive && m.SectId == s.Id) count[(int)m.Realm]++;
                var master = sim.Cultivation.MasterOf(s.Id);
                body.Append($"\n\n<color=#ffd873>Tông môn</color>{(master != null ? $" · tông chủ {master.Title} ({master.RealmText})" : "")}\n");
                body.Append($"Luyện Khí {count[1]} · Trúc Cơ {count[2]} · Kết Đan {count[3]} · Nguyên Anh {count[4]} · Hóa Thần {count[5]}");
            }
            _cardBody.text = body.ToString();
            _cardBar1Root.gameObject.SetActive(false);
            _cardBar2Root.gameObject.SetActive(false);
            _followButton.Frame.gameObject.SetActive(false);
        }

        void UpdateTooltip()
        {
            string text = Tooltip.Current;
            bool show = !string.IsNullOrEmpty(text);
            if (_tooltip.gameObject.activeSelf != show) _tooltip.gameObject.SetActive(show);
            if (!show || Mouse.current == null) return;
            _tooltipText.text = text;
            _tooltip.SetAsLastSibling();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, Mouse.current.position.ReadValue(), null, out var local);
            // Keep it on screen: flip above the pointer near the bottom, left of it near the right edge.
            var size = _tooltip.rect.size;
            var half = _root.rect.size * 0.5f;
            float x = local.x + 18f, y = local.y + (local.y < -half.y + 260f ? size.y + 18f : -18f);
            if (x + size.x > half.x) x = local.x - size.x - 12f;
            _tooltip.anchoredPosition = new Vector2(x, y);
            _tooltip.anchorMin = _tooltip.anchorMax = new Vector2(0.5f, 0.5f);
        }

        // World-space labels: village names and Kết Đan+ cultivators out on the map; plus a hint under the pointer.
        void UpdateLabels()
        {
            var sim = _game.Sim;
            var cam = _game.Camera.Cam;
            int used = 0;
            if (ShowLabels && _game.Camera.PixelsPerCell >= 3f)
            {
                foreach (var s in sim.Settlements.All)
                {
                    if (!s.Alive) continue;
                    PlaceLabel(ref used, cam, new Vector3(s.X + 0.5f, s.Y + 5f, 0f), $"{s.Name} ({s.Population})", Ui.Ink, 20);
                }
                var e = sim.Entities;
                foreach (var c in sim.Cultivation.All)
                {
                    if (c.Realm < Realm.KetDan || !sim.Cultivation.IsShownOnMap(c)) continue;
                    PlaceLabel(ref used, cam, new Vector3(e.X[c.Entity], e.Y[c.Entity] + 2.6f, 0f), $"{c.Title} · {Realms.Names[(int)c.Realm]}",
                        c.Demonic ? new Color(1f, 0.5f, 0.5f) : Ui.Gold, 18);
                }
            }
            for (int k = used; k < _labels.Count; k++)
                if (_labels[k].gameObject.activeSelf) _labels[k].gameObject.SetActive(false);

            string hint = null;
            if (!PointerOverUI && _game.World.InBounds(_game.HoverX, _game.HoverY))
            {
                var c = sim.Cultivation.FindShownNear(_game.HoverWorld.x, _game.HoverWorld.y, 1.5f);
                if (c != null) hint = $"{c.Title} · {c.RealmText} · {c.Activity}";
                else
                {
                    var s = sim.Settlements.Owning(_game.World.Idx(_game.HoverX, _game.HoverY));
                    if (s != null && s.Alive) hint = $"{s.Name} · {s.Population} người";
                }
            }
            _hoverHint.gameObject.SetActive(hint != null && Mouse.current != null);
            if (hint != null && Mouse.current != null)
            {
                _hoverHint.text = hint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelRoot, Mouse.current.position.ReadValue(), null, out var local);
                _hoverHint.rectTransform.anchorMin = _hoverHint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _hoverHint.rectTransform.anchoredPosition = local + new Vector2(18f, 12f);
            }
        }

        void PlaceLabel(ref int used, Camera cam, Vector3 world, string text, Color color, int size)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) return;
            if (used == _labels.Count)
            {
                var t = Ui.Label(_labelRoot, "", size, TextAnchor.MiddleCenter);
                t.rectTransform.sizeDelta = new Vector2(420f, 28f);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _labels.Add(t);
            }
            var label = _labels[used++];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            label.text = text;
            label.color = color;
            label.fontSize = size;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_labelRoot, sp, null, out var local);
            label.rectTransform.anchoredPosition = local;
        }

        public void OnWorldChanged()
        {
            _tickerSeen = _game.Sim.Events.TotalAdded;
            foreach (var (text, _) in _tickerLines) Destroy(text.gameObject);
            _tickerLines.Clear();
            if (_seedField != null) _seedField.text = _game.SeedText;
        }
    }
}
