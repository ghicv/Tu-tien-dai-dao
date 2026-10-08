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
        Text _date, _toolTitle;
        CanvasGroup _sizeGroup;
        readonly List<Ui.IconButton> _speedButtons = new List<Ui.IconButton>();
        RectTransform _ticker;
        readonly List<(Text text, Image icon, float shownAt)> _tickerLines = new List<(Text, Image, float)>();
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
        Window _ranking, _events, _stats, _powers, _destiny;
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

        static readonly string[] TabNames = { "Địa hình", "Sinh linh", "Linh khí", "Thiên Đạo", "Thiên tai", "Thời tiết", "Quy luật", "Lớp phủ", "Thế giới" };
        static readonly string[] OverlayNames = { "Không", "Linh khí", "Độ cao", "Nhiệt độ", "Độ ẩm", "Thức ăn", "Lãnh thổ", "Thiên tai" };

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
            { BrushTool.Smite, "Thiên phạt — một đạo thiên lôi (4.000 sát thương) đánh xuống tu sĩ, yêu thú hay chỗ bấm; mọi sinh vật trong 3 ô đều trúng sét" },
            { BrushTool.Tribulation, "Thiên kiếp — bấm vào một tu sĩ: sống sót thì phá bình cảnh hoặc được lôi kiếp tôi luyện, không thì vẫn lạc. " +
                                     "Sét đánh cả vùng quanh đó và để lại lôi địa (linh khí dày, phàm nhân tránh xa) vài trăm năm" },
            { BrushTool.Earthquake, "Động đất — nhà sập, người chết, linh mạch trong vùng có thể đứt gãy (cọ to thì vùng rộng)" },
            { BrushTool.Eruption, "Núi lửa — một ngọn núi lửa mọc lên, dung nham chảy ra rồi nguội thành đá sau nhiều năm; tro bụi phủ các làng quanh đó" },
            { BrushTool.Flood, "Lũ lụt — vùng trũng ngập nước vài tháng: ruộng mất, người và thú chết đuối, rồi nước rút" },
            { BrushTool.Drought, "Hạn hán — một vùng rộng mất mùa 1–2 năm, cỏ khô héo; nạn đói kéo theo di dân" },
            { BrushTool.Plague, "Ôn dịch — bấm vào một làng: dịch kéo dài vài tháng và có thể lan sang làng lân cận" },
            { BrushTool.BeastTide, "Thú triều — hàng trăm yêu lang tràn vào các làng quanh đó; tông môn che chở thì đỡ thiệt hại" },
            { BrushTool.GreatCalamity, "Đại kiếp — cả thế giới: 8–15 năm linh khí chỉ còn một nửa, thiên tai liên miên; qua đi thì sang thời đại mới" },
            { BrushTool.Rain, "Mưa — mùa màng tốt hơn vài tháng, cỏ xanh lại; mưa xuống vùng hạn thì hạn hán chấm dứt" },
            { BrushTool.Storm, "Bão — cuồng phong quét theo một đường dài: cây đổ, nhà tốc mái, người chết (cọ to thì bão rộng)" },
            { BrushTool.Cold, "Rét — vài tháng tuyết phủ: mùa màng mất trắng, người già trẻ nhỏ chết cóng, cỏ héo" },
            { BrushTool.Annihilate, "Diệt môn — bấm vào một tông môn: thiên phạt san bằng sơn môn, tu sĩ trong núi vẫn lạc, nơi đó hóa lôi địa" },
            { BrushTool.SpawnBeast, "Thả yêu thú (chọn giai bằng + / −): nó chiếm lãnh địa, săn thú, tập kích làng; tu sĩ sẽ tới săn yêu đan" },
            { BrushTool.SpawnCultivator, "Thả tu sĩ (chọn cảnh giới bằng + / −): một tán tu xuống nhân gian, chỗ bấm thành động phủ; từ đó tự tu luyện, kết thù, thu đồ đệ, lập tông môn" },
            { BrushTool.SpawnHungThu, "Đánh thức hung thú (ngũ đến cửu giai, chọn bằng + / −): nó đi tàn sát từ thành này sang thành khác cho tới khi các tông môn liên minh trảm yêu" },
            { BrushTool.GrantTreasure, "Ban pháp bảo — bấm vào tu sĩ: thêm một pháp bảo (đánh mạnh hơn, dễ vượt thiên kiếp hơn), nhưng ma tu sẽ thèm khát" },
            { BrushTool.HeartDemon, "Giáng tâm ma — bấm vào tu sĩ: đạo tâm lung lay, tu vi trì trệ; đạo tâm càng yếu càng dễ tẩu hỏa nhập ma, tụt cảnh giới hoặc sa vào ma đạo" },
            { BrushTool.Cripple, "Phế tu vi — bấm vào tu sĩ: mất trọn một đại cảnh giới, sức, máu và thọ nguyên giảm theo" },
            { BrushTool.GrantTechnique, "Ban công pháp — bấm vào tu sĩ: truyền một bộ công pháp cực phẩm hợp linh căn (tu nhanh, tới tận Hóa Thần); tông môn của người đó lập làm trấn phái công pháp" },
            { BrushTool.PlaceTreasure, "Thiên tài địa bảo — đặt một linh vật ở chỗ bấm (linh khí càng đậm phẩm càng cao): bảo quang xung thiên, các tông môn kéo đến tranh đoạt" },
            { BrushTool.OpenRealm, "Mở bí cảnh — xé mở một thượng cổ di tích ba tầng ở chỗ bấm: có pháp bảo, linh thạch, đan dược và hiểm nguy; tu sĩ bốn phương tranh nhau vào" },
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

        // Top-left: the date with its season, the speed buttons, the world in a row of numbers, and below it
        // warnings that only appear while something is going on (đại kiếp, droughts, epidemics, changed laws).
        void BuildClock()
        {
            var panel = Ui.Panel(_root, "Clock");
            Ui.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(520f, 150f));
            _seasonIcon = Ui.Icon(panel.rectTransform, Icons.Flower, 32f);
            Ui.Place(_seasonIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(32f, 32f));
            _seasonTip = _seasonIcon.gameObject.AddComponent<Tooltip>();
            _seasonIcon.raycastTarget = true;
            _date = Ui.Label(panel.rectTransform, "", 24, TextAnchor.MiddleLeft, Ui.Gold);
            Ui.Place(_date.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -10f), new Vector2(456f, 32f));
            _date.horizontalOverflow = HorizontalWrapMode.Overflow;

            var speeds = Ui.Node("Speeds", panel.rectTransform);
            Ui.Place(speeds, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -48f), new Vector2(300f, 48f));
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

            var stats = Ui.Node("Stats", panel.rectTransform);
            Ui.Place(stats, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -104f), new Vector2(430f, 34f));
            Row(stats, 4).childAlignment = TextAnchor.MiddleLeft;
            float[] widths = { 96f, 62f, 72f, 56f, 56f, 52f };
            for (int k = 0; k < _hud.Length; k++) _hud[k] = Ui.MakeChip(stats, widths[k]);

            // Warnings, each an icon and a number, only while they matter.
            var alerts = Ui.Node("Alerts", _root);
            Ui.Place(alerts, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -168f), new Vector2(520f, 32f));
            Row(alerts, 8).childAlignment = TextAnchor.MiddleLeft;
            for (int k = 0; k < _alerts.Length; k++) _alerts[k] = Ui.MakeChip(alerts, 72f);

            _ticker = Ui.Node("Ticker", _root);
            Ui.Place(_ticker, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -206f), new Vector2(560f, 200f));
            var tickerCol = Column(_ticker, 4);
            tickerCol.childForceExpandWidth = false;
        }

        Image _seasonIcon;
        Tooltip _seasonTip;
        readonly Ui.Chip[] _hud = new Ui.Chip[6];
        readonly Ui.Chip[] _alerts = new Ui.Chip[6];
        static readonly string[] SeasonNames = { "Xuân", "Hạ", "Thu", "Đông" };

        void UpdateHud()
        {
            var sim = _game.Sim;
            var clock = sim.Clock;
            var era = sim.Eras.Current;
            _date.text = $"Năm {clock.Year} · tháng {clock.Month}" + (era != null ? $"  <size=17><color=#c8b8ff>{era.Name}</color></size>" : "");
            _seasonIcon.sprite = Icons.Season(clock.Season);
            _seasonTip.Text = $"Mùa {SeasonNames[(int)clock.Season]} · {clock.DateText}";

            var cr = sim.Cultivation.CountByRealm;
            int top = cr[(int)Realm.NguyenAnh] + cr[(int)Realm.HoaThan];
            _hud[0].Set(Icons.Person, $"{sim.Settlements.TotalPopulation:N0}", "Phàm nhân");
            _hud[1].Set(Icons.Object(ObjectType.House), $"{sim.Settlements.AliveCount}", "Làng, trấn, thành");
            _hud[2].Set(Icons.ForRealm(Realm.LuyenKhi), $"{sim.Cultivation.AliveCount}", "Tu sĩ");
            _hud[3].Set(Icons.ForRealm(Realm.NguyenAnh), $"{top}", $"Nguyên Anh {cr[(int)Realm.NguyenAnh]} · Hóa Thần {cr[(int)Realm.HoaThan]}", top > 0 ? Ui.Gold : Ui.Dim);
            _hud[4].Set(Icons.Banner, $"{sim.Factions.AliveCount}", "Thế lực (tông môn)");
            _hud[5].Set(Icons.Sword, $"{sim.Factions.WarCount}", "Cuộc chiến đang diễn ra", sim.Factions.WarCount > 0 ? new Color(1f, 0.55f, 0.5f) : Ui.Dim);

            var dis = sim.Disasters;
            int n = 0;
            float cycle = sim.Eras.QiFactor(clock.Tick);
            if (cycle < 0.85f || cycle > 1.15f)
                _alerts[n++].Set(Icons.Orb, $"{cycle * 100f:0}%", $"Linh khí thiên địa đang ở {cycle * 100f:0}% ({(sim.Eras.Waxing(clock.Tick) ? "đang dâng" : "đang suy")}): " +
                                 (cycle < 0.85f ? "mạt pháp, tu sĩ khó tiến cảnh" : "thời hoàng kim của người tu tiên"), cycle < 0.85f ? new Color(1f, 0.55f, 0.5f) : new Color(0.55f, 0.95f, 1f));
            if (dis.GreatCalamityActive)
                _alerts[n++].Set(Icons.Eclipse, $"{dis.GreatCalamityYearsLeft(clock.Tick)} năm", "Đại kiếp: linh khí chỉ còn một nửa, thiên tai liên miên", new Color(1f, 0.5f, 0.4f));
            if (dis.DroughtCount > 0) _alerts[n++].Set(Icons.Sun, $"{dis.DroughtCount}", "Vùng đang hạn hán (lớp phủ Thiên tai để xem)", new Color(1f, 0.75f, 0.4f));
            if (dis.EpidemicCount > 0) _alerts[n++].Set(Icons.Skull, $"{dis.EpidemicCount}", "Làng đang có ôn dịch", new Color(0.6f, 0.9f, 0.5f));
            if (dis.WeatherCount > 0) _alerts[n++].Set(Icons.RainCloud, $"{dis.WeatherCount}", "Vùng đang mưa hoặc rét");
            int changed = 0;
            for (int k = 0; k < (int)Rule.Count; k++)
                if (!sim.Rules.IsDefault((Rule)k)) changed++;
            if (changed > 0) _alerts[n++].Set(Icons.Scales, $"{changed}", "Quy luật đã bị Thiên Đạo sửa (tab Quy luật)", new Color(1f, 0.6f, 0.45f));
            for (int k = n; k < _alerts.Length; k++) _alerts[k].Hide();
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
            _toolTitle = Ui.Label(middle, "", 16, TextAnchor.MiddleLeft, Ui.Dim);
            Height(_toolTitle, 20f);
            _tabsRow = Ui.Node("Tabs", middle);
            Row(_tabsRow, 6).childAlignment = TextAnchor.MiddleLeft;
            Sprite[] tabIcons = { Icons.TerrainTile(Terrain.Mountain), Icons.Unit(Unit.Deer), Icons.Orb, Icons.Bolt, Icons.Volcano, Icons.RainCloud, Icons.Scales, Icons.Layers, Icons.Globe };
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
            _sizeGroup = sizeBox.gameObject.AddComponent<CanvasGroup>();
            Column(sizeBox, 2).childAlignment = TextAnchor.MiddleCenter;
            var plus = Ui.Button(sizeBox, Icons.Plus, "Cọ to hơn ( ] )", () => StepBrush(+1), 40f);
            _plusTip = plus.Frame.GetComponent<Tooltip>();
            Size(plus.Frame, 40f, 40f);
            _brushSize = Ui.Label(sizeBox, "6", 20, TextAnchor.MiddleCenter);
            Size(_brushSize, 96f, 24f);
            var minus = Ui.Button(sizeBox, Icons.Minus, "Cọ nhỏ hơn ( [ )", () => StepBrush(-1), 40f);
            _minusTip = minus.Frame.GetComponent<Tooltip>();
            Size(minus.Frame, 40f, 40f);
        }

        Tooltip _plusTip, _minusTip;

        // + / − change the brush size, or for a tool that sends a being down, its realm or grade.
        void StepBrush(int d)
        {
            var brush = _game.Brush;
            if (WorldBrush.HasLevel(brush.Tool)) brush.StepLevel(d);
            else brush.Size = Mathf.Clamp(brush.Size + d, 1, 40);
        }

        void SelectTab(int tab)
        {
            _tab = tab;
            for (int k = 0; k < _tabButtons.Count; k++) _tabButtons[k].SetSelected(k == tab);
            for (int k = _toolsRow.childCount - 1; k >= 0; k--) Destroy(_toolsRow.GetChild(k).gameObject);
            _toolButtons.Clear();
            _overlayButtons.Clear();
            _ruleValues.Clear();
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
                    AddTool(BrushTool.SpawnBeast, Icons.Unit(Unit.Beast));
                    AddTool(BrushTool.SpawnHungThu, Icons.Unit(SpriteLibrary.BeastUnit((int)BeastKind.Tiger, 9)));
                    AddTool(BrushTool.SpawnCultivator, Icons.Unit(Unit.CultivatorKD));
                    AddTool(BrushTool.Erase, Icons.Erase);
                    break;
                case 2:
                    AddTool(BrushTool.LeyAdd, Icons.LeyLine);
                    AddTool(BrushTool.LeyErase, Icons.LeyBreak);
                    AddTool(BrushTool.QiInfuse, Icons.QiUp);
                    AddTool(BrushTool.QiDrain, Icons.QiDown);
                    AddTool(BrushTool.PlaceTreasure, Icons.Object(ObjectType.RelicTreasure));
                    AddTool(BrushTool.OpenRealm, Icons.Object(ObjectType.RelicAncient));
                    break;
                case 3:
                    AddTool(BrushTool.GrantRoot, Icons.Seed);
                    AddTool(BrushTool.Bless, Icons.Star);
                    AddTool(BrushTool.Smite, Icons.Bolt);
                    AddTool(BrushTool.Tribulation, Icons.Tribulation);
                    AddTool(BrushTool.GrantTechnique, Icons.JadeSlip);
                    AddTool(BrushTool.GrantTreasure, Icons.Sword);
                    AddTool(BrushTool.HeartDemon, Icons.HeartDemon);
                    AddTool(BrushTool.Cripple, Icons.Cripple);
                    AddTool(BrushTool.Annihilate, Icons.Wrath);
                    break;
                case 4:
                    AddTool(BrushTool.Earthquake, Icons.Quake);
                    AddTool(BrushTool.Eruption, Icons.Volcano);
                    AddTool(BrushTool.Flood, Icons.Wave);
                    AddTool(BrushTool.Drought, Icons.Sun);
                    AddTool(BrushTool.Plague, Icons.Skull);
                    AddTool(BrushTool.BeastTide, Icons.Paw);
                    AddTool(BrushTool.GreatCalamity, Icons.Eclipse);
                    break;
                case 5:
                    AddTool(BrushTool.Rain, Icons.RainCloud);
                    AddTool(BrushTool.Storm, Icons.Storm);
                    AddTool(BrushTool.Cold, Icons.Snowflake);
                    break;
                case 6:
                    BuildRulesTab();
                    break;
                case 7:
                    var grads = new[]
                    {
                        Icons.Gradient("none", new Color32(60, 66, 90, 255), new Color32(60, 66, 90, 255)),
                        Icons.Gradient("qi", new Color32(40, 10, 90, 255), new Color32(130, 255, 255, 255)),
                        Icons.Gradient("height", new Color32(20, 20, 20, 255), new Color32(240, 240, 240, 255)),
                        Icons.Gradient("temp", new Color32(40, 90, 255, 255), new Color32(255, 60, 30, 255)),
                        Icons.Gradient("moist", new Color32(170, 110, 50, 255), new Color32(30, 120, 255, 255)),
                        Icons.Gradient("forage", new Color32(200, 60, 40, 255), new Color32(60, 230, 70, 255)),
                        Icons.Gradient("owner", new Color32(200, 120, 90, 255), new Color32(90, 140, 220, 255)),
                        Icons.Gradient("calamity", new Color32(240, 160, 60, 255), new Color32(170, 120, 255, 255)),
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

            var create = Ui.Button(_toolsRow, Icons.Globe, "Tạo lại thế giới từ seed này", () => _game.Generate(_seedField.text), 58f);
            Size(create.Frame, 58f, 58f);
            var random = Ui.Button(_toolsRow, Icons.Dice, "Tạo thế giới với seed ngẫu nhiên", () => _game.Generate(WorldBootstrap.RandomSeed()), 58f);
            Size(random.Frame, 58f, 58f);
            _labelsToggle = Ui.Button(_toolsRow, Icons.Tag, "Hiện/ẩn tên làng và cường giả trên bản đồ", () => ShowLabels = !ShowLabels, 58f);
            Size(_labelsToggle.Frame, 58f, 58f);
            var saves = Ui.Button(_toolsRow, Icons.SaveSlip, "Lưu / tải thế giới (F5 lưu nhanh, F9 tải nhanh; tự lưu 5 phút một lần)", ToggleSaves, 58f);
            Size(saves.Frame, 58f, 58f);
        }

        SaveWindow _saves;

        public void ToggleSaves()
        {
            if (_saves == null) _saves = new SaveWindow(_root, _game, this);
            _saves.Toggle();
        }

        // Quy luật: one box per law of the world, − / + (or a switch); every change is a command, so it replays.
        readonly List<(Rule rule, Text value)> _ruleValues = new List<(Rule, Text)>();

        static Sprite[] RuleIcons => new[]
        {
            Icons.Orb, Icons.Seed, Icons.Star, Icons.Tribulation, Icons.Person, Icons.Volcano, Icons.ForRealm(Realm.LuyenKhi, true)
        };

        void BuildRulesTab()
        {
            for (int k = 0; k < (int)Rule.Count; k++)
            {
                var rule = (Rule)k;
                // An icon says which law it is (the tooltip names it); − value + changes it.
                var box = Ui.Panel(_toolsRow, "Rule", Ui.BarSprite);
                Size(box, 112f, 60f);
                box.gameObject.AddComponent<Tooltip>().Text = $"{WorldRules.Names[k]}: {WorldRules.Help[k]}";
                var icon = Ui.Icon(box.rectTransform, RuleIcons[k], 26f);
                Ui.Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(26f, 26f));
                var value = Ui.Label(box.rectTransform, "", 18, TextAnchor.MiddleRight, Ui.Gold);
                Ui.Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(64f, 26f));
                if (WorldRules.IsToggle(rule))
                {
                    var flip = Ui.Button(box.rectTransform, null, WorldRules.Help[k], () => SetRule(rule, _game.Sim.Rules[rule] > 0.5f ? 0f : 1f), 24f, "bật / tắt");
                    flip.Caption.fontSize = 14;
                    Ui.Place(flip.Frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(84f, 24f));
                }
                else
                {
                    float step = WorldRules.Step[k];
                    var minus = Ui.Button(box.rectTransform, null, "Giảm", () => SetRule(rule, _game.Sim.Rules[rule] - step), 24f, "-");
                    minus.Caption.fontSize = 24;
                    Ui.Place(minus.Frame.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 6f), new Vector2(40f, 24f));
                    var plus = Ui.Button(box.rectTransform, null, "Tăng", () => SetRule(rule, _game.Sim.Rules[rule] + step), 24f, "+");
                    plus.Caption.fontSize = 24;
                    Ui.Place(plus.Frame.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 6f), new Vector2(40f, 24f));
                }
                _ruleValues.Add((rule, value));
            }
            var reset = Ui.Button(_toolsRow, Icons.Undo, "Trả mọi quy luật về như lúc khai thiên lập địa", ResetRules, 58f);
            Size(reset.Frame, 58f, 58f);
        }

        void SetRule(Rule rule, float value) => _game.Sim.Enqueue(new SetRuleCommand(rule, value));

        void ResetRules()
        {
            var rules = _game.Sim.Rules;
            for (int k = 0; k < (int)Rule.Count; k++)
                if (!rules.IsDefault((Rule)k)) SetRule((Rule)k, WorldRules.DefaultOf((Rule)k));
        }

        void UpdateRuleValues()
        {
            var rules = _game.Sim.Rules;
            foreach (var (rule, text) in _ruleValues)
            {
                text.text = rules.Describe(rule);
                text.color = rules.IsDefault(rule) ? Ui.Gold : new Color(1f, 0.55f, 0.45f);
            }
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
            Ui.Place(buttons, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(390f, 60f));
            Row(buttons, 6).childAlignment = TextAnchor.MiddleRight;

            _windowStack = Ui.Node("Windows", _root);
            Ui.Place(_windowStack, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -80f), new Vector2(460f, 800f));
            var col = Column(_windowStack, 8);
            col.childAlignment = TextAnchor.UpperRight;

            _ranking = MakeWindow("Bảng cường giả");
            BuildRankingRows();
            _events = MakeWindow("Sự kiện");
            BuildEventRows();
            _stats = MakeWindow("Thống kê");
            BuildStatChips();
            _powers = MakeWindow("Thế lực");
            _destiny = MakeWindow("Thiên mệnh");
            BuildDestinyRows();
            _chronicle = new ChronicleWindow(_root, _game);
            var book = Ui.Button(buttons, Icons.Book, "Biên niên sử: sử sách, truyền kỳ, danh nhân (H)", () => _chronicle.Toggle(), 56f);
            Size(book.Frame, 56f, 56f);
            AddWindowButton(buttons, Icons.Star, "Thiên mệnh: mục tiêu thế giới gợi ý, và lời cầu nguyện của chúng sinh đang chờ đáp", _destiny);
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

            // Header: a picture of who (or what) it is, the name, and a close button.
            var header = Ui.Node("Header", _card);
            Height(header, 44f);
            _cardPortrait = Ui.Icon(header, null, 40f);
            Ui.Place(_cardPortrait.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            _cardTitle = Ui.Label(header, "", 25, TextAnchor.MiddleLeft, Ui.Gold);
            Ui.Stretch(_cardTitle.rectTransform, 48, 36, 0, 0);
            var close = Ui.Button(header, Icons.Close, "Đóng (Esc)", () => _game.ClearSelection(), 30f);
            Ui.Place(close.Frame.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            _cardSub = Ui.Label(_card, "", 16, TextAnchor.UpperLeft, Ui.Dim);

            // The numbers that matter, as icon chips (hover for what each one is).
            var chips = Ui.Node("Chips", _card);
            var grid = chips.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(138f, 30f);
            grid.spacing = new Vector2(6f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            for (int k = 0; k < _cardChips.Length; k++) _cardChips[k] = Ui.MakeChip(chips, 138f);

            _cardBar1Root = BarRow(Icons.Orb, new Color(0.45f, 0.85f, 1f), "Tu vi trong tiểu cảnh giới hiện tại", out _cardBar1, out _cardBar1Text);
            _cardBar2Root = BarRow(Icons.Hourglass, new Color(1f, 0.6f, 0.4f), "Tuổi so với thọ nguyên", out _cardBar2, out _cardBar2Text);
            _cardBody = Ui.Label(_card, "", 17);

            var buttons = Ui.Node("Buttons", _card);
            Row(buttons, 8).childAlignment = TextAnchor.MiddleLeft;
            Height(buttons, 46f);
            _followButton = Ui.Button(buttons, Icons.Target, "Camera bám theo", () => _game.Follow = !_game.Follow, 44f);
            Size(_followButton.Frame, 44f, 44f);
            _watchButton = Ui.Button(buttons, Icons.Bookmark, "Theo dõi: người này sẽ sống như nhân vật chính", ToggleWatchSelected, 44f);
            Size(_watchButton.Frame, 44f, 44f);
            _reviveButton = Ui.Button(buttons, Icons.Revive, "Hồi sinh: sống lại với thêm trăm năm thọ, mang huyết thù với kẻ đã giết mình", _game.ReviveSelected, 44f);
            Size(_reviveButton.Frame, 44f, 44f);
            // Thiên Đạo acts on exactly the one shown on the card: phúc first, then họa.
            var actsRow = _actsRow = Ui.Node("Acts", _card);
            Row(actsRow, 6).childAlignment = TextAnchor.MiddleLeft;
            Height(actsRow, 46f);
            var acts = new[]
            {
                (DivineAct.GrantRoot, Icons.Seed), (DivineAct.Bless, Icons.Star), (DivineAct.Smite, Icons.Bolt), (DivineAct.Tribulation, Icons.Tribulation),
                (DivineAct.Annihilate, Icons.Wrath), (DivineAct.GrantTreasure, Icons.Sword), (DivineAct.HeartDemon, Icons.HeartDemon), (DivineAct.Cripple, Icons.Cripple),
                (DivineAct.GrantTechnique, Icons.JadeSlip)
            };
            for (int k = 0; k < acts.Length; k++)
            {
                var act = acts[k].Item1;
                _divineButtons[k] = Ui.Button(actsRow, acts[k].Item2, DivineTipPerson[k], () => _game.ActOnSelected(act), 40f);
                Size(_divineButtons[k].Frame, 40f, 40f);
            }
            _card.gameObject.SetActive(false);
        }

        // Icon, bar and its numbers on one line.
        RectTransform BarRow(Sprite icon, Color fill, string tip, out Image bar, out Text value)
        {
            var row = Ui.Node("BarRow", _card);
            Height(row, 24f);
            Row(row, 6).childAlignment = TextAnchor.MiddleLeft;
            Ui.Icon(row, icon, 22f);
            var bg = Ui.Bar(row, fill, out bar);
            Element(bg).flexibleWidth = 1f;
            Height(bg, 18f);
            value = Ui.Label(row, "", 16, TextAnchor.MiddleRight, Ui.Dim);
            Size(value, 128f, 22f);
            row.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // hover target
            row.gameObject.AddComponent<Tooltip>().Text = tip;
            return row;
        }

        Image _cardPortrait;
        Text _cardSub, _cardBar1Text, _cardBar2Text;
        readonly Ui.Chip[] _cardChips = new Ui.Chip[24];
        int _chipCount;

        void Chip(Sprite icon, string text, string tip, Color? color = null)
        {
            if (_chipCount < _cardChips.Length) _cardChips[_chipCount++].Set(icon, text, tip, color);
        }

        void EndChips()
        {
            for (int k = _chipCount; k < _cardChips.Length; k++) _cardChips[k].Hide();
            _chipCount = 0;
        }

        readonly Ui.IconButton[] _divineButtons = new Ui.IconButton[9];
        Ui.IconButton _reviveButton;
        RectTransform _actsRow;
        const int SmiteButton = 2, AnnihilateButton = 4;
        const string DivineTipCreature = "Thiên phạt: một đạo thiên lôi 4.000 sát thương (ít nhất 40% sinh lực); mọi sinh vật trong 3 ô đều trúng sét";

        static readonly string[] DivineTipPerson =
        {
            "Ban linh căn: tẩy luyện linh căn người này lên Thiên / Dị linh căn",
            "Ban cơ duyên: tu vi tăng mạnh, khí vận tràn đầy, thêm 20 năm thọ",
            "Thiên phạt: một đạo thiên lôi 4.000 sát thương (ít nhất 40% sinh lực); kẻ mạnh có thể chịu được vài đạo. Ai đứng gần cũng trúng sét",
            "Thiên kiếp: vượt qua thì phá bình cảnh (hoặc được tôi luyện), thất bại thì vẫn lạc; nơi đó hóa lôi địa",
            "",
            "Ban pháp bảo: đánh mạnh hơn, dễ vượt thiên kiếp hơn; ma tu sẽ thèm khát",
            "Giáng tâm ma: đạo tâm lung lay; đạo tâm yếu thì dễ tẩu hỏa nhập ma, tụt cảnh giới hoặc sa vào ma đạo",
            "Phế tu vi: mất trọn một đại cảnh giới, sức, máu và thọ nguyên giảm theo",
            "Ban công pháp: truyền một bộ công pháp cực phẩm hợp linh căn; tông môn sẽ lập làm trấn phái công pháp"
        };

        static readonly string[] DivineTipVillage =
        {
            "Ban linh căn: điểm hóa một phàm nhân trưởng thành trong làng",
            "Ban cơ duyên: mùa màng bội thu",
            "Thiên phạt: thiên lôi đánh xuống làng",
            "",
            "Diệt môn: thiên phạt san bằng sơn môn, tu sĩ trong núi vẫn lạc, nơi đó hóa lôi địa",
            "", "", "", ""
        };

        void ShowDivineButtons(bool show, bool village, bool sect = false, bool creature = false)
        {
            bool any = false;
            for (int k = 0; k < _divineButtons.Length; k++)
            {
                var b = _divineButtons[k];
                // Thiên kiếp is for cultivators only; diệt môn for sects only.
                bool on = show && (creature ? k == SmiteButton : village ? DivineTipVillage[k].Length > 0 && (k != AnnihilateButton || sect) : DivineTipPerson[k].Length > 0);
                if (b.Frame.gameObject.activeSelf != on) b.Frame.gameObject.SetActive(on);
                if (!on) continue;
                any = true;
                var tip = b.Frame.GetComponent<Tooltip>();
                if (tip != null) tip.Text = creature ? DivineTipCreature : village ? DivineTipVillage[k] : DivineTipPerson[k];
            }
            if (_actsRow.gameObject.activeSelf != any) _actsRow.gameObject.SetActive(any);
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

            for (int k = 0; k < _speedButtons.Count; k++) _speedButtons[k].SetSelected(sim.SpeedIndex == k);
            UpdateHud();
            UpdateTicker();

            var current = _game.Brush.Tool;
            _inspectButton.SetSelected(current == BrushTool.Inspect);
            foreach (var (button, tool) in _toolButtons) button.SetSelected(current == tool);
            foreach (var (button, mode) in _overlayButtons) button.SetSelected(_game.Renderer.Overlay == mode);
            if (_labelsToggle != null) _labelsToggle.SetSelected(ShowLabels);
            if (_ruleValues.Count > 0) UpdateRuleValues();
            bool leveled = WorldBrush.HasLevel(current);
            _brushSize.text = leveled ? WorldBrush.LevelName(current, _game.Brush.Level) : _game.Brush.Size.ToString();
            _brushSize.fontSize = leveled ? 16 : 20;
            if (_plusTip != null) _plusTip.Text = leveled ? (current == BrushTool.SpawnCultivator ? "Cảnh giới cao hơn ( ] )" : "Giai cao hơn ( ] )") : "Cọ to hơn ( ] )";
            if (_minusTip != null) _minusTip.Text = leveled ? (current == BrushTool.SpawnCultivator ? "Cảnh giới thấp hơn ( [ )" : "Giai thấp hơn ( [ )") : "Cọ nhỏ hơn ( [ )";
            // The brush size only matters to tools that paint an area or reach over one; the level to those that send a being down.
            bool sized = leveled || (current != BrushTool.Inspect && !WorldBrush.IsDivineTool(current) && !WorldBrush.IsPointTool(current) &&
                         !WorldBrush.IsBuildingTool(current) && current != BrushTool.Plague && current != BrushTool.Eruption && current != BrushTool.GreatCalamity);
            _sizeGroup.alpha = sized ? 1f : 0.15f; // faded, not hidden, so the toolbar keeps its shape
            _sizeGroup.interactable = _sizeGroup.blocksRaycasts = sized;
            _toolTitle.text = $"{TabNames[_tab].ToUpper()}  <color=#ffd873>›  {WorldBrush.ToolNames[(int)current]}</color>";

            _slowRefresh -= Time.unscaledDeltaTime;
            if (_slowRefresh <= 0f)
            {
                _slowRefresh = 0.25f;
                if (_ranking.Open) UpdateRankingRows();
                UpdateWatchList();
                if (_events.Open) UpdateEventRows();
                if (_powers.Open) _powers.Body.text = PowersText();
                if (_destiny.Open) UpdateDestinyRows();
            }
            if (_stats.Open)
            {
                UpdateStatChips();
                _stats.Body.text = StatsText();
            }

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
                var ev = recent[k];
                bool watched = IsWatchedEvent(ev);
                if (ev.Importance < 2 && !watched) continue;
                // One line of news: what kind of thing happened, at a glance, then the words.
                var row = Ui.Node("News", _ticker);
                var layout = Row(row, 6);
                layout.childAlignment = TextAnchor.UpperLeft;
                var icon = Ui.Icon(row, watched ? Icons.Star : Icons.ForEvent(ev.Kind, ev.Fx), 26f);
                var t = Ui.Label(row, ev.Text, 19, TextAnchor.UpperLeft, watched ? WatchColor : ev.Importance >= 3 ? Ui.Gold : Ui.Ink);
                Element(t).preferredWidth = 524f; // height follows the wrapped text
                // Click the news to fly there.
                row.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                var evCopy = ev;
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => FocusEvent(evCopy));
                _tickerLines.Add((t, icon, Time.unscaledTime));
                if (_tickerLines.Count > 4)
                {
                    Destroy(_tickerLines[0].text.transform.parent.gameObject);
                    _tickerLines.RemoveAt(0);
                }
            }
            for (int k = _tickerLines.Count - 1; k >= 0; k--)
            {
                var (text, img, shownAt) = _tickerLines[k];
                float age = Time.unscaledTime - shownAt;
                if (age > 8f)
                {
                    Destroy(text.transform.parent.gameObject);
                    _tickerLines.RemoveAt(k);
                    continue;
                }
                float a = Mathf.Clamp01((8f - age) / 1.5f);
                var c = text.color;
                c.a = a;
                text.color = c;
                img.color = new Color(1f, 1f, 1f, a);
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

        // Where an event happened, or where the one it is about is now; the camera glides there and the
        // cultivator (if any) is shown on the card.
        public void FocusEvent(WorldEvent ev)
        {
            var sim = _game.Sim;
            var all = sim.Cultivation.All;
            var c = ev.A >= 0 && ev.A < all.Count ? all[ev.A] : null;
            Vector2 at;
            if (ev.X >= 0f) at = new Vector2(ev.X, ev.Y);
            else if (c != null && sim.Cultivation.IsShownOnMap(c)) at = new Vector2(sim.Entities.X[c.Entity], sim.Entities.Y[c.Entity]);
            else if (c != null) at = new Vector2(c.HomeX, c.HomeY);
            else
            {
                ShowToast("Sự kiện này không gắn với một nơi nào trên bản đồ.");
                return;
            }
            var cam = _game.Camera;
            cam.GlideTo(at, Mathf.Min(cam.Cam.orthographicSize, 28f));
            if (c != null) _game.Inspect(c);
        }

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
                Ui.Stretch(b.Caption.rectTransform, 46, 6, 2, 2);
                Size(b.Frame, 324f, 46f);
                _watchIcons[k] = Ui.Icon(b.Frame.rectTransform, null, 34f);
                Ui.Place(_watchIcons[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(34f, 34f));
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
                _watchIcons[k].sprite = c.Alive ? Icons.Cultivator(c) : Icons.Skull;
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
                _game.Inspect(c); // the card still opens: the biography, and the power to bring them back
                ShowToast($"{c.Title} đã vẫn lạc. Thiên Đạo có thể hồi sinh người này từ thẻ nhân vật.");
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
        readonly Image[] _rankIcons = new Image[RankRows];
        readonly Image[] _watchIcons = new Image[WatchRows];

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
                Ui.Stretch(b.Caption.rectTransform, 54, 8, 2, 2);
                Height(b.Frame, 54f);
                _rankIcons[k] = Ui.Icon(b.Frame.rectTransform, null, 40f);
                Ui.Place(_rankIcons[k].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(40f, 40f));
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
                _rankIcons[k].sprite = Icons.Cultivator(c);
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

        // ---------------------------------------------------------------- events window: one line per event, with its icon

        const int EventRows = 8;
        readonly (Image icon, Text text)[] _eventRows = new (Image, Text)[EventRows];
        readonly WorldEvent[] _eventRowEvents = new WorldEvent[EventRows];

        void BuildEventRows()
        {
            _events.Body.gameObject.SetActive(false);
            for (int k = 0; k < EventRows; k++)
            {
                var row = Ui.Node("Event", _events.Root);
                Row(row, 8).childAlignment = TextAnchor.UpperLeft;
                var icon = Ui.Icon(row, null, 26f);
                var t = Ui.Label(row, "", 17);
                Element(t).preferredWidth = 380f;
                _eventRows[k] = (icon, t);
                int slot = k;
                row.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                row.gameObject.AddComponent<Button>().onClick.AddListener(() => FocusEvent(_eventRowEvents[slot])); // click to fly there
                row.gameObject.SetActive(false); // shown once there is news to fill it
            }
        }

        // ---------------------------------------------------------------- thiên mệnh window: soft goals and prayers

        const int DestinyRows = DestinySystem.Slots + 8;
        readonly (Image icon, Text text)[] _destinyRows = new (Image, Text)[DestinyRows];
        readonly Vector2[] _destinyAt = new Vector2[DestinyRows];
        readonly bool[] _destinyPlaced = new bool[DestinyRows];

        void BuildDestinyRows()
        {
            _destiny.Body.fontSize = 17;
            for (int k = 0; k < DestinyRows; k++)
            {
                var row = Ui.Node("Destiny", _destiny.Root);
                Row(row, 8).childAlignment = TextAnchor.UpperLeft;
                var icon = Ui.Icon(row, null, 26f);
                var t = Ui.Label(row, "", 17);
                Element(t).preferredWidth = 380f;
                _destinyRows[k] = (icon, t);
                int slot = k;
                row.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                row.gameObject.AddComponent<Button>().onClick.AddListener(() =>
                {
                    if (!_destinyPlaced[slot]) { ShowToast("Mục tiêu này không gắn với một nơi nào trên bản đồ."); return; }
                    var cam = _game.Camera;
                    cam.GlideTo(_destinyAt[slot], Mathf.Min(cam.Cam.orthographicSize, 28f)); // click to fly there
                });
                row.gameObject.SetActive(false);
            }
        }

        string DestinyProgress(Destiny d, int p)
        {
            switch (d.Kind)
            {
                case DestinyKind.AnswerPrayers: return $"đã đáp {p}/{d.Goal}";
                case DestinyKind.SlayHungThu: return $"nó đã tàn sát thêm {p}/{d.Goal} thành";
                case DestinyKind.Ascend: return $"hiện ở {Realms.Names[Mathf.Clamp(p, 0, (int)Realm.HoaThan)]}";
                case DestinyKind.ProtectSect: return $"{p}/{d.Goal} năm";
                case DestinyKind.Population: return $"{p:N0}/{d.Goal:N0}";
                default: return $"{p}/{d.Goal} nơi";
            }
        }

        void UpdateDestinyRows()
        {
            var sim = _game.Sim;
            var ds = sim.Destiny;
            long tick = sim.Clock.Tick;
            var sb = new StringBuilder();
            sb.Append($"<color=#ffd873>Thiên uy {ds.Merit}</color>  <color=#8890a8>· đã thành {ds.Completed} · không thành {ds.Failed}</color>\n");
            sb.Append("<color=#8890a8>Thế giới tự gợi ý; theo hay không, theo cách nào, tùy Thiên Đạo. Bấm một dòng để tới nơi đó.</color>");
            for (int k = 0; k < Mathf.Min(2, ds.Past.Count); k++)
            {
                var d = ds.Past[k];
                sb.Append($"\n<color={(d.Done ? "#a8e890" : "#ff8a80")}>{(d.Done ? "Đã thành" : "Không thành")}</color> <color=#8890a8>({d.EndTick / SimClock.DaysPerYear + 1})</color> {d.Text}");
            }
            _destiny.Body.text = sb.ToString();

            int shown = 0;
            foreach (var d in ds.Active)
            {
                if (shown >= DestinyRows) break;
                int p = ds.Progress(d, tick);
                long years = Mathf.Max(0, (int)((d.Until - tick) / SimClock.DaysPerYear));
                _destinyPlaced[shown] = ds.Where(d, out _destinyAt[shown]);
                var (icon, text) = _destinyRows[shown++];
                icon.sprite = Icons.Star;
                text.text = $"<color=#ffd873>{d.Text}</color>\n<color=#b8bccc>{DestinyProgress(d, p)} · còn {years} năm · thiên uy +{d.Reward}</color>";
                text.transform.parent.gameObject.SetActive(true);
            }
            foreach (var pr in sim.Faith.Open)
            {
                if (shown >= DestinyRows) break;
                var s = sim.Settlements.All[pr.Settlement];
                _destinyPlaced[shown] = true;
                _destinyAt[shown] = new Vector2(s.X + 0.5f, s.Y + 0.5f);
                var (icon, text) = _destinyRows[shown++];
                icon.sprite = Icons.Incense;
                text.text = $"<color=#ffffff>{s.Name}: {FaithSystem.KindNames[(int)pr.Kind]}</color> <color=#8890a8>· còn {(pr.Until - tick) / SimClock.DaysPerMonth} tháng</color>\n" +
                            $"<color=#b8bccc>{AnswerHint(pr.Kind)}</color>";
                text.transform.parent.gameObject.SetActive(true);
            }
            for (int k = shown; k < DestinyRows; k++) _destinyRows[k].text.transform.parent.gameObject.SetActive(false);
        }

        void UpdateEventRows()
        {
            var events = _game.Sim.Events.Recent;
            int shown = 0;
            for (int k = events.Count - 1; k >= 0 && shown < EventRows; k--)
            {
                var ev = events[k];
                if (ev.Importance < 1) continue;
                _eventRowEvents[shown] = ev;
                var (icon, text) = _eventRows[shown++];
                icon.sprite = Icons.ForEvent(ev.Kind, ev.Fx);
                string color = ev.Importance >= 3 ? "#ffd873" : ev.Importance == 2 ? "#ffffff" : "#b8bccc";
                text.text = $"<color=#8890a8>{ev.Tick / SimClock.DaysPerYear + 1}</color>  <color={color}>{ev.Text}</color>";
                text.transform.parent.gameObject.SetActive(true);
            }
            for (int k = shown; k < EventRows; k++) _eventRows[k].text.transform.parent.gameObject.SetActive(false);
            _events.Body.gameObject.SetActive(shown == 0);
            if (shown == 0) _events.Body.text = "Thiên hạ thái bình.";
        }

        // ---------------------------------------------------------------- stats window: the world in icons

        readonly Ui.Chip[] _statChips = new Ui.Chip[27];

        void BuildStatChips()
        {
            var grid = Ui.Node("StatChips", _stats.Root);
            grid.SetSiblingIndex(1); // under the header, above the hovered-cell text
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(136f, 30f);
            g.spacing = new Vector2(4f, 4f);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 3;
            for (int k = 0; k < _statChips.Length; k++) _statChips[k] = Ui.MakeChip(grid, 136f);
            _stats.Body.fontSize = 16;
        }

        void UpdateStatChips()
        {
            var sim = _game.Sim;
            var cr = sim.Cultivation.CountByRealm;
            var wild = sim.Wildlife;
            var dis = sim.Disasters;
            var n = sim.Events.CountByKind;
            int scars = 0, volcanoes = 0;
            foreach (var l in dis.Landmarks)
                if (l.Alive) { if (l.Kind == Landmark.Thunder) scars++; else volcanoes++; }
            int k = 0;
            _statChips[k++].Set(Icons.Person, $"{sim.Settlements.TotalPopulation:N0}", "Phàm nhân");
            _statChips[k++].Set(Icons.Object(ObjectType.House), $"{sim.Settlements.AliveCount}", "Làng, trấn, thành");
            _statChips[k++].Set(Icons.Unit(Unit.Migrants), $"{sim.Settlements.MigrantGroups}", "Đoàn di dân đang đi tìm đất");
            for (var r = Realm.LuyenKhi; r < Realm.Count; r++) _statChips[k++].Set(Icons.ForRealm(r), $"{cr[(int)r]}", Realms.Names[(int)r]);
            _statChips[k++].Set(Icons.Banner, $"{sim.Factions.AliveCount}", $"Thế lực · lập tông {n[(int)EventKind.Founding]} · ly khai {n[(int)EventKind.Schism]}");
            _statChips[k++].Set(Icons.Sword, $"{sim.Factions.WarCount}", "Cuộc chiến đang diễn ra");
            _statChips[k++].Set(Icons.Wrath, $"{n[(int)EventKind.Destruction]}", "Số lần diệt môn");
            _statChips[k++].Set(Icons.Unit(Unit.Deer), $"{wild.Total(Species.Deer):N0}", "Hươu");
            _statChips[k++].Set(Icons.Unit(Unit.Rabbit), $"{wild.Total(Species.Rabbit):N0}", "Thỏ");
            _statChips[k++].Set(Icons.Unit(Unit.Wolf), $"{wild.Total(Species.Wolf):N0}", "Sói");
            _statChips[k++].Set(Icons.Sun, $"{dis.DroughtCount}", "Vùng đang hạn hán");
            _statChips[k++].Set(Icons.Skull, $"{dis.EpidemicCount}", "Ổ dịch");
            _statChips[k++].Set(Icons.RainCloud, $"{dis.WeatherCount}", "Vùng đang mưa hoặc rét");
            _statChips[k++].Set(Icons.Tribulation, $"{scars}", "Lôi địa");
            _statChips[k++].Set(Icons.Volcano, $"{volcanoes}", "Núi lửa");
            _statChips[k++].Set(Icons.Eclipse, $"{n[(int)EventKind.Calamity]}", "Thiên tai đã xảy ra");
            int kings = 0, found = 0, open = 0;
            foreach (var b in sim.Beasts.All)
                if (b.Alive && b.IsKing) kings++;
            foreach (var r in sim.Relics.All)
            {
                if (r.Open) open++;
                if (r.Open && r.Discovered) found++;
            }
            _statChips[k++].Set(Icons.Unit(Unit.Beast), $"{sim.Beasts.AliveCount}", "Yêu thú");
            _statChips[k++].Set(Icons.Crown, $"{kings}", "Yêu Vương (mỗi người một yêu tộc)");
            _statChips[k++].Set(Icons.Book, $"{found}/{open}", "Bí cảnh đã lộ diện / còn chưa bị vét sạch");
            _statChips[k++].Set(Icons.Unit(Unit.Caravan), $"{sim.Trade.CaravanCount}", $"Thương đội đang đi · đã giao {sim.Trade.Delivered:N0} chuyến");
            _statChips[k++].Set(Icons.Tag, $"{sim.Paths.RoadCells:N0} · {sim.Paths.TrailCells:N0}", "Ô đường đất · ô đường mòn: người đi bộ, di dân, thương đội đi nhiều thành đường; bỏ không thì cỏ mọc lại");
            _statChips[k++].Set(Icons.Orb, $"{sim.Eras.QiFactor(sim.Clock.Tick) * 100f:0}%", $"Linh khí thiên địa theo chu kỳ ({(sim.Eras.Waxing(sim.Clock.Tick) ? "đang dâng" : "đang suy")})");
            while (k < _statChips.Length) _statChips[k++].Hide();
        }

        // Below the chips: the cell under the pointer, and the engine numbers, in small print.
        string StatsText()
        {
            var sim = _game.Sim;
            var w = _game.World;
            var wild = sim.Wildlife;
            var sb = new StringBuilder();
            int hx = _game.HoverX, hy = _game.HoverY;
            if (w.InBounds(hx, hy))
            {
                int i = w.Idx(hx, hy);
                float tempC = -20f + (w.Temperature[i] / 255f + sim.Clock.SeasonalTemperatureOffset) * 60f;
                sb.Append($"<color=#ffd873>Ô ({hx}, {hy})</color> {TerrainInfo.Names[(int)w.Terrain[i]]} · {tempC:0}°C · ẩm {w.Moisture[i] * 100 / 255}%\n");
                sb.Append($"Linh khí {sim.Qi.SampleQi(hx, hy):0} / trần {w.QiCap[i]}{(w.LeyLine[i] ? " · linh mạch" : "")} · cỏ {sim.Forage.At(hx, hy):0}\n");
                int region = wild.RegionOf(hx, hy);
                sb.Append($"Vùng: hươu {wild.At(Species.Deer, region):0} · thỏ {wild.At(Species.Rabbit, region):0} · sói {wild.At(Species.Wolf, region):0}\n");
            }
            var r = _game.Renderer;
            sb.Append($"<color=#8890a8>FPS {_game.Fps:0} · {_game.Camera.PixelsPerCell:0.0} px/ô · chunk {r.ResidentChunks}/{r.ChunkCount} · " +
                      $"{sim.TicksLastFrame} tick/frame · {w.Objects.AliveCount:N0} vật thể · {sim.Log.Count} lệnh · seed {w.Seed}</color>");
            return sb.ToString();
        }

        // How Thiên Đạo can answer a prayer of this kind (more than one way, on purpose).
        static string AnswerHint(PrayerKind kind)
        {
            switch (kind)
            {
                case PrayerKind.Rain: return "ban mưa xuống vùng đó (tab Thời tiết) hoặc ban cơ duyên cho làng";
                case PrayerKind.Cure: return "ban cơ duyên cho làng, ôn dịch sẽ tiêu tan";
                case PrayerKind.Harvest: return "ban cơ duyên cho làng hoặc ban mưa";
                default: return "giáng thiên phạt diệt hung thú (tu sĩ giết được thì dân cảm ơn tu sĩ)";
            }
        }

        // Tin đồn (devlog 29): how far the word of a relic has gone, and how it has grown in the telling.
        string RelicWord(Relic r)
        {
            var word = _game.Sim.Knowledge.Of(RumorKind.Relic, r.Index);
            if (word == null) return "\n<color=#8890a8>Đã có người biết, nhưng tin chưa lan ra ngoài; ai chưa nghe thì sẽ không tới.</color>";
            return $"\n<color=#e8d8a8>Tin đồn đã lan tới {word.Heard.Count} tông môn{HeardNames(word)}</color>" +
                   (word.Exaggeration >= 1.3f ? $"<color=#ff8a80> · đồn thổi gấp {word.Exaggeration:0.0} lần</color>" : "") +
                   "\n<color=#8890a8>Tin loang dần mỗi tháng và theo thương đội, di dân; tông nào nghe muộn thì đến muộn.</color>";
        }

        string HeardNames(Rumor word)
        {
            if (word == null || word.Heard.Count == 0) return "";
            var names = new List<string>();
            for (int k = 0; k < word.Heard.Count && names.Count < 3; k++)
                if (_game.Sim.Factions.Get(word.Heard[k]) is Faction f && f.Alive) names.Add(_game.Sim.Factions.NameOf(f.Id));
            return names.Count == 0 ? "" : ": " + string.Join(", ", names) + (word.Heard.Count > names.Count ? "…" : "");
        }

        void UpdateCard()
        {
            var sim = _game.Sim;
            var sel = _game.Selection;
            _game.Fx.ShownRumor = null; // set again below if the card shows something talked about
            var c = _game.Selected;
            var s = _game.SelectedSettlement;
            bool show = sel.Kind != InspectKind.None;
            if (_card.gameObject.activeSelf != show) _card.gameObject.SetActive(show);
            if (!show) return;

            if (sel.Kind != InspectKind.Cultivator && sel.Kind != InspectKind.Settlement)
            {
                _cardPortrait.sprite = InspectIcon(sel);
                _cardTitle.text = InspectTitle(sel);
                _cardSub.text = "";
                _cardBody.text = InspectBody(sel);
                _cardBody.gameObject.SetActive(_cardBody.text.Length > 0);
                if (sel.Kind == InspectKind.Beast) BeastChips(sim.Beasts.ForEntity(sel.Entity));
                EndChips();
                _cardBar1Root.gameObject.SetActive(false);
                _cardBar2Root.gameObject.SetActive(false);
                _followButton.Frame.gameObject.SetActive(sel.Kind == InspectKind.Migrants || sel.Kind == InspectKind.Beast || sel.Kind == InspectKind.Caravan);
                ShowDivineButtons(sel.Kind == InspectKind.Beast || sel.Kind == InspectKind.Migrants || sel.Kind == InspectKind.Caravan || sel.Kind == InspectKind.Animal, false, false, true);
                ShowRevive(false);
                _watchButton.Frame.gameObject.SetActive(false);
                _followButton.SetSelected(_game.Follow);
                return;
            }

            if (c != null)
            {
                long tick = sim.Clock.Tick;
                _cardPortrait.sprite = Icons.Cultivator(c);
                _cardTitle.text = c.Title;
                string where = c.SectId >= 0 ? $"{sim.Cultivation.Role(c)} · {sim.Cultivation.SectName(c)}" : sim.Cultivation.Role(c);
                _cardSub.text = c.Alive ? $"{where} · {c.Activity}" : where;
                var bad = new Color(1f, 0.55f, 0.5f);
                if (!c.Alive)
                {
                    var killer = c.KilledBy >= 0 ? sim.Cultivation.All[c.KilledBy] : null;
                    Chip(Icons.Skull, $"Năm {c.DeathTick / SimClock.DaysPerYear + 1}", killer != null ? $"Vẫn lạc dưới tay {killer.Title}" : "Năm vẫn lạc", bad);
                    Chip(Icons.ForRealm(c.Realm, c.Demonic), c.RealmText, "Cảnh giới lúc mất");
                    if (killer != null) Chip(Icons.Sword, killer.Name, $"Kẻ đã giết: {killer.Title}", bad);
                }
                else
                {
                    Chip(Icons.ForRealm(c.Realm, c.Demonic), Realms.Names[(int)c.Realm], $"{c.RealmText}{(c.Demonic ? " · ma tu" : "")}", c.Demonic ? new Color(1f, 0.5f, 0.5f) : Ui.Gold);
                    HpChip(CombatSystem.HpOf(c), CombatSystem.MaxHp(c), CombatSystem.Wounded(c) ? "trọng thương, ở nhà dưỡng thương; sức chiến đấu giảm" : "");
                    MethodChip(c);
                    ClanChip(c);
                    Chip(Icons.Seed, $"{RootShort(c.Roots)} · {SpiritRoots.Elements(c.Roots)}",
                        $"{SpiritRoots.Kind(c.Roots)} ({SpiritRoots.Elements(c.Roots)}) · tốc độ tu luyện ×{SpiritRoots.SpeedMultiplier(c.Roots):0.0}");
                    float qi = sim.Qi.SampleQi((int)c.HomeX, (int)c.HomeY), need = Realms.RequiredQi[(int)c.Realm];
                    Chip(Icons.Orb, $"{qi:0}", $"Linh khí nơi ở (cần {need:0} để tu luyện hết tốc độ)", qi < need ? bad : (Color?)null);
                    Chip(Icons.Eye, $"{c.Comprehension * 100f:0}", "Ngộ tính");
                    Chip(Icons.Heart, $"{c.DaoHeart * 100f:0}", "Tâm cảnh");
                    Chip(Icons.Star, $"{c.Luck * 100f:0}", "Khí vận");
                    if (c.Kills > 0) Chip(Icons.Sword, $"{c.Kills}", "Số tu sĩ đã giết");
                    if (c.Stones > 0) Chip(Icons.Gem, $"{c.Stones:N0}", "Linh thạch trong túi trữ vật");
                    if (c.Pills > 0) Chip(Icons.Pill, $"{c.Pills}", $"{Lore.PillFor(c.Realm + 1)} (giúp đột phá)");
                    if (c.Treasures > 0) Chip(Icons.Crown, $"{c.Treasures}", $"Pháp bảo: {c.TreasureName}");
                    if (c.FailedAttempts > 0) Chip(Icons.Skull, $"{c.FailedAttempts}", "Số lần đột phá thất bại", bad);
                }
                if (c.Legend) Chip(Icons.Book, "Truyền kỳ", $"Lưu danh sử sách: {c.Epithet}", Ui.Gold);
                EndChips();

                // Ties and the latest deeds, briefly; the full life is in Biên niên sử.
                var sb = new StringBuilder();
                var master = sim.Cultivation.MasterOfDisciple(c);
                if (master != null) sb.Append($"Sư phụ {master.Name}{(master.Alive ? "" : " (đã mất)")}");
                if (c.Nemesis >= 0)
                    sb.Append(sb.Length > 0 ? " · " : "").Append($"<color=#ff8a6a>Huyết thù: {sim.Cultivation.All[c.Nemesis].Name}</color>");
                sim.History.OfCultivator(c.Index, _bio, 4);
                for (int k = _bio.Count - 1; k >= 0; k--)
                    sb.Append(sb.Length > 0 ? "\n" : "").Append($"<color=#8890a8>{_bio[k].Year}</color> {_bio[k].Text}");
                int more = sim.History.CountOfCultivator(c.Index) - _bio.Count;
                if (more > 0) sb.Append($"\n<color=#8890a8>+{more} sự tích (H)</color>");
                _cardBody.text = sb.ToString();
                _cardBody.gameObject.SetActive(sb.Length > 0);
                _cardBar1Root.gameObject.SetActive(c.Alive);
                _cardBar2Root.gameObject.SetActive(c.Alive);
                if (c.Alive)
                {
                    float need = Realms.Need(c.Realm, c.Stage);
                    _cardBar1.fillAmount = Mathf.Clamp01(c.Progress / need);
                    string stage = c.Realm == Realm.LuyenKhi ? $"tầng {c.Stage + 1}" : Realms.StageNames[Mathf.Min(c.Stage, 3)];
                    _cardBar1Text.text = $"{stage} {Mathf.Clamp01(c.Progress / need) * 100f:0}%";
                    _cardBar2.fillAmount = Mathf.Clamp01(c.AgeYears(tick) / c.LifespanYears);
                    _cardBar2Text.text = $"{c.AgeYears(tick):0}/{c.LifespanYears}";
                }
                _followButton.Frame.gameObject.SetActive(c.Alive);
                _watchButton.Frame.gameObject.SetActive(true);
                _watchButton.SetSelected(c.Watched);
                _watchButton.Frame.GetComponent<Tooltip>().Text = c.Watched ? "Bỏ theo dõi" : "Theo dõi: người này sẽ sống như nhân vật chính";
                ShowDivineButtons(c.Alive, false);
                ShowRevive(!c.Alive);
                _followButton.SetSelected(_game.Follow);
                return;
            }

            int pop = Mathf.Max(1, s.Population);
            _cardPortrait.sprite = Icons.Object(s.Sect ? ObjectType.SectHall : ObjectType.House);
            _cardTitle.text = s.Name;
            var body = new StringBuilder();
            var f = s.Sect ? sim.Factions.Get(s.Id) : null;
            var head = s.Sect ? sim.Cultivation.MasterOf(s.Id) : null;
            var protector = !s.Sect && s.Alive ? sim.Factions.ProtectorOf(s.X, s.Y) : null;
            _cardSub.text = !s.Alive ? "<color=#ff9090>Đã bị bỏ hoang</color>"
                : s.Sect ? $"{(f != null && f.Demonic ? "Ma đạo" : "Chính đạo")}{(head != null ? $" · tông chủ {head.Name} ({Realms.Names[(int)head.Realm]})" : "")}"
                : protector != null ? $"Dưới sự che chở của {protector.BaseName}" : "Không thuộc tông môn nào";
            if (s.Alive)
            {
                var warn = new Color(1f, 0.55f, 0.5f);
                Chip(Icons.Person, $"{s.Population}", "Dân số");
                Chip(Icons.Object(ObjectType.House), $"{s.Houses.Count}", "Nhà");
                Chip(Icons.TerrainTile(Terrain.Farmland), $"{s.Farms.Count}", "Ô ruộng");
                Chip(Icons.Bowl, $"{s.Food / pop:0.0} th", "Lương thực còn đủ ăn mấy tháng", s.Food / pop < 1f ? warn : (Color?)null);
                Chip(Icons.Seed, $"+{s.BirthsLastYear}", "Sinh năm qua");
                Chip(Icons.Skull, $"-{s.DeathsLastYear}", "Mất năm qua", s.DeathsLastYear > s.BirthsLastYear ? warn : (Color?)null);
                if (sim.Disasters.IsInfected(s.Id)) Chip(Icons.Skull, "Ôn dịch", "Ôn dịch đang hoành hành", new Color(0.6f, 0.9f, 0.5f));
                int drought = sim.Disasters.DroughtMonthsLeft(s.X, s.Y, sim.Clock.Tick);
                if (drought >= 0) Chip(Icons.Sun, $"{drought} th", "Đại hạn, còn khoảng chừng ấy tháng", new Color(1f, 0.75f, 0.4f));
                // Bất mãn with the crown, and the rising they have joined.
                var rising = sim.Politics.RebellionAt(s.Id);
                Chip(Icons.Torch, $"{s.Unrest:0}",
                    "Bất mãn với triều đình (0–100). Đói kém, chết chóc, hạn hán, ôn dịch, vua bạo ngược, xa kinh đô làm tăng; " +
                    "tông môn bảo hộ, miếu thờ, gia tộc tu tiên trên ngai vàng, kinh thành làm giảm. Từ 75 có thể khởi nghĩa",
                    s.Unrest >= 75f ? warn : s.Unrest >= 55f ? new Color(1f, 0.75f, 0.4f) : (Color?)null);
                if (rising != null)
                    Chip(Icons.Torch, rising.Usurper ? "Tranh ngôi" : "Khởi nghĩa", $"Theo {rising.Leader} chống triều đình, đã {(sim.Clock.Tick - rising.Start) / SimClock.DaysPerYear} năm", warn);
                // Tín ngưỡng, and the prayer they are waiting on.
                var prayer = sim.Faith.PrayerOf(s.Id);
                if (prayer != null)
                    Chip(Icons.Incense, $"{FaithSystem.KindShort[(int)prayer.Kind]} · {(prayer.Until - sim.Clock.Tick) / SimClock.DaysPerMonth} th",
                        $"Đang {FaithSystem.KindNames[(int)prayer.Kind]}, chờ Thiên Đạo đáp lời: {AnswerHint(prayer.Kind)}. Làm ngơ thì lòng tin nguội lạnh",
                        new Color(1f, 0.85f, 0.45f));
                Chip(Icons.Incense, $"{s.Faith:0}",
                    "Tín ngưỡng Thiên Đạo (0–100). Đáp lời cầu nguyện thì tăng, làm ngơ hay giáng thiên phạt thì giảm. " +
                    $"Từ {FaithSystem.ShrineFaith:0} dựng miếu; từ 80 trời có thể giáng phúc, sinh người có linh căn; dưới {FaithSystem.ShrineFloor:0} bỏ miếu; gần 0 dễ sinh tà giáo, ma tu",
                    s.Faith >= FaithSystem.ShrineFaith ? new Color(1f, 0.85f, 0.45f) : s.Faith < FaithSystem.ShrineFloor ? new Color(0.75f, 0.55f, 0.95f) : (Color?)null);
                // The market: stock and price against the usual (red when dear).
                var m = sim.Trade.MarketOf(s);
                Color? Dear(Good g) => sim.Trade.PriceFactor(s, g) > 1.5f ? warn : sim.Trade.PriceFactor(s, g) < 0.7f ? new Color(0.6f, 0.95f, 0.6f) : (Color?)null;
                Chip(Icons.Bowl, $"giá ×{sim.Trade.PriceFactor(s, Good.Food):0.0}", "Giá lương thực ở chợ so với bình thường", Dear(Good.Food));
                Chip(Icons.Herb, $"{m.Herbs:0} · ×{sim.Trade.PriceFactor(s, Good.Herb):0.0}", "Linh thảo trong kho · giá so với bình thường", Dear(Good.Herb));
                Chip(Icons.Pill, $"{m.Pills:0} · ×{sim.Trade.PriceFactor(s, Good.Pill):0.0}", "Đan dược trong kho · giá so với bình thường", Dear(Good.Pill));
                if (m.CaravansSent + m.CaravansReceived > 0)
                    Chip(Icons.Unit(Unit.Caravan), $"{m.CaravansSent}/{m.CaravansReceived}", "Thương đội đã gửi đi / đã nhận");
            }
            if (s.Sect)
            {
                var count = new int[(int)Realm.Count];
                foreach (var m in sim.Cultivation.All)
                    if (m.Alive && m.SectId == s.Id) count[(int)m.Realm]++;
                for (var r = Realm.LuyenKhi; r < Realm.Count; r++)
                    if (count[(int)r] > 0) Chip(Icons.ForRealm(r), $"{count[(int)r]}", Realms.Names[(int)r]);
                var method = sim.Techniques.OfSect(s.Id);
                if (method != null)
                    Chip(Icons.JadeSlip, method.Name,
                        $"Trấn phái công pháp: {method.GradeText}{(method.Demonic ? ", ma công" : "")}, hệ {method.ElementText}, đệ tử tu được tới {Realms.Names[(int)method.Ceiling]}. " +
                        "Tông bị diệt thì công pháp thất truyền vào phế tích; ai nhặt được có thể khôi phục sơn môn",
                        method.Grade >= 4 ? new Color(1f, 0.85f, 0.45f) : (Color?)null);
                if (f != null && f.Alive)
                {
                    Chip(Icons.Banner, $"{f.Tiles}", "Vùng lãnh thổ");
                    Chip(Icons.LeyLine, $"{f.LeyTiles}", "Vùng có linh mạch");
                    Chip(Icons.Gem, $"{f.Treasury:N0}", $"Linh thạch (+{f.LastIncome:0}/năm)");
                    Chip(Icons.Sword, $"{f.Power:N0}", $"Thực lực · thắng {f.BattlesWon}, thua {f.BattlesLost}, tử trận {f.Fallen}");
                    string ties = TiesText(f.Id, 3);
                    if (ties.Length > 0) body.Append(ties);
                    // What word has reached this mountain gate: what they can act on.
                    var news = new List<string>();
                    foreach (var word in sim.Knowledge.All)
                    {
                        if (news.Count >= 3 || !word.Heard.Contains(s.Id)) continue;
                        if (word.Kind == RumorKind.Relic && word.Subject < sim.Relics.All.Count)
                            news.Add(sim.Relics.All[word.Subject].Name + (word.Exaggeration >= 1.3f ? " (đồn thổi)" : ""));
                        else if (word.Kind == RumorKind.HungThu && word.Subject < sim.Beasts.All.Count) news.Add(sim.Beasts.All[word.Subject].Title + " đang tàn sát");
                        else if (word.Kind == RumorKind.WeakSect && word.Subject != s.Id) news.Add($"{sim.Factions.NameOf(word.Subject)} đang suy yếu");
                    }
                    if (news.Count > 0) body.Append(body.Length > 0 ? "\n" : "").Append($"<color=#e8d8a8>Nghe đồn:</color> {string.Join(" · ", news)}");
                }
                // Sử sách of the sect: its greatest moments.
                sim.History.OfFaction(s.Id, _bio, 30);
                int told = 0;
                foreach (var r in _bio)
                {
                    if (r.Importance < 3 || told >= 3) continue;
                    told++;
                    body.Append(body.Length > 0 ? "\n" : "").Append($"<color=#8890a8>{r.Year}</color> {r.Text}");
                }
            }
            EndChips();
            var wd = _game.World;
            var kingdom = s.Kingdom >= 0 && s.Kingdom < wd.Kingdoms.Count ? wd.Kingdoms[s.Kingdom] : null;
            string regionName = wd.Lore.RegionNames[wd.Region[wd.Idx(s.X, s.Y)]];
            body.Append(body.Length > 0 ? "\n" : "").Append($"<color=#e8d8a8>{(s.Capital ? "Kinh thành của " : "")}{(kingdom != null ? kingdom.Name + " · " : "")}{regionName}</color>");
            // The crown over it (PoliticsSystem), and the gia tộc whose ancestral land it is (ClanSystem).
            if (kingdom != null && !kingdom.Fallen && kingdom.Ruler != null)
            {
                long now = sim.Clock.Tick;
                var royal = kingdom.RoyalClan >= 0 && kingdom.RoyalClan < sim.Clans.All.Count ? sim.Clans.All[kingdom.RoyalClan] : null;
                body.Append($"\n<color=#e8d8a8>{kingdom.Name}: vua {kingdom.Ruler}</color> ({PoliticsSystem.RulerAge(kingdom, now)} tuổi, {PoliticsSystem.Temper(kingdom)})" +
                            $" · triều {kingdom.Dynasty} {Mathf.Max(0, PoliticsSystem.DynastyYears(kingdom, now))} năm{(royal != null ? $" ({royal.Title})" : "")} · ổn định {kingdom.Stability:0}");
                var war = sim.Politics.RebellionOf(kingdom.Id);
                if (war != null)
                    body.Append($"\n<color=#ff8a80>Nội chiến: {war.Leader} {(war.Usurper ? "tranh ngôi" : "khởi nghĩa")} ở {sim.Settlements.All[war.Seat].Name}, {war.Towns.Count} thành theo, đã {(now - war.Start) / SimClock.DaysPerYear} năm</color>");
            }
            var clanSeat = sim.Clans.SeatedAt(s.Id);
            if (clanSeat != null)
            {
                var feuds = new List<string>();
                foreach (int fe in clanSeat.Feuds) if (feuds.Count < 2) feuds.Add(sim.Clans.All[fe].Title);
                body.Append($"\n<color=#e8d8a8>Đất tổ của {clanSeat.Title}</color> · {clanSeat.Members} tu sĩ mang họ {clanSeat.Name}" +
                            (feuds.Count > 0 ? $" · <color=#ff8a80>thế thù với {string.Join(", ", feuds)}</color>" : ""));
            }
            body.Append(BuildingsLine(s));
            body.Append($"\n<color=#ff8a80>Phàm nhân: {SpeciesInfo.Hp[(int)Species.Migrants]:0} máu mỗi người</color><color=#8890a8> · trước yêu thú và tu sĩ thì chỉ là con kiến</color>");
            body.Append("\n").Append($"<color=#8890a8>Lập năm {s.FoundedTick / SimClock.DaysPerYear + 1}" +
                        (s.ParentId >= 0 ? $" · di dân từ {sim.Settlements.All[s.ParentId].Name}" : "") + "</color>");
            _cardBody.text = body.ToString();
            _cardBody.gameObject.SetActive(true);
            _cardBar1Root.gameObject.SetActive(false);
            _cardBar2Root.gameObject.SetActive(false);
            _followButton.Frame.gameObject.SetActive(false);
            ShowDivineButtons(s.Alive, true, s.Sect);
            ShowRevive(false);
            _watchButton.Frame.gameObject.SetActive(false);
        }

        // What a place has built and what each thing does for it, so the player can see why one town weathers
        // a beast tide that wipes out the next.
        string BuildingsLine(Settlement s)
        {
            var st = _game.Sim.Settlements;
            var parts = new List<string>();
            string[] standing = { "thôn", "trấn", "thành", "kinh thành" };
            if (s.Walled) parts.Add("tường thành (thú triều giết ít hơn, dân tị nạn tìm về)");
            if (st.HasCivic(s, ObjectType.Market)) parts.Add("chợ (thương nhân tấp nập)");
            if (st.HasCivic(s, ObjectType.Shrine)) parts.Add("miếu (ôn dịch nhẹ hơn)");
            if (st.HasCivic(s, ObjectType.Pagoda)) parts.Add("bảo tháp");
            if (st.HasCivic(s, ObjectType.Palace)) parts.Add("hoàng cung");
            string line = $"\n<color=#c8d0a0>Quy mô {standing[SettlementSystem.Standing(s)]}" + (parts.Count > 0 ? ": " + string.Join(", ", parts) : "") + "</color>";
            return s.Sect ? "" : line;
        }

        void ShowRevive(bool on)
        {
            if (_reviveButton.Frame.gameObject.activeSelf != on) _reviveButton.Frame.gameObject.SetActive(on);
        }

        // Sinh lực (máu): current / whole, red when low; the tooltip says what the wounds mean.
        // Gia tộc: the house they belong to, its seat, its standing and its blood feuds.
        void ClanChip(Cultivator c)
        {
            var sim = _game.Sim;
            var clan = sim.Clans.Of(c);
            if (clan == null) return;
            string seat = clan.Seat >= 0 && clan.Seat < sim.Settlements.All.Count ? sim.Settlements.All[clan.Seat].Name : "?";
            var feuds = new List<string>();
            foreach (int f in clan.Feuds) if (feuds.Count < 3) feuds.Add(sim.Clans.All[f].Title);
            Chip(Icons.Person, clan.Title,
                $"Người của {clan.Title} · đất tổ {seat} · {clan.Members} tu sĩ cùng họ · uy danh {clan.Prestige:0}" +
                (clan.Royal >= 0 && clan.Royal < sim.World.Kingdoms.Count ? $" · hoàng tộc của {sim.World.Kingdoms[clan.Royal].Name}" : "") +
                (feuds.Count > 0 ? $" · thế thù với {string.Join(", ", feuds)}: gặp nhau là đánh" : ""),
                clan.Royal >= 0 ? Ui.Gold : clan.Feuds.Count > 0 ? new Color(1f, 0.6f, 0.55f) : (Color?)null);
        }

        // Công pháp: its name, and a warning when it is the ceiling holding them back.
        void MethodChip(Cultivator c)
        {
            var ts = _game.Sim.Techniques;
            var t = ts.Of(c);
            bool capped = ts.Capped(c);
            string fit = t.Element < 0 ? "vạn năng" : (c.Roots & (1 << t.Element)) != 0 ? $"hệ {t.ElementText}, hợp linh căn" : $"hệ {t.ElementText}, không hợp linh căn";
            Chip(Icons.JadeSlip, t.Name,
                $"Công pháp {t.GradeText}{(t.Demonic ? " (ma công)" : "")} · {fit} · tốc độ tu luyện ×{TechniqueSystem.SpeedFor(t, c):0.00} · tu được tới {Realms.Names[(int)t.Ceiling]}" +
                (t.Sect >= 0 ? $" · trấn phái công pháp của {_game.Sim.Factions.NameOf(t.Sect)}" : "") +
                (capped ? ". Đã chạm trần công pháp: phải tìm được công pháp cao hơn (bí cảnh, sư môn, đoạt từ kẻ khác) mới đột phá được" : ""),
                capped ? new Color(1f, 0.55f, 0.5f) : t.Grade >= 4 ? new Color(1f, 0.85f, 0.45f) : (Color?)null);
        }

        void HpChip(float hp, float max, string note)
        {
            float frac = max > 0f ? hp / max : 1f;
            var color = frac < 0.4f ? new Color(1f, 0.45f, 0.42f) : frac < 0.75f ? new Color(1f, 0.8f, 0.5f) : (Color?)null;
            Chip(Icons.Blood, $"{hp:N0}/{max:N0}", "Sinh lực (máu)" + (note.Length > 0 ? " · " + note : ""), color);
        }

        void BeastChips(Beast b)
        {
            if (b == null) return;
            var sim = _game.Sim;
            var red = new Color(1f, 0.55f, 0.5f);
            string kind = BeastSystem.KindNames[(int)b.Kind] + (BeastSystem.Flies(b.Kind) ? ", biết bay" : "");
            _cardSub.text = b.Rampage ? $"Hung thú ({kind}) · đã tàn sát {b.Ravaged} nơi" :
                b.IsKing ? $"Yêu Vương của {b.ClanName} ({kind})" : b.Clan >= 0 ? $"Thuộc {sim.Beasts.KingOf(b).ClanName} ({kind})" : $"Yêu thú tự do ({kind})";
            Chip(Icons.Unit(SpriteLibrary.BeastUnit((int)b.Kind, b.Grade)), b.GradeText, $"Giai {b.Grade} · sức mạnh ngang {Realms.Names[(b.Grade + 1) / 2]}", Ui.Gold);
            HpChip(BeastSystem.HpOf(b), BeastSystem.MaxHp(b), b.Rampage ? "hung thú hồi máu chậm: vết thương từ các trận trước vẫn còn" : "");
            if (b.Rampage)
            {
                int hunters = sim.Beasts.HuntersOf(b);
                Chip(Icons.Sword, hunters > 0 ? $"{hunters}" : "—", hunters > 0 ? $"Liên minh trảm yêu: {hunters} cao thủ đang truy sát" : "Chưa có liên minh nào dám ra tay", red);
                var word = sim.Knowledge.Of(RumorKind.HungThu, b.Index);
                Chip(Icons.Scroll, $"{sim.Knowledge.HeardCount(word)} tông", $"Tin dữ về nó đã lan tới {sim.Knowledge.HeardCount(word)} tông môn{HeardNames(word)}. Chỉ tông đã nghe tin mới có thể vào liên minh trảm yêu; vòng loang của tin đồn hiện trên bản đồ");
                _game.Fx.ShownRumor = word;
            }
            Chip(Icons.Hourglass, $"{b.AgeYears(sim.Clock.Tick):0}/{b.LifespanYears}", "Tuổi / thọ nguyên");
            Chip(Icons.Skull, $"{b.Kills}", "Số người đã giết", b.Kills > 0 ? red : (Color?)null);
            Chip(Icons.Orb, $"{sim.Qi.SampleQi((int)b.HomeX, (int)b.HomeY):0}", "Linh khí nơi hang ổ (càng dày, lên giai càng nhanh)");
            if (b.IsKing) Chip(Icons.Crown, $"{sim.Beasts.ClanSize(b)}", "Số yêu thú thuộc hạ", Ui.Gold);
            if (b.Humanoid) Chip(Icons.Person, "Hóa hình", "Đã hóa thành hình người");
        }

        static string RootShort(int roots)
        {
            string k = SpiritRoots.Kind(roots);
            return k.StartsWith("Thiên") ? "Thiên" : k.StartsWith("Chân") ? "Chân" : k.StartsWith("Ngụy") ? "Ngụy" : "Dị";
        }

        Sprite InspectIcon(in InspectTarget t)
        {
            var w = _game.World;
            switch (t.Kind)
            {
                case InspectKind.Migrants: return Icons.Unit(Unit.Migrants);
                case InspectKind.Beast: return Icons.Unit(Unit.Beast);
                case InspectKind.Caravan: return Icons.Unit(Unit.Caravan);
                case InspectKind.Animal:
                    var kind = WildlifeSystem.Kinds[t.Animal];
                    return Icons.Unit(kind == Species.Deer ? Unit.Deer : kind == Species.Rabbit ? Unit.Rabbit : Unit.Wolf);
                case InspectKind.Object:
                    return w.Objects.IsAlive(t.ObjectId) ? Icons.Object(w.Objects.Get(t.ObjectId).Type) : Icons.Erase;
                default:
                    return Icons.TerrainTile(w.Terrain[w.Idx(t.CellX, t.CellY)]);
            }
        }

        string InspectTitle(in InspectTarget t)
        {
            var w = _game.World;
            switch (t.Kind)
            {
                case InspectKind.Migrants: return "Đoàn di dân";
                case InspectKind.Beast:
                {
                    var b = _game.Sim.Beasts.ForEntity(t.Entity);
                    return b != null ? b.Title : "Yêu thú đã chết";
                }
                case InspectKind.Caravan: return "Thương đội";
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
                case InspectKind.Beast:
                    if (sim.Beasts.ForEntity(t.Entity) == null) sb.Append("<color=#8890a8>Đã bị trảm sát hoặc chết già.</color>");
                    break;
                case InspectKind.Caravan:
                {
                    var c = sim.Trade.ForEntity(t.Entity);
                    if (c == null)
                    {
                        sb.Append("<color=#8890a8>Thương đội đã tới nơi.</color>");
                        break;
                    }
                    var all = sim.Settlements.All;
                    sb.Append($"{all[c.From].Name} → {all[c.To].Name}\n");
                    sb.Append($"Chở {c.Amount:0} {TradeSystem.GoodNames[(int)c.Good]} · đi được {(sim.Clock.Tick - c.Start) / (float)SimClock.DaysPerMonth:0} tháng\n");
                    sb.Append($"<color=#ff8a80>Sinh lực: {SpeciesInfo.Hp[(int)Species.Caravan]:0} máu mỗi người</color> · phàm nhân, gặp yêu thú hay lũ lụt là mất mạng");
                    break;
                }
                case InspectKind.Migrants:
                    if (sim.Entities.Species[t.Entity] != Species.Migrants || !sim.Settlements.MigrantInfo(t.Entity, out var from, out int people, out float food))
                    {
                        sb.Append("<color=#8890a8>Đoàn đã dừng chân lập làng hoặc tan rã.</color>");
                        break;
                    }
                    sb.Append($"{people} người · lương thực mang theo {food / Mathf.Max(1, people):0.0} tháng\n");
                    sb.Append($"<color=#ff8a80>Sinh lực: {SpeciesInfo.Hp[(int)Species.Migrants]:0} máu mỗi người</color> (cả đoàn {people * SpeciesInfo.Hp[(int)Species.Migrants]:N0})\n");
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
                    sb.Append($"<color=#ff8a80>Sinh lực: {SpeciesInfo.Hp[(int)kind]:0} máu mỗi con</color>\n");
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
                        if (ObjectInfo.IsRelic(o.Type)) AppendRelic(sb, t.ObjectId);
                        else if (ObjectInfo.IsBuilding(o.Type)) sb.Append(" · <color=#8890a8>không thuộc làng nào</color>");
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

        // Thiên Đạo sees what mortals do not: what a bí cảnh holds and whether anyone has found it yet.
        void AppendRelic(StringBuilder sb, int objectId)
        {
            var sim = _game.Sim;
            Relic r = null;
            foreach (var x in sim.Relics.All)
                if (x.ObjectId == objectId) { r = x; break; }
            if (r == null) return;
            sb.Append($"\n<color=#9fe0d0>{r.Name}</color> · {r.Origin} · nguy hiểm cấp {r.Tier}/5");
            if (!r.Open) { sb.Append("\nĐã bị vét sạch."); return; }
            var inside = new List<string>();
            if (r.Treasure != null) inside.Add(r.Kind == RelicKind.Treasure ? $"linh vật {r.Treasure}" : $"pháp bảo {r.Treasure}");
            if (r.Stones > 0f) inside.Add($"{r.Stones:0} linh thạch");
            if (r.Pills > 0) inside.Add($"{r.Pills} viên đan");
            if (r.Technique >= 0) { var t = sim.Techniques.All[r.Technique]; inside.Add($"ngọc giản {t.Name} ({t.GradeText}, tới {Realms.Names[(int)t.Ceiling]}{(t.Lost ? ", thất truyền" : "")})"); }
            inside.Add(r.Kind == RelicKind.Treasure ? "thọ nguyên" : "truyền thừa");
            sb.Append($"\nBên trong: {string.Join(", ", inside)} · còn {r.Layers} tầng");
            sb.Append(sim.Relics.Contested(r) ? "\n<color=#ff8070>Các thế lực đang tranh đoạt cơ duyên nơi đây.</color>"
                : r.Discovered ? RelicWord(r) : "\n<color=#8890a8>Chưa ai phát hiện, chờ người có cơ duyên.</color>");
            _game.Fx.ShownRumor = sim.Knowledge.Of(RumorKind.Relic, r.Index); // the reach of the word, drawn on the map
        }

        void AppendCell(StringBuilder sb, int x, int y)
        {
            var sim = _game.Sim;
            var w = _game.World;
            int i = w.Idx(x, y);
            var terrain = w.Terrain[i];
            float tempC = -20f + (w.Temperature[i] / 255f + sim.Clock.SeasonalTemperatureOffset) * 60f;
            sb.Append($"<color=#ffd873>Ô ({x}, {y})</color> {TerrainInfo.Names[(int)terrain]} · độ cao {w.Height[i]:0.00}\n");
            // Where on the continent: great region and kingdom.
            var kingdom = w.KingdomAt(i);
            string regionName = TerrainInfo.IsLand(terrain) ? w.Lore.RegionNames[w.Region[i]] : w.Lore.Sea;
            sb.Append($"<color=#e8d8a8>{w.Lore.Continent} đại lục · {regionName}{(kingdom != null ? " · " + kingdom.Name : "")}</color>\n");
            sb.Append($"Nhiệt độ {tempC:0}°C · độ ẩm {w.Moisture[i] * 100 / 255}%");
            if (TerrainInfo.IsLand(terrain)) sb.Append($" · màu mỡ {w.Fertility(i) * 100f:0}%");
            if ((w.Zone[i] & ZoneFlags.Road) != 0) sb.Append($" · <color=#e0c08a>đường đất</color> ({sim.Paths.TreadsAt(i)} lượt qua)");
            else if ((w.Zone[i] & ZoneFlags.Trail) != 0) sb.Append($" · <color=#e0c08a>đường mòn</color> ({sim.Paths.TreadsAt(i)}/{PathSystem.RoadAt} lượt để thành đường đất)");
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
                    : mark.Kind == Landmark.Battlefield
                    ? $"<color=#d07070>{mark.Name}</color>, {mark.Origin}; oán khí của {mark.Toll} người vẫn còn, tan sau khoảng {(mark.Until - sim.Clock.Tick) / SimClock.DaysPerYear} năm · ma tu tu luyện nhanh hơn, lâu ngày sinh yêu thú\n"
                    : $"<color=#ff9a6a>{mark.Name}</color>, {mark.Origin}\n");
            if (terrain == Terrain.Lava) sb.Append("Dung nham đang chảy: ai rơi vào là chết cháy, nguội dần thành đá\n");
            byte scar = w.Scar[i];
            if (scar != 0 && TerrainInfo.IsLand(terrain))
            {
                var kind = ScarInfo.Kind(scar);
                int years = ScarInfo.Strength(scar) * ScarInfo.YearsPerStep[(int)kind];
                string effect = ScarInfo.EffectText(scar);
                sb.Append($"<color=#d8b894>Vết tích: {ScarInfo.Names[(int)kind]}</color>{(effect.Length > 0 ? " · " + effect : "")} · đất lành lại sau khoảng {years} năm\n");
            }
            int drought = sim.Disasters.DroughtMonthsLeft(x, y, sim.Clock.Tick);
            if (drought >= 0) sb.Append($"<color=#ffb060>Đang hạn hán</color>, còn khoảng {drought} tháng · mùa màng chỉ được một phần tư\n");
            int spell = sim.Disasters.WeatherMonthsLeft(x, y, sim.Clock.Tick, out var weather);
            if (spell >= 0)
                sb.Append(weather == Calamity.Rain ? $"<color=#80b0ff>Mưa thuận gió hòa</color>, còn khoảng {spell} tháng\n"
                                                   : $"<color=#e8f0ff>Rét đậm</color>, còn khoảng {spell} tháng · mùa màng mất trắng\n");
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
            float ppc = _game.Camera.PixelsPerCell;
            var world = _game.World;
            if (ShowLabels && ppc < 6f)
            {
                // The map of the continent: great regions in wide capitals, kingdoms beneath, the sea by name.
                var regionColor = new Color(1f, 0.96f, 0.86f, 0.9f);
                foreach (var r in world.Regions)
                    PlaceLabel(ref used, cam, new Vector3(r.LabelX, r.LabelY, 0f), Spaced(r.Name), regionColor, ppc < 2f ? 40 : 34);
                if (world.SeaLabelX >= 0)
                    PlaceLabel(ref used, cam, new Vector3(world.SeaLabelX, world.SeaLabelY, 0f), Spaced(world.Lore.Sea), new Color(0.82f, 0.9f, 1f, 0.85f), 36);
                if (ppc >= 1.2f)
                    foreach (var k in world.Kingdoms)
                        PlaceLabel(ref used, cam, new Vector3(k.CapitalX + 0.5f, k.CapitalY - 6f, 0f), k.Fallen ? "Cố " + k.Name : k.Name, k.Fallen ? new Color(0.75f, 0.72f, 0.66f, 0.8f) : new Color(1f, 0.88f, 0.62f, 0.95f), 26);
            }
            if (ShowLabels && ppc >= 3f)
            {
                foreach (var s in sim.Settlements.All)
                {
                    if (!s.Alive) continue;
                    var f = s.Sect ? sim.Factions.Get(s.Id) : null;
                    // Sects in a light tint of their colour; mortal villages in plain ink.
                    var color = f != null ? Color.Lerp(f.Color, Color.white, 0.45f) : Ui.Ink;
                    PlaceLabel(ref used, cam, new Vector3(s.X + 0.5f, s.Y + 5f, 0f), $"{s.Name} ({s.Population})", color, 20);
                }
                // Risings against the crown, over the town they began in.
                foreach (var war in sim.Politics.Rebellions)
                {
                    var seat = sim.Settlements.All[war.Seat];
                    if (seat.Alive)
                        PlaceLabel(ref used, cam, new Vector3(seat.X + 0.5f, seat.Y + 10f, 0f),
                            war.Usurper ? $"{war.Leader} tranh ngôi" : $"Khởi nghĩa · {war.Leader}", new Color(1f, 0.5f, 0.4f), 19);
                }
                // Villages praying to Thiên Đạo, and how long they will wait.
                foreach (var p in sim.Faith.Open)
                {
                    var s = sim.Settlements.All[p.Settlement];
                    PlaceLabel(ref used, cam, new Vector3(s.X + 0.5f, s.Y + 7.5f, 0f),
                        $"{FaithSystem.KindShort[(int)p.Kind]} · còn {(p.Until - sim.Clock.Tick) / SimClock.DaysPerMonth} tháng", new Color(1f, 0.85f, 0.45f), 18);
                }
                foreach (var l in sim.Disasters.Landmarks)
                    if (l.Alive)
                        PlaceLabel(ref used, cam, new Vector3(l.X + 0.5f, l.Y + l.R + 1.5f, 0f), l.Name,
                            l.Kind == Landmark.Thunder ? new Color(0.8f, 0.68f, 1f) : new Color(1f, 0.6f, 0.4f), 18);
                // Known bí cảnh, and the Yêu Vương with the name of their yêu tộc.
                foreach (var r in sim.Relics.All)
                    if (r.Discovered && r.Open)
                        PlaceLabel(ref used, cam, new Vector3(r.X + 0.5f, r.Y + 3f, 0f), sim.Relics.Contested(r) ? $"[{r.Name}] · đang tranh đoạt" : $"[{r.Name}]",
                            sim.Relics.Contested(r) ? new Color(1f, 0.55f, 0.45f) : new Color(0.55f, 0.95f, 0.85f), 17);
                var ent = sim.Entities;
                foreach (var b in sim.Beasts.All)
                    if (b.Alive && (b.IsKing || b.Grade >= 5))
                        PlaceLabel(ref used, cam, new Vector3(ent.X[b.Entity], ent.Y[b.Entity] + 2.4f, 0f),
                            b.IsKing ? $"{b.ClanName} · {b.Name}" : $"{b.Name} · {b.GradeText}", new Color(1f, 0.5f, 0.45f), 17);
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
                else if (tool == BrushTool.Annihilate)
                    hint = h.Kind == InspectKind.Settlement && h.Settlement.Sect ? $"Diệt môn → {h.Settlement.Name}" : "Chọn một tông môn để diệt";
                else if (tool == BrushTool.GrantTreasure || tool == BrushTool.HeartDemon || tool == BrushTool.Cripple || tool == BrushTool.GrantTechnique)
                    hint = h.Kind == InspectKind.Cultivator
                        ? $"{WorldBrush.ToolNames[(int)tool]} → {who} ({h.Cultivator.RealmText}{(tool == BrushTool.HeartDemon ? $", đạo tâm {h.Cultivator.DaoHeart * 100f:0}%" : "")})"
                        : $"Chọn một tu sĩ để {WorldBrush.ToolNames[(int)tool].ToLower()}";
                else if (tool == BrushTool.Tribulation)
                    hint = h.Kind == InspectKind.Cultivator
                        ? $"Thiên kiếp → {who} ({h.Cultivator.RealmText}{(Realms.IsPeak(h.Cultivator.Realm, h.Cultivator.Stage) ? ", đang ở bình cảnh" : "")})"
                        : "Chọn một tu sĩ để giáng thiên kiếp";
                else
                    hint = h.Kind == InspectKind.Cultivator ? $"Thiên phạt → {who}" : h.Kind == InspectKind.Settlement ? $"Thiên lôi → {h.Settlement.Name}" :
                           h.Kind == InspectKind.Beast && sim.Beasts.ForEntity(h.Entity) is Beast hb ? $"Thiên phạt → {hb.Title} ({hb.GradeText})" : "Thiên lôi đánh xuống đây";
            }
            else if (!PointerOverUI && WorldBrush.IsCalamityTool(_game.Brush.Tool))
            {
                var tool = _game.Brush.Tool;
                hint = tool == BrushTool.Plague
                    ? h.Kind == InspectKind.Settlement ? $"Ôn dịch → {h.Settlement.Name}" : "Ôn dịch → làng gần nhất"
                    : tool == BrushTool.GreatCalamity
                        ? sim.Disasters.GreatCalamityActive ? "Đại kiếp đang diễn ra" : "Đại kiếp → toàn thế giới"
                        : $"{WorldBrush.ToolNames[(int)tool]} · bán kính {DisasterSystem.Radius(WorldBrush.CalamityFor(tool), _game.Brush.Size)} ô";
            }
            else if (!PointerOverUI && WorldBrush.HasLevel(_game.Brush.Tool))
                hint = $"{WorldBrush.ToolNames[(int)_game.Brush.Tool]} · {WorldBrush.LevelName(_game.Brush.Tool, _game.Brush.Level)}  ( [ ] đổi )";
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

        // "MA ĐẠO" → "M A   Đ Ạ O": wide map lettering.
        static string Spaced(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name.ToUpperInvariant())
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ch == ' ' ? ' ' : ch);
            }
            return sb.ToString();
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
            foreach (var (text, _, _) in _tickerLines) Destroy(text.transform.parent.gameObject);
            _tickerLines.Clear();
            if (_seedField != null) _seedField.text = _game.SeedText;
        }
    }
}
