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
        Window _ranking, _events, _stats, _powers;
        ChronicleWindow _chronicle;
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

        static readonly string[] TabNames = { "Địa hình", "Sinh linh", "Linh khí", "Thiên Đạo", "Thiên tai", "Lớp phủ", "Thế giới" };
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
            { BrushTool.GrantRoot, "Ban linh căn — bấm vào một người: tu sĩ được tẩy luyện linh căn, phàm nhân trong làng thức tỉnh linh căn" },
            { BrushTool.Bless, "Ban cơ duyên — bấm vào tu sĩ: tu vi tăng mạnh, thêm thọ; bấm vào làng: mùa màng bội thu" },
            { BrushTool.Smite, "Thiên phạt — sét đánh xuống người được chọn (hồn phi phách tán) hoặc xuống chỗ bấm" },
            { BrushTool.Tribulation, "Thiên kiếp — bấm vào một tu sĩ: sống sót thì phá bình cảnh hoặc được lôi kiếp tôi luyện, không thì vẫn lạc. " +
                                     "Sét đánh cả vùng quanh đó và để lại lôi địa (linh khí dày, phàm nhân tránh xa) vài trăm năm" },
            { BrushTool.Earthquake, "Động đất — nhà sập, người chết, linh mạch trong vùng có thể đứt gãy (cọ to thì vùng rộng)" },
            { BrushTool.Eruption, "Núi lửa — một ngọn núi lửa mọc lên, dung nham chảy ra rồi nguội thành đá sau nhiều năm; tro bụi phủ các làng quanh đó" },
            { BrushTool.Flood, "Lũ lụt — vùng trũng ngập nước vài tháng: ruộng mất, người và thú chết đuối, rồi nước rút" },
            { BrushTool.Drought, "Hạn hán — một vùng rộng mất mùa 1–2 năm, cỏ khô héo; nạn đói kéo theo di dân" },
            { BrushTool.Plague, "Ôn dịch — bấm vào một làng: dịch kéo dài vài tháng và có thể lan sang làng lân cận" },
            { BrushTool.BeastTide, "Thú triều — hàng trăm yêu lang tràn vào các làng quanh đó; tông môn che chở thì đỡ thiệt hại" },
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

        public void ToggleChronicle() => _chronicle?.Toggle();

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
            BuildWatchList();
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
            Sprite[] tabIcons = { Icons.TerrainTile(Terrain.Mountain), Icons.Unit(Unit.Deer), Icons.Orb, Icons.Bolt, Icons.Volcano, Icons.Layers, Icons.Globe };
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
                    AddTool(BrushTool.Tribulation, Icons.Tribulation);
                    break;
                case 4:
                    AddTool(BrushTool.Earthquake, Icons.Quake);
                    AddTool(BrushTool.Eruption, Icons.Volcano);
                    AddTool(BrushTool.Flood, Icons.Wave);
                    AddTool(BrushTool.Drought, Icons.Sun);
                    AddTool(BrushTool.Plague, Icons.Skull);
                    AddTool(BrushTool.BeastTide, Icons.Paw);
                    break;
                case 5:
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
            Ui.Place(buttons, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(320f, 60f));
            Row(buttons, 6).childAlignment = TextAnchor.MiddleRight;

            _windowStack = Ui.Node("Windows", _root);
            Ui.Place(_windowStack, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -80f), new Vector2(460f, 800f));
            var col = Column(_windowStack, 8);
            col.childAlignment = TextAnchor.UpperRight;

            _ranking = MakeWindow("Bảng cường giả");
            BuildRankingRows();
            _events = MakeWindow("Sự kiện");
            _stats = MakeWindow("Thống kê");
            _powers = MakeWindow("Thế lực");
            _chronicle = new ChronicleWindow(_root, _game);
            var book = Ui.Button(buttons, Icons.Book, "Biên niên sử: sử sách, truyền kỳ, danh nhân (H)", () => _chronicle.Toggle(), 56f);
            Size(book.Frame, 56f, 56f);
            AddWindowButton(buttons, Icons.Banner, "Thế lực: tông môn, lãnh thổ, chiến tranh", _powers);
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
            _followButton = Ui.Button(buttons, null, "Camera bám theo nhân vật", () => _game.Follow = !_game.Follow, 46f, "Camera");
            Size(_followButton.Frame, 110f, 44f);
            _watchButton = Ui.Button(buttons, null, "Thêm vào danh sách theo dõi: người này sẽ sống như nhân vật chính", ToggleWatchSelected, 46f, "☆ Theo dõi");
            Size(_watchButton.Frame, 150f, 44f);
            // Thiên Đạo acts on exactly the one shown on the card.
            var acts = new[] { (DivineAct.GrantRoot, Icons.Seed), (DivineAct.Bless, Icons.Star), (DivineAct.Smite, Icons.Bolt), (DivineAct.Tribulation, Icons.Tribulation) };
            for (int k = 0; k < acts.Length; k++)
            {
                var act = acts[k].Item1;
                _divineButtons[k] = Ui.Button(buttons, acts[k].Item2, DivineTipPerson[k], () => _game.ActOnSelected(act), 44f);
                Size(_divineButtons[k].Frame, 44f, 44f);
            }
            _card.gameObject.SetActive(false);
        }

        readonly Ui.IconButton[] _divineButtons = new Ui.IconButton[4];

        static readonly string[] DivineTipPerson =
        {
            "Ban linh căn: tẩy luyện linh căn người này lên Thiên / Dị linh căn",
            "Ban cơ duyên: tu vi tăng mạnh, khí vận tràn đầy, thêm 20 năm thọ",
            "Thiên phạt: sét đánh xuống, hồn phi phách tán",
            "Thiên kiếp: vượt qua thì phá bình cảnh (hoặc được tôi luyện), thất bại thì vẫn lạc; nơi đó hóa lôi địa"
        };

        static readonly string[] DivineTipVillage =
        {
            "Ban linh căn: điểm hóa một phàm nhân trưởng thành trong làng",
            "Ban cơ duyên: mùa màng bội thu",
            "Thiên phạt: thiên lôi đánh xuống làng",
            ""
        };

        void ShowDivineButtons(bool show, bool village)
        {
            for (int k = 0; k < _divineButtons.Length; k++)
            {
                var b = _divineButtons[k];
                bool on = show && (!village || DivineTipVillage[k].Length > 0); // thiên kiếp is for cultivators only
                if (b.Frame.gameObject.activeSelf != on) b.Frame.gameObject.SetActive(on);
                if (!on) continue;
                var tip = b.Frame.GetComponent<Tooltip>();
                if (tip != null) tip.Text = village ? DivineTipVillage[k] : DivineTipPerson[k];
            }
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
                if (_ranking.Open) UpdateRankingRows();
                UpdateWatchList();
                if (_events.Open) _events.Body.text = EventsText();
                if (_powers.Open) _powers.Body.text = PowersText();
            }
            if (_stats.Open) _stats.Body.text = StatsText();

            UpdateCard();
            _chronicle.Tick(Time.unscaledDeltaTime);
            UpdateToast();
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
                bool watched = IsWatchedEvent(recent[k]);
                if (recent[k].Importance < 2 && !watched) continue;
                var t = Ui.Label(_ticker, watched ? "★ " + recent[k].Text : recent[k].Text, 20, TextAnchor.UpperLeft, watched ? WatchColor : recent[k].Importance >= 3 ? Ui.Gold : Ui.Ink);
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

        // ---------------------------------------------------------------- watch list (nhân vật chính)

        static readonly Color WatchColor = new Color(1f, 0.92f, 0.55f);
        const int WatchRows = 8;
        Ui.IconButton _watchButton;
        RectTransform _watchPanel;
        Text _watchTitle;
        readonly Ui.IconButton[] _watchRows = new Ui.IconButton[WatchRows];
        readonly Ui.IconButton[] _watchRemove = new Ui.IconButton[WatchRows];
        readonly Cultivator[] _watchWho = new Cultivator[WatchRows];

        bool IsWatchedEvent(WorldEvent ev)
        {
            var all = _game.Sim.Cultivation.All;
            return (ev.A >= 0 && ev.A < all.Count && all[ev.A].Watched) || (ev.B >= 0 && ev.B < all.Count && all[ev.B].Watched);
        }

        void ToggleWatchSelected()
        {
            var c = _game.Selected;
            if (c == null) return;
            _game.Sim.Enqueue(new WatchCommand(c.Index, !c.Watched));
            if (!c.Watched) ShowToast($"{c.Title} được thêm vào danh sách theo dõi. Từ nay người này tự quyết định cuộc đời mình.");
        }

        // Bottom-right: everyone the player watches. Click a name to follow them; × to stop watching.
        void BuildWatchList()
        {
            var panel = Ui.Panel(_root, "WatchList");
            _watchPanel = panel.rectTransform;
            Ui.Place(_watchPanel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(400f, 0f));
            Column(_watchPanel, 4, 10);
            Fit(_watchPanel, false, true);
            _watchTitle = Ui.Label(_watchPanel, "", 18, TextAnchor.MiddleLeft, Ui.Gold);
            Height(_watchTitle, 24f);
            for (int k = 0; k < WatchRows; k++)
            {
                int row = k;
                var line = Ui.Node("Row", _watchPanel);
                Row(line, 4).childAlignment = TextAnchor.MiddleLeft;
                Height(line, 48f);
                var b = Ui.Button(line, null, "Bấm để camera theo người này", () => FocusWatched(row), 46f, "");
                b.Caption.alignment = TextAnchor.MiddleLeft;
                b.Caption.fontSize = 16;
                Ui.Stretch(b.Caption.rectTransform, 10, 6, 2, 2);
                Size(b.Frame, 324f, 46f);
                var x = Ui.Button(line, Icons.Close, "Bỏ theo dõi", () => Unwatch(row), 40f);
                Size(x.Frame, 40f, 40f);
                _watchRows[k] = b;
                _watchRemove[k] = x;
            }
            _watchPanel.gameObject.SetActive(false);
        }

        void UpdateWatchList()
        {
            var sim = _game.Sim;
            int n = 0, total = 0;
            foreach (var c in sim.Cultivation.All)
            {
                if (!c.Watched) continue;
                total++;
                if (n < WatchRows) _watchWho[n++] = c;
            }
            for (int k = n; k < WatchRows; k++) _watchWho[k] = null;
            bool show = total > 0;
            if (_watchPanel.gameObject.activeSelf != show) _watchPanel.gameObject.SetActive(show);
            if (!show) return;
            _watchTitle.text = $"★ Theo dõi ({total})";
            for (int k = 0; k < WatchRows; k++)
            {
                var c = _watchWho[k];
                var row = _watchRows[k].Frame.transform.parent.gameObject;
                if (row.activeSelf != (c != null)) row.SetActive(c != null);
                if (c == null) continue;
                _watchRows[k].SetSelected(_game.Selected == c);
                string state = !c.Alive ? $"<color=#ff9090>đã vẫn lạc năm {c.DeathTick / SimClock.DaysPerYear + 1}</color>"
                    : sim.Cultivation.IsShownOnMap(c) ? $"<color=#9fe0a0>{c.Activity}</color>" : $"<color=#b8bccc>{c.Activity}</color>";
                _watchRows[k].Caption.text = $"<color=#ffe68c>{c.Name}</color> · {c.RealmText}\n<size=14>{state}</size>";
            }
        }

        void FocusWatched(int row)
        {
            var c = _watchWho[row];
            if (c == null) return;
            if (!c.Alive)
            {
                ShowToast($"{c.Title} đã vẫn lạc. Tiểu sử vẫn còn trong Biên niên sử (H).");
                return;
            }
            // Watched cultivators are drawn even at home, so there is always someone to follow.
            _game.FocusCultivator(c, true);
        }

        void Unwatch(int row)
        {
            var c = _watchWho[row];
            if (c != null) _game.Sim.Enqueue(new WatchCommand(c.Index, false));
        }

        const int RankRows = 8;
        readonly Ui.IconButton[] _rankRows = new Ui.IconButton[RankRows];
        readonly Cultivator[] _rankWho = new Cultivator[RankRows];

        // Each row of the ranking is a button: click to select that expert and follow them on the map.
        void BuildRankingRows()
        {
            _ranking.Body.text = "<color=#8890a8>Bấm vào một cao thủ để theo dõi vị trí hiện tại.</color>";
            _ranking.Body.fontSize = 16;
            for (int k = 0; k < RankRows; k++)
            {
                int row = k;
                var b = Ui.Button(_ranking.Root, null, "Theo dõi cao thủ này trên bản đồ", () => FocusRanked(row), 56f, "");
                b.Caption.alignment = TextAnchor.MiddleLeft;
                b.Caption.fontSize = 17;
                b.Caption.supportRichText = true;
                Ui.Stretch(b.Caption.rectTransform, 12, 8, 2, 2);
                Height(b.Frame, 54f);
                _rankRows[k] = b;
            }
        }

        void UpdateRankingRows()
        {
            var sim = _game.Sim;
            _rank.Clear();
            foreach (var c in sim.Cultivation.All)
                if (c.Alive) _rank.Add(c);
            _rank.Sort((a, b) => b.Rank != a.Rank ? b.Rank.CompareTo(a.Rank) : a.Index.CompareTo(b.Index));
            for (int k = 0; k < RankRows; k++)
            {
                var c = k < _rank.Count ? _rank[k] : null;
                _rankWho[k] = c;
                var b = _rankRows[k];
                if (b.Frame.gameObject.activeSelf != (c != null)) b.Frame.gameObject.SetActive(c != null);
                if (c == null) continue;
                b.SetSelected(_game.Selected == c);
                string where = c.SectId >= 0 ? $"{sim.Cultivation.Role(c)} {sim.Cultivation.SectName(c)}" : sim.Cultivation.Role(c);
                string doing = sim.Cultivation.IsShownOnMap(c) ? $"<color=#9fe0a0>{c.Activity}</color>" : "<color=#8890a8>đang bế quan</color>";
                b.Caption.text = $"<color=#ffd873>{k + 1}. {c.Title}</color> · {c.RealmText}{(c.Demonic ? " <color=#ff7070>ma tu</color>" : "")}\n" +
                                 $"<size=15>{where} · {doing}</size>";
            }
        }

        void FocusRanked(int row)
        {
            var c = _rankWho[row];
            if (c == null || !c.Alive) return;
            if (_game.FocusCultivator(c, c.Watched)) return; // nhân vật chính are on the map even in seclusion
            string place = c.SectId >= 0 ? $"tại {_game.Sim.Cultivation.SectName(c)}" : "trong động phủ";
            ShowToast($"{c.Title} đang bế quan {place}, không xuất hiện trên bản đồ.");
        }

        // ---------------------------------------------------------------- toast

        Text _toast;
        float _toastUntil;

        public void ShowToast(string text)
        {
            if (_toast == null)
            {
                var panel = Ui.Panel(_root, "Toast");
                panel.raycastTarget = false;
                var rt = panel.rectTransform;
                Ui.Place(rt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(720f, 0f));
                Column(rt, 0, 14);
                Fit(rt, false, true);
                _toast = Ui.Label(rt, "", 21, TextAnchor.MiddleCenter, Ui.Gold);
            }
            _toast.text = text;
            _toast.transform.parent.gameObject.SetActive(true);
            _toast.transform.parent.SetAsLastSibling();
            _toastUntil = Time.unscaledTime + 3.5f;
        }

        void UpdateToast()
        {
            if (_toast == null || !_toast.transform.parent.gameObject.activeSelf) return;
            if (Time.unscaledTime > _toastUntil) _toast.transform.parent.gameObject.SetActive(false);
        }

        readonly List<Faction> _powerRank = new List<Faction>();
        readonly List<BattleInfo> _battleInfo = new List<BattleInfo>();
        readonly List<HistoryRecord> _bio = new List<HistoryRecord>();
        readonly List<Relation> _rels = new List<Relation>();

        static string Hex(Color32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";

        string PowersText()
        {
            var sim = _game.Sim;
            var fs = sim.Factions;
            _powerRank.Clear();
            foreach (var f in fs.All)
                if (f.Alive) _powerRank.Add(f);
            _powerRank.Sort((a, b) => b.Power != a.Power ? b.Power.CompareTo(a.Power) : a.Id.CompareTo(b.Id));
            var sb = new StringBuilder();
            sb.Append($"<color=#8890a8>{fs.AliveCount} thế lực · {fs.WarCount} cuộc chiến đang diễn ra</color>\n");
            for (int k = 0; k < Mathf.Min(7, _powerRank.Count); k++)
            {
                var f = _powerRank[k];
                var master = sim.Cultivation.MasterOf(f.Id);
                sb.Append($"<color={Hex(f.Color)}>■</color> <color=#ffd873>{fs.NameOf(f.Id)}</color>{(f.Demonic ? " <color=#ff7070>ma</color>" : "")}" +
                          $" · {f.Members} tu sĩ" + (master != null ? $" · {Realms.Names[(int)master.Realm]}" : "") + "\n");
                sb.Append($"   <color=#b8bccc>{f.Tiles} vùng · {f.LeyTiles} linh mạch · {f.Treasury:N0} linh thạch</color>\n");
                string ties = TiesText(f.Id, 1);
                if (ties.Length > 0) sb.Append($"   {ties}\n");
            }
            return sb.ToString().TrimEnd();
        }

        // "Chiến: A, B · Minh: C" for one faction; at most `max` names per group.
        string TiesText(int id, int max)
        {
            var fs = _game.Sim.Factions;
            fs.RelationsOf(id, _rels);
            var war = new StringBuilder();
            var ally = new StringBuilder();
            var foe = new StringBuilder();
            int nw = 0, na = 0, nf = 0;
            foreach (var r in _rels)
            {
                string name = fs.NameOf(r.Other(id));
                switch (r.Stance)
                {
                    case Stance.War: if (nw++ < max) war.Append(nw > 1 ? ", " : "").Append(name); break;
                    case Stance.Allied: if (na++ < max) ally.Append(na > 1 ? ", " : "").Append(name); break;
                    case Stance.Hostile: if (nf++ < max) foe.Append(nf > 1 ? ", " : "").Append(name); break;
                }
            }
            var sb = new StringBuilder();
            if (nw > 0) sb.Append($"<color=#ff7070>Chiến: {war}{(nw > max ? $" +{nw - max}" : "")}</color>");
            if (na > 0) sb.Append(sb.Length > 0 ? " · " : "").Append($"<color=#9fe0a0>Minh: {ally}{(na > max ? $" +{na - max}" : "")}</color>");
            if (nf > 0) sb.Append(sb.Length > 0 ? " · " : "").Append($"<color=#e0a070>Thù: {foe}{(nf > max ? $" +{nf - max}" : "")}</color>");
            return sb.ToString();
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
            sb.Append($"Thế lực {sim.Factions.AliveCount} · {sim.Factions.WarCount} cuộc chiến · lập tông {sim.Events.CountByKind[(int)EventKind.Founding]} · " +
                      $"ly khai {sim.Events.CountByKind[(int)EventKind.Schism]} · diệt môn {sim.Events.CountByKind[(int)EventKind.Destruction]}\n");
            sb.Append($"Hoang dã: Hươu {wild.Total(Species.Deer):N0} · Thỏ {wild.Total(Species.Rabbit):N0} · Sói {wild.Total(Species.Wolf):N0}\n");
            var dis = sim.Disasters;
            int scars = 0, volcanoes = 0;
            foreach (var l in dis.Landmarks)
                if (l.Alive) { if (l.Kind == Landmark.Thunder) scars++; else volcanoes++; }
            sb.Append($"Thiên tai: {dis.DroughtCount} vùng hạn · {dis.EpidemicCount} ổ dịch · {dis.FloodedCells:N0} ô ngập · {dis.LavaCells:N0} ô dung nham · " +
                      $"{scars} lôi địa · {volcanoes} núi lửa · {sim.Events.CountByKind[(int)EventKind.Calamity]} sự kiện\n");

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
            var sel = _game.Selection;
            var c = _game.Selected;
            var s = _game.SelectedSettlement;
            bool show = sel.Kind != InspectKind.None;
            if (_card.gameObject.activeSelf != show) _card.gameObject.SetActive(show);
            if (!show) return;

            if (sel.Kind != InspectKind.Cultivator && sel.Kind != InspectKind.Settlement)
            {
                _cardTitle.text = InspectTitle(sel);
                _cardBody.text = InspectBody(sel);
                _cardBar1Root.gameObject.SetActive(false);
                _cardBar2Root.gameObject.SetActive(false);
                _followButton.Frame.gameObject.SetActive(sel.Kind == InspectKind.Migrants);
                ShowDivineButtons(false, false);
                _watchButton.Frame.gameObject.SetActive(false);
                _followButton.SetSelected(_game.Follow);
                return;
            }

            if (c != null)
            {
                long tick = sim.Clock.Tick;
                _cardTitle.text = c.Title + (c.Demonic ? " <color=#ff7070>· ma tu</color>" : "");
                var sb = new StringBuilder();
                sb.Append(c.SectId >= 0 ? $"{sim.Cultivation.Role(c)} · {sim.Cultivation.SectName(c)}\n" : $"{sim.Cultivation.Role(c)}\n");
                if (!c.Alive)
                {
                    sb.Append($"<color=#ff9090>Đã vẫn lạc năm {c.DeathTick / SimClock.DaysPerYear + 1}" +
                              (c.KilledBy >= 0 ? $" dưới tay {sim.Cultivation.All[c.KilledBy].Title}" : "") + ".</color>\n");
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
                // Ties: sư phụ, huyết thù, the tally.
                var master = sim.Cultivation.MasterOfDisciple(c);
                var ties = new StringBuilder();
                if (master != null) ties.Append($"Sư phụ {master.Name}{(master.Alive ? "" : " (đã mất)")}");
                if (c.Nemesis >= 0)
                {
                    var foe = sim.Cultivation.All[c.Nemesis];
                    string why = c.NemesisFor >= 0 ? $" (giết {sim.Cultivation.All[c.NemesisFor].Name})" : "";
                    ties.Append(ties.Length > 0 ? " · " : "").Append($"<color=#ff8a6a>Huyết thù: {foe.Name}{why}</color>");
                }
                if (c.Kills > 0) ties.Append(ties.Length > 0 ? " · " : "").Append($"{c.Kills} mạng");
                if (c.Legend) ties.Append(ties.Length > 0 ? " · " : "").Append($"<color=#ffd873>Lưu danh sử sách</color>");
                if (ties.Length > 0) sb.Append('\n').Append(ties);
                if (c.Alive && (c.Watched || c.Pills > 0 || c.Treasures > 0))
                    sb.Append($"\n<color=#b8bccc>Túi trữ vật: {c.Stones:N0} linh thạch" +
                              (c.Pills > 0 ? $" · {c.Pills} {Lore.PillFor(c.Realm + 1)}" : "") +
                              (c.Treasures > 0 ? $" · pháp bảo {c.TreasureName}{(c.Treasures > 1 ? $" (+{c.Treasures - 1})" : "")}" : "") + "</color>");

                // Tiểu sử from the HistoryLog: every remembered deed, not just the last few hundred lines.
                sim.History.OfCultivator(c.Index, _bio, 6);
                if (_bio.Count > 0) sb.Append("\n\n<color=#ffd873>Tiểu sử</color>");
                for (int k = _bio.Count - 1; k >= 0; k--)
                    sb.Append($"\n<color=#8890a8>Năm {_bio[k].Year}</color> {_bio[k].Text}");
                int more = sim.History.CountOfCultivator(c.Index) - _bio.Count;
                if (more > 0) sb.Append($"\n<color=#8890a8>… và {more} sự tích khác (Biên niên sử, H)</color>");
                _cardBody.text = sb.ToString();
                _cardBar1Root.gameObject.SetActive(c.Alive);
                _cardBar2Root.gameObject.SetActive(c.Alive);
                if (c.Alive)
                {
                    _cardBar1.fillAmount = Mathf.Clamp01(c.Progress / Realms.Need(c.Realm, c.Stage));
                    _cardBar2.fillAmount = Mathf.Clamp01(c.AgeYears(tick) / c.LifespanYears);
                }
                _followButton.Frame.gameObject.SetActive(c.Alive);
                _watchButton.Frame.gameObject.SetActive(true);
                _watchButton.Caption.text = c.Watched ? "★ Bỏ theo dõi" : "☆ Theo dõi";
                _watchButton.SetSelected(c.Watched);
                ShowDivineButtons(c.Alive, false);
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
                if (sim.Disasters.IsInfected(s.Id)) body.Append("<color=#9fe07a>Ôn dịch đang hoành hành</color>\n");
                int drought = sim.Disasters.DroughtMonthsLeft(s.X, s.Y, sim.Clock.Tick);
                if (drought >= 0) body.Append($"<color=#ffb060>Đại hạn, còn khoảng {drought} tháng</color>\n");
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
                var f = sim.Factions.Get(s.Id);
                if (f != null && f.Alive)
                {
                    body.Append($"\n\n<color={Hex(f.Color)}>■</color> <color=#ffd873>Thế lực</color> {(f.Demonic ? "<color=#ff7070>ma đạo</color>" : "chính đạo")}" +
                                (f.ParentId >= 0 ? $" · tách từ {sim.Factions.NameOf(f.ParentId)}" : "") +
                                (f.FounderName != null ? $" · khai sơn: {f.FounderName}" : "") + "\n");
                    body.Append($"{f.Tiles} vùng lãnh thổ, {f.LeyTiles} linh mạch · {f.Treasury:N0} linh thạch (+{f.LastIncome:0}/năm)\n");
                    body.Append($"Thực lực {f.Power:N0} · trận thắng {f.BattlesWon}, thua {f.BattlesLost} · tử trận {f.Fallen}");
                    string ties = TiesText(f.Id, 3);
                    if (ties.Length > 0) body.Append('\n').Append(ties);
                }
                // Sử sách of the sect: its greatest moments.
                sim.History.OfFaction(s.Id, _bio, 30);
                int told = 0;
                foreach (var r in _bio)
                {
                    if (r.Importance < 3 || told >= 4) continue;
                    if (told++ == 0) body.Append("\n\n<color=#ffd873>Sử sách</color>");
                    body.Append($"\n<color=#8890a8>Năm {r.Year}</color> {r.Text}");
                }
            }
            else if (s.Alive)
            {
                var protector = sim.Factions.ProtectorOf(s.X, s.Y);
                if (protector != null) body.Append($"\nDưới sự che chở của {protector.BaseName} (nộp cống linh thạch, tiến cử đệ tử)");
            }
            _cardBody.text = body.ToString();
            _cardBar1Root.gameObject.SetActive(false);
            _cardBar2Root.gameObject.SetActive(false);
            _followButton.Frame.gameObject.SetActive(false);
            ShowDivineButtons(s.Alive, true);
            _watchButton.Frame.gameObject.SetActive(false);
        }

        string InspectTitle(in InspectTarget t)
        {
            var w = _game.World;
            switch (t.Kind)
            {
                case InspectKind.Migrants: return "Đoàn di dân";
                case InspectKind.Animal: return $"Đàn {SpeciesInfo.Names[(int)WildlifeSystem.Kinds[t.Animal]].ToLower()}";
                case InspectKind.Object:
                    return w.Objects.IsAlive(t.ObjectId) ? ObjectInfo.Names[(int)w.Objects.Get(t.ObjectId).Type] : "Vật thể đã mất";
                default:
                    return $"Ô ({t.CellX}, {t.CellY}) · {TerrainInfo.Names[(int)w.Terrain[w.Idx(t.CellX, t.CellY)]]}";
            }
        }

        string InspectBody(in InspectTarget t)
        {
            var sim = _game.Sim;
            var w = _game.World;
            var sb = new StringBuilder();
            switch (t.Kind)
            {
                case InspectKind.Migrants:
                    if (sim.Entities.Species[t.Entity] != Species.Migrants || !sim.Settlements.MigrantInfo(t.Entity, out var from, out int people, out float food))
                    {
                        sb.Append("<color=#8890a8>Đoàn đã dừng chân lập làng hoặc tan rã.</color>");
                        break;
                    }
                    sb.Append($"{people} người · lương thực mang theo {food / Mathf.Max(1, people):0.0} tháng\n");
                    if (from != null) sb.Append($"Rời {from.Name} đi tìm đất lập làng mới\n");
                    sb.Append($"Đi được {(sim.Clock.Tick - sim.Entities.BirthTick[t.Entity]) / (float)SimClock.DaysPerMonth:0} tháng");
                    break;

                case InspectKind.Animal:
                {
                    var wild = sim.Wildlife;
                    var kind = WildlifeSystem.Kinds[t.Animal];
                    int rx = t.Region % wild.RW, ry = t.Region / wild.RW;
                    sb.Append($"<color=#ffd873>{wild.At(kind, t.Region):N0} con</color> trong vùng ({rx}, {ry}) rộng {WildlifeSystem.Region}×{WildlifeSystem.Region} ô\n");
                    sb.Append("<color=#8890a8>Trên map chỉ vẽ vài con tượng trưng.</color>\n");
                    sb.Append(kind == Species.Wolf ? "Săn hươu và thỏ trong vùng.\n" : "Ăn cỏ; bị sói săn và dân làng đi săn.\n");
                    sb.Append($"Cả vùng: hươu {wild.At(Species.Deer, t.Region):N0} · thỏ {wild.At(Species.Rabbit, t.Region):N0} · sói {wild.At(Species.Wolf, t.Region):N0}\n");
                    int b = WildlifeSystem.Region / ForageSystem.Block;
                    float cap = sim.Forage.CapSum(rx * b, ry * b, b);
                    sb.Append($"Cỏ trong vùng {(cap > 0f ? sim.Forage.Sum(rx * b, ry * b, b) / cap * 100f : 0f):0}% · toàn thế giới {wild.Total(kind):N0} con");
                    break;
                }

                case InspectKind.Object:
                    if (w.Objects.IsAlive(t.ObjectId))
                    {
                        var o = w.Objects.Get(t.ObjectId);
                        sb.Append($"Chiếm {ObjectInfo.FootprintW[(int)o.Type]}×{ObjectInfo.FootprintH[(int)o.Type]} ô tại ({o.X}, {o.Y})");
                        if (ObjectInfo.IsBuilding(o.Type)) sb.Append(" · <color=#8890a8>không thuộc làng nào</color>");
                        sb.Append("\n\n");
                    }
                    AppendCell(sb, t.CellX, t.CellY);
                    break;

                default:
                    AppendCell(sb, t.CellX, t.CellY);
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        void AppendCell(StringBuilder sb, int x, int y)
        {
            var sim = _game.Sim;
            var w = _game.World;
            int i = w.Idx(x, y);
            var terrain = w.Terrain[i];
            float tempC = -20f + (w.Temperature[i] / 255f + sim.Clock.SeasonalTemperatureOffset) * 60f;
            sb.Append($"<color=#ffd873>Ô ({x}, {y})</color> {TerrainInfo.Names[(int)terrain]} · độ cao {w.Height[i]:0.00}\n");
            sb.Append($"Nhiệt độ {tempC:0}°C · độ ẩm {w.Moisture[i] * 100 / 255}%");
            if (TerrainInfo.IsLand(terrain)) sb.Append($" · màu mỡ {w.Fertility(i) * 100f:0}%");
            sb.Append('\n');
            sb.Append($"Linh khí {sim.Qi.SampleQi(x, y):0} / trần {w.QiCap[i]}{(w.LeyLine[i] ? " · <color=#9fe0ff>LINH MẠCH</color>" : "")}\n");
            if (TerrainInfo.IsLand(terrain))
                sb.Append($"Cỏ khu {ForageSystem.Block}×{ForageSystem.Block} ô quanh đây {sim.Forage.At(x, y):0} / {sim.Forage.CapAt(x, y):0}{(w.IsWalkable(x, y) ? "" : " · không đi bộ qua được")}\n");
            else
                sb.Append("Mặt nước: phàm nhân và Luyện Khí rơi xuống là chết đuối\n");
            var mark = sim.Disasters.LandmarkAt(x + 0.5f, y + 0.5f);
            if (mark != null)
                sb.Append(mark.Kind == Landmark.Thunder
                    ? $"<color=#c8a8ff>Lôi địa {mark.Name}</color>, {mark.Origin}; lôi khí còn khoảng {(mark.Until - sim.Clock.Tick) / SimClock.DaysPerYear} năm\n"
                    : $"<color=#ff9a6a>{mark.Name}</color>, {mark.Origin}\n");
            if (terrain == Terrain.Lava) sb.Append("Dung nham đang chảy: ai rơi vào là chết cháy, nguội dần thành đá\n");
            int drought = sim.Disasters.DroughtMonthsLeft(x, y, sim.Clock.Tick);
            if (drought >= 0) sb.Append($"<color=#ffb060>Đang hạn hán</color>, còn khoảng {drought} tháng · mùa màng chỉ được một phần tư\n");
            var owner = sim.Settlements.Owning(i);
            if (owner != null && owner.Alive)
                sb.Append(terrain == Terrain.Farmland ? $"Ruộng của {owner.Name}\n" : $"Đất của {owner.Name}\n");
            var realm = sim.Factions.OwnerAt(x, y);
            int tile = sim.Factions.TileOf(x, y);
            sb.Append(realm != null
                ? $"Lãnh thổ <color={Hex(realm.Color)}>{sim.Factions.NameOf(realm.Id)}</color>{(sim.Factions.IsLeyTile(tile) ? " · vùng có linh mạch" : "")}\n"
                : $"Vô chủ{(sim.Factions.IsLeyTile(tile) ? " · vùng có linh mạch chưa ai chiếm" : "")}\n");
            int region = sim.Wildlife.RegionOf(x, y);
            sb.Append($"<color=#8890a8>Vùng thú: hươu {sim.Wildlife.At(Species.Deer, region):0} · thỏ {sim.Wildlife.At(Species.Rabbit, region):0} · sói {sim.Wildlife.At(Species.Wolf, region):0}</color>");
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
                    var f = s.Sect ? sim.Factions.Get(s.Id) : null;
                    // Sects in a light tint of their colour; mortal villages in plain ink.
                    var color = f != null ? Color.Lerp(f.Color, Color.white, 0.45f) : Ui.Ink;
                    PlaceLabel(ref used, cam, new Vector3(s.X + 0.5f, s.Y + 5f, 0f), $"{s.Name} ({s.Population})", color, 20);
                }
                foreach (var l in sim.Disasters.Landmarks)
                    if (l.Alive)
                        PlaceLabel(ref used, cam, new Vector3(l.X + 0.5f, l.Y + l.R + 1.5f, 0f), l.Name,
                            l.Kind == Landmark.Thunder ? new Color(0.8f, 0.68f, 1f) : new Color(1f, 0.6f, 0.4f), 18);
                sim.Factions.ActiveBattles(_battleInfo);
                foreach (var b in _battleInfo)
                    PlaceLabel(ref used, cam, new Vector3(b.X, b.Y + 3.5f, 0f),
                        $"Chiến trường: {sim.Factions.NameOf(b.Attacker)} – {sim.Factions.NameOf(b.Defender)}", new Color(1f, 0.45f, 0.4f), 18);
                var e = sim.Entities;
                foreach (var c in sim.Cultivation.All)
                {
                    if (c.Watched && c.Alive)
                    {
                        PlaceLabel(ref used, cam, new Vector3(e.X[c.Entity], e.Y[c.Entity] + 2.6f, 0f), $"★ {c.Name} · {c.RealmText}", WatchColor, 18);
                        continue;
                    }
                    if (c.Realm < Realm.KetDan || !sim.Cultivation.IsShownOnMap(c)) continue;
                    PlaceLabel(ref used, cam, new Vector3(e.X[c.Entity], e.Y[c.Entity] + 2.6f, 0f), $"{c.Title} · {Realms.Names[(int)c.Realm]}",
                        c.Demonic ? new Color(1f, 0.5f, 0.5f) : Ui.Gold, 18);
                }
            }
            for (int k = used; k < _labels.Count; k++)
                if (_labels[k].gameObject.activeSelf) _labels[k].gameObject.SetActive(false);

            string hint = null;
            var h = _game.Hovered;
            if (!PointerOverUI && WorldBrush.IsDivineTool(_game.Brush.Tool))
            {
                // Say who the act will fall on before the click.
                var tool = _game.Brush.Tool;
                string who = h.Kind == InspectKind.Cultivator ? h.Cultivator.Title :
                             h.Kind == InspectKind.Settlement ? $"một phàm nhân ở {h.Settlement.Name}" : null;
                if (tool == BrushTool.GrantRoot)
                    hint = h.Kind == InspectKind.Cultivator ? $"Ban linh căn → {who} ({SpiritRoots.Kind(h.Cultivator.Roots)})" :
                           who != null ? $"Ban linh căn → {who}" : "Chọn một người để ban linh căn";
                else if (tool == BrushTool.Bless)
                    hint = h.Kind == InspectKind.Settlement ? $"Ban phúc → {h.Settlement.Name}" : who != null ? $"Ban cơ duyên → {who}" : "Ban cơ duyên → tu sĩ gần nhất";
                else if (tool == BrushTool.Tribulation)
                    hint = h.Kind == InspectKind.Cultivator
                        ? $"Thiên kiếp → {who} ({h.Cultivator.RealmText}{(Realms.IsPeak(h.Cultivator.Realm, h.Cultivator.Stage) ? ", đang ở bình cảnh" : "")})"
                        : "Chọn một tu sĩ để giáng thiên kiếp";
                else
                    hint = h.Kind == InspectKind.Cultivator ? $"Thiên phạt → {who}" : h.Kind == InspectKind.Settlement ? $"Thiên lôi → {h.Settlement.Name}" : "Thiên lôi đánh xuống đây";
            }
            else if (!PointerOverUI && WorldBrush.IsCalamityTool(_game.Brush.Tool))
            {
                var tool = _game.Brush.Tool;
                hint = tool == BrushTool.Plague
                    ? h.Kind == InspectKind.Settlement ? $"Ôn dịch → {h.Settlement.Name}" : "Ôn dịch → làng gần nhất"
                    : $"{WorldBrush.ToolNames[(int)tool]} · bán kính {DisasterSystem.Radius(WorldBrush.CalamityFor(tool), _game.Brush.Size)} ô";
            }
            else if (!PointerOverUI && _game.Brush.Tool == BrushTool.Inspect)
            {
                switch (h.Kind)
                {
                    case InspectKind.Cultivator: hint = $"{h.Cultivator.Title} · {h.Cultivator.RealmText} · {h.Cultivator.Activity}"; break;
                    case InspectKind.Settlement: hint = $"{h.Settlement.Name} · {h.Settlement.Population} người"; break;
                    case InspectKind.Migrants: hint = "Đoàn di dân"; break;
                    case InspectKind.Animal:
                        hint = $"{SpeciesInfo.Names[(int)WildlifeSystem.Kinds[h.Animal]]} · {sim.Wildlife.At(WildlifeSystem.Kinds[h.Animal], h.Region):N0} con trong vùng";
                        break;
                    case InspectKind.Object:
                    case InspectKind.Cell: hint = InspectTitle(h); break;
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
