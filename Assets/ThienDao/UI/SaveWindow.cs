using System;
using UnityEngine;
using UnityEngine.UI;

namespace ThienDao.UI
{
    // Lưu / tải: one row per slot (3 manual, quick save F5, autosave) with what it holds and Lưu / Tải buttons.
    public sealed class SaveWindow
    {
        readonly WorldBootstrap _game;
        readonly GameUI _ui;
        readonly RectTransform _root;
        readonly Text[] _info;

        public bool Open => _root.gameObject.activeSelf;

        public SaveWindow(RectTransform parent, WorldBootstrap game, GameUI ui)
        {
            _game = game;
            _ui = ui;
            var slots = WorldBootstrap.Slots;
            _info = new Text[slots.Length];

            var panel = Ui.Panel(parent, "SaveWindow");
            _root = panel.rectTransform;
            float height = 90f + slots.Length * 70f;
            Ui.Place(_root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(760f, height));

            var title = Ui.Label(_root, "LƯU / TẢI THẾ GIỚI", 26, TextAnchor.MiddleLeft, Ui.Gold);
            Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -14f), new Vector2(500f, 40f));
            var close = Ui.Button(_root, Icons.Close, "Đóng", Close, 34f);
            Ui.Place(close.Frame.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(34f, 34f));

            for (int k = 0; k < slots.Length; k++)
            {
                string slot = slots[k];
                float y = -70f - k * 70f;
                var row = Ui.Panel(_root, "Slot");
                Ui.Place(row.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(720f, 62f));
                _info[k] = Ui.Label(row.rectTransform, "", 18, TextAnchor.MiddleLeft);
                _info[k].supportRichText = true;
                Ui.Stretch(_info[k].rectTransform, 14, 220, 4, 4);
                bool canSave = slot != WorldBootstrap.AutoSlot;
                if (canSave)
                {
                    var save = Ui.Button(row.rectTransform, null, $"Lưu thế giới hiện tại vào {WorldBootstrap.SlotName(slot)}", () => DoSave(slot), 46f, "Lưu");
                    Ui.Place(save.Frame.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-114f, 0f), new Vector2(96f, 46f));
                }
                var load = Ui.Button(row.rectTransform, null, $"Tải {WorldBootstrap.SlotName(slot)} (thế giới hiện tại chưa lưu sẽ mất)", () => DoLoad(slot), 46f, "Tải");
                Ui.Place(load.Frame.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(96f, 46f));
            }
            _root.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            _root.gameObject.SetActive(!Open);
            if (!Open) return;
            _root.SetAsLastSibling();
            Refresh();
        }

        public void Close() => _root.gameObject.SetActive(false);

        void Refresh()
        {
            var slots = WorldBootstrap.Slots;
            for (int k = 0; k < slots.Length; k++)
            {
                var h = WorldBootstrap.SlotHeader(slots[k]);
                string name = $"<color=#ffd873>{WorldBootstrap.SlotName(slots[k])}</color>";
                _info[k].text = h == null
                    ? $"{name}\n<color=#8890a8>trống</color>"
                    : $"{name} · thế giới \"{h.Seed}\" · năm {h.Year}\n<color=#8890a8>lưu lúc {new DateTime(h.SavedAtUtcTicks, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy HH:mm}</color>";
            }
        }

        void DoSave(string slot)
        {
            _game.SaveTo(slot, out var msg);
            _ui.ShowToast(msg);
            Refresh();
        }

        void DoLoad(string slot)
        {
            bool ok = _game.LoadFrom(slot, out var msg);
            _ui.ShowToast(msg);
            if (ok) Close();
        }
    }
}
