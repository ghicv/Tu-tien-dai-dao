using UnityEngine;

namespace ThienDao.Render
{
    // Rectangle outlines in world space whose line width stays a fixed number of screen pixels at any zoom:
    // one for whatever the pointer is over, one for the current selection.
    public sealed class HighlightRenderer : MonoBehaviour
    {
        sealed class Frame
        {
            public SpriteRenderer[] Lines; // 4 dark shadow lines, then 4 bright lines
        }

        static Sprite _pixel;
        Frame _hover, _selection;

        public void Init()
        {
            if (_hover != null) return;
            if (_pixel == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            _hover = MakeFrame("Hover", material, 21);
            _selection = MakeFrame("Selection", material, 23);
        }

        Frame MakeFrame(string name, Material material, int order)
        {
            var root = new GameObject(name);
            root.transform.SetParent(transform, false);
            var f = new Frame { Lines = new SpriteRenderer[8] };
            for (int k = 0; k < 8; k++)
            {
                var go = new GameObject(k < 4 ? "Shadow" : "Line");
                go.transform.SetParent(root.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = material;
                sr.sprite = _pixel;
                sr.sortingOrder = order + (k < 4 ? 0 : 1);
                sr.enabled = false;
                f.Lines[k] = sr;
            }
            return f;
        }

        public void Hover(Rect? box, Color color, float pixelsPerCell) => Show(_hover, box, color, pixelsPerCell, 2f);

        public void Selection(Rect? box, Color color, float pixelsPerCell)
        {
            // A slow pulse so the selection reads apart from the hover outline.
            color.a *= 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            Show(_selection, box, color, pixelsPerCell, 3f);
        }

        static void Show(Frame f, Rect? box, Color color, float pixelsPerCell, float widthPx)
        {
            if (f == null) return;
            bool on = box.HasValue;
            for (int k = 0; k < 8; k++)
                if (f.Lines[k].enabled != on) f.Lines[k].enabled = on;
            if (!on) return;

            float px = 1f / Mathf.Max(0.01f, pixelsPerCell);
            var r = box.Value;
            // Keep a small gap between the outline and what it frames.
            r = Rect.MinMaxRect(r.xMin - 2f * px, r.yMin - 2f * px, r.xMax + 2f * px, r.yMax + 2f * px);
            var shadow = new Color(0f, 0f, 0f, 0.55f * color.a);
            Place(f.Lines, 0, r, (widthPx + 2f) * px, shadow);
            Place(f.Lines, 4, r, widthPx * px, color);
        }

        static void Place(SpriteRenderer[] lines, int start, Rect r, float w, Color color)
        {
            Set(lines[start], new Vector2(r.center.x, r.yMax), new Vector2(r.width + w, w), color);
            Set(lines[start + 1], new Vector2(r.center.x, r.yMin), new Vector2(r.width + w, w), color);
            Set(lines[start + 2], new Vector2(r.xMin, r.center.y), new Vector2(w, r.height + w), color);
            Set(lines[start + 3], new Vector2(r.xMax, r.center.y), new Vector2(w, r.height + w), color);
        }

        static void Set(SpriteRenderer sr, Vector2 pos, Vector2 size, Color color)
        {
            sr.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.color = color;
        }
    }
}
