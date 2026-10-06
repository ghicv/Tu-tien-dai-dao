using System.Collections.Generic;
using System.Text;
using ThienDao.Core;
using ThienDao.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace ThienDao.UI
{
    // Biên niên sử: a large scrollable window over the HistoryLog — the chronicle by century, the stories
    // StoryDetector found, and the legends history remembers.
    public sealed class ChronicleWindow
    {
        enum Tab { Chronicle, Stories, Legends }

        const int MaxChars = 14000; // one uGUI Text holds about 16k characters
        static readonly string[] TabNames = { "Biên niên", "Truyền kỳ", "Danh nhân" };

        readonly WorldBootstrap _game;
        readonly RectTransform _root;
        readonly Text _body;
        readonly ScrollRect _scroll;
        readonly Ui.IconButton[] _tabs = new Ui.IconButton[3];
        Tab _tab;
        float _refresh;
        bool _resetScroll;

        readonly List<HistoryRecord> _records = new List<HistoryRecord>();
        readonly List<Cultivator> _legends = new List<Cultivator>();

        public bool Open => _root.gameObject.activeSelf;

        public ChronicleWindow(RectTransform parent, WorldBootstrap game)
        {
            _game = game;
            var panel = Ui.Panel(parent, "Chronicle");
            _root = panel.rectTransform;
            Ui.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1040f, 780f));

            var title = Ui.Label(_root, "BIÊN NIÊN SỬ", 28, TextAnchor.MiddleLeft, Ui.Gold);
            Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(300f, 40f));
            for (int k = 0; k < 3; k++)
            {
                var tab = (Tab)k;
                _tabs[k] = Ui.Button(_root, null, TabNames[k], () => Show(tab), 40f, TabNames[k]);
                Ui.Place(_tabs[k].Frame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(330f + k * 170f, -12f), new Vector2(160f, 44f));
            }
            var close = Ui.Button(_root, Icons.Close, "Đóng", () => _root.gameObject.SetActive(false), 34f);
            Ui.Place(close.Frame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(34f, 34f));

            var viewport = Ui.Node("Viewport", _root);
            Ui.Stretch(viewport, 22, 22, 70, 22);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A solid page behind the text: long chronicles must not compete with the map underneath.
            var page = viewport.gameObject.AddComponent<Image>();
            page.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);
            var content = Ui.Node("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var col = content.gameObject.AddComponent<VerticalLayoutGroup>();
            col.childControlWidth = col.childControlHeight = true;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = false;
            col.padding = new RectOffset(4, 12, 4, 12);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _body = Ui.Label(content, "", 19);
            Object.Destroy(_body.GetComponent<Shadow>()); // a shadow doubles the vertices of a very long text
            _body.supportRichText = true;

            _scroll = _root.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport;
            _scroll.content = content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = false;
            _scroll.scrollSensitivity = 45f;

            _root.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            _root.gameObject.SetActive(!Open);
            _root.SetAsLastSibling();
            if (Open) Show(_tab);
        }

        public void Close() => _root.gameObject.SetActive(false);

        void Show(Tab tab)
        {
            _tab = tab;
            _refresh = 0f;
            _resetScroll = true;
            for (int k = 0; k < _tabs.Length; k++) _tabs[k].SetSelected(k == (int)tab);
        }

        public void Tick(float dt)
        {
            if (!Open || _game.Sim == null) return;
            _refresh -= dt;
            if (_refresh > 0f) return;
            _refresh = 1f;
            string text = _tab == Tab.Chronicle ? ChronicleText() : _tab == Tab.Stories ? StoriesText() : LegendsText();
            if (text.Length > MaxChars) text = text.Substring(0, MaxChars) + "\n<color=#8890a8>…</color>";
            _body.text = text;
            if (_resetScroll)
            {
                _resetScroll = false;
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        // ---------------------------------------------------------------- content

        static string Color(EventKind k)
        {
            switch (k)
            {
                case EventKind.Legend: return "#ffd873";
                case EventKind.Death: return "#ff9090";
                case EventKind.War: case EventKind.Battle: case EventKind.Destruction: return "#ff8a6a";
                case EventKind.Founding: case EventKind.Schism: return "#a8d8ff";
                case EventKind.Breakthrough: case EventKind.Tribulation: return "#c8f0a0";
                case EventKind.Divine: return "#fff2a8";
                default: return "#e8e8e8";
            }
        }

        string ChronicleText()
        {
            var sim = _game.Sim;
            var h = sim.History;
            var sb = new StringBuilder();
            int now = HistoryLog.CenturyOf(sim.Clock.Tick);
            sb.Append($"<color=#8890a8>Năm {sim.Clock.Year} · {h.All.Count:N0} sự kiện được ghi vào sử sách · {sim.Stories.All.Count} truyền kỳ</color>\n\n");
            for (int c = now; c >= 0 && sb.Length < MaxChars; c--)
            {
                int from = c * HistoryLog.YearsPerCentury + 1, to = Mathf.Min((c + 1) * HistoryLog.YearsPerCentury, sim.Clock.Year);
                sb.Append($"<size=24><color=#ffd873>Thế kỷ {c + 1}</color></size>  <color=#8890a8>năm {from}–{to}</color>\n");
                sb.Append($"<color=#b8bccc>Lập tông {h.CountIn(c, EventKind.Founding)} · ly khai {h.CountIn(c, EventKind.Schism)} · " +
                          $"tuyên chiến {CountWars(c)} · đại chiến {h.CountIn(c, EventKind.Battle)} · diệt môn {h.CountIn(c, EventKind.Destruction)} · " +
                          $"đấu pháp {h.CountIn(c, EventKind.Duel)} · đột phá {h.CountIn(c, EventKind.Breakthrough)} · thiên kiếp {h.CountIn(c, EventKind.Tribulation)}</color>\n");
                h.InCentury(c, 3, _records);
                int shown = 0;
                foreach (var r in _records)
                {
                    if (shown++ >= 16) break;
                    sb.Append($"  <color=#8890a8>Năm {r.Year}</color>  <color={Color(r.Kind)}>{r.Text}</color>\n");
                }
                if (_records.Count > 16) sb.Append($"  <color=#8890a8>… và {_records.Count - 16} đại sự khác</color>\n");
                if (_records.Count == 0) sb.Append("  <color=#8890a8>Thiên hạ thái bình, không có đại sự.</color>\n");
                sb.Append('\n');
            }
            return sb.ToString();
        }

        // War declarations only (the War kind also logs border quarrels at importance 1).
        int CountWars(int century)
        {
            _records.Clear();
            _game.Sim.History.InCentury(century, 3, _records);
            int n = 0;
            foreach (var r in _records)
                if (r.Kind == EventKind.War) n++;
            return n;
        }

        string StoriesText()
        {
            var stories = _game.Sim.Stories.All;
            var sb = new StringBuilder();
            if (stories.Count == 0) return "<color=#8890a8>Chưa có truyền kỳ nào. Hãy để thời gian trôi.</color>";
            sb.Append($"<color=#8890a8>{stories.Count} truyền kỳ, mới nhất trước</color>\n\n");
            for (int k = stories.Count - 1; k >= 0 && sb.Length < MaxChars; k--)
            {
                var s = stories[k];
                sb.Append($"<color=#ffd873>【{s.Title}】</color> <color=#8890a8>năm {s.Year}</color>\n{s.Text}\n\n");
            }
            return sb.ToString();
        }

        string LegendsText()
        {
            var sim = _game.Sim;
            sim.Stories.Legends(_legends);
            if (_legends.Count == 0) return "<color=#8890a8>Chưa ai lưu danh sử sách.</color>";
            var sb = new StringBuilder();
            sb.Append($"<color=#8890a8>{_legends.Count} nhân vật lưu danh sử sách</color>\n\n");
            foreach (var c in _legends)
            {
                if (sb.Length >= MaxChars) break;
                string fate = c.Alive ? $"<color=#9fe0a0>tại thế</color>, {c.AgeYears(sim.Clock.Tick):0} tuổi"
                    : $"<color=#ff9090>vẫn lạc năm {c.DeathTick / SimClock.DaysPerYear + 1}</color>" +
                      (c.KilledBy >= 0 ? $" dưới tay {sim.Cultivation.All[c.KilledBy].Name}" : "");
                sb.Append($"<color=#ffd873>{c.Epithet}</color> — {c.Name} · {c.RealmText}{(c.Demonic ? " · <color=#ff7070>ma tu</color>" : "")} · " +
                          $"{sim.Cultivation.SectName(c)} · {fate}\n");
                var master = sim.Cultivation.MasterOfDisciple(c);
                sb.Append($"<color=#b8bccc>   {SpiritRoots.Kind(c.OriginRoots)} · {c.Kills} mạng · danh vọng {c.Fame:0}" +
                          (master != null ? $" · sư phụ {master.Name}" : "") + "</color>\n");
                sim.History.OfCultivator(c.Index, _records, 40);
                int shown = 0;
                for (int k = _records.Count - 1; k >= 0 && shown < 3; k--) // the greatest deeds, oldest first
                {
                    if (_records[k].Importance < 3 || _records[k].Kind == EventKind.Legend) continue;
                    sb.Append($"   <color=#8890a8>Năm {_records[k].Year}</color> {_records[k].Text}\n");
                    shown++;
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
