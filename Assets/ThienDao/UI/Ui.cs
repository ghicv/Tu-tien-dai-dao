using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThienDao.UI
{
    // Small helpers for building a pixel-styled uGUI hierarchy in code.
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.93f, 0.91f, 0.84f);
        public static readonly Color Dim = new Color(0.68f, 0.68f, 0.72f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.45f);

        static Font _font;
        public static Font Font => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        static Sprite _panel, _button, _buttonSelected, _white, _bar;
        public static Sprite PanelSprite => _panel != null ? _panel : (_panel = NineSlice(new Color32(34, 38, 54, 236), new Color32(14, 14, 22, 255), new Color32(78, 84, 110, 255)));
        public static Sprite ButtonSprite => _button != null ? _button : (_button = NineSlice(new Color32(64, 70, 94, 255), new Color32(18, 18, 26, 255), new Color32(110, 118, 150, 255)));
        public static Sprite SelectedSprite => _buttonSelected != null ? _buttonSelected : (_buttonSelected = NineSlice(new Color32(112, 92, 48, 255), new Color32(255, 214, 110, 255), new Color32(170, 140, 70, 255)));
        public static Sprite BarSprite => _bar != null ? _bar : (_bar = NineSlice(new Color32(20, 22, 30, 255), new Color32(10, 10, 14, 255), new Color32(20, 22, 30, 255)));

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                var tex = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
                tex.Apply();
                return _white = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        // 12×12 frame: dark outline, light bevel on top/left, flat fill; 3 px border for 9-slicing.
        static Sprite NineSlice(Color32 fill, Color32 border, Color32 light)
        {
            const int s = 12;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool edge = x == 0 || y == 0 || x == s - 1 || y == s - 1;
                bool bevel = !edge && (y == s - 2 || x == 1);
                bool shade = !edge && (y == 1 || x == s - 2);
                var c = edge ? border : bevel ? light : shade ? Shade(fill, 0.75f) : fill;
                bool corner = (x == 0 || x == s - 1) && (y == 0 || y == s - 1);
                if (corner) c = new Color32(0, 0, 0, 0);
                px[y * s + x] = c;
            }
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        }

        static Color32 Shade(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image Panel(Transform parent, string name, Sprite sprite = null)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? sprite : PanelSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f / 3f; // 3 px border drawn 9 px wide: chunky pixel frame
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null)
        {
            var rt = Node("Label", parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Ink;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rt.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public sealed class IconButton
        {
            public Button Button;
            public Image Frame;
            public Image Icon;
            public Text Caption;

            public void SetSelected(bool selected) => Frame.sprite = selected ? SelectedSprite : ButtonSprite;
        }

        public static IconButton Button(Transform parent, Sprite icon, string tooltip, Action onClick, float size = 56f, string caption = null)
        {
            var frame = Panel(parent, "Button", ButtonSprite);
            var rt = frame.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            if (!string.IsNullOrEmpty(tooltip)) frame.gameObject.AddComponent<Tooltip>().Text = tooltip;

            var result = new IconButton { Button = button, Frame = frame };
            if (icon != null)
            {
                var iconRt = Node("Icon", rt);
                Stretch(iconRt, 8, 8, 8, 8);
                var img = iconRt.gameObject.AddComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.raycastTarget = false;
                result.Icon = img;
            }
            if (caption != null)
            {
                var t = Label(rt, caption, 18, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, 4, 4, 2, 2);
                result.Caption = t;
            }
            return result;
        }

        public static Image Bar(Transform parent, Color fill, out Image fillImage)
        {
            var bg = Panel(parent, "Bar", BarSprite);
            var f = Node("Fill", bg.rectTransform);
            Stretch(f, 3, 3, 3, 3);
            fillImage = f.gameObject.AddComponent<Image>();
            fillImage.sprite = White;
            fillImage.color = fill;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.raycastTarget = false;
            return bg;
        }
    }

    // Hover text for a UI element; GameUI shows the current one near the pointer.
    public sealed class Tooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Text;
        public static string Current;

        public void OnPointerEnter(PointerEventData eventData) => Current = Text;

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Current == Text) Current = null;
        }

        void OnDisable()
        {
            if (Current == Text) Current = null;
        }
    }
}
