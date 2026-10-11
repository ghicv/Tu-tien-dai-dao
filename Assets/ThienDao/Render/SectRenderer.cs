using System.Collections.Generic;
using ThienDao.Core;
using ThienDao.Sim;
using UnityEngine;

namespace ThienDao.Render
{
    // Sơn môn on the map (devlog 38): each sect's complex, and each sect's ruins, as one sprite laid over the ground,
    // under the people and the effects. The sprite is painted again only when what stands changes (a module built,
    // fallen to ruin, a rank won), which is once in some years.
    public sealed class SectRenderer : MonoBehaviour
    {
        const int SortingOrder = 2; // above the ground (0), below the people (5) and the effects (15)

        sealed class Entry
        {
            public GameObject Go;
            public SpriteRenderer R;
            public Texture2D Tex;
            public ulong Key;
            public bool Seen;
        }

        Simulation _sim;
        readonly Dictionary<long, Entry> _entries = new Dictionary<long, Entry>();

        public void Init(Simulation sim)
        {
            _sim = sim;
            foreach (var e in _entries.Values) Drop(e);
            _entries.Clear();
        }

        void OnDestroy()
        {
            foreach (var e in _entries.Values) Drop(e);
        }

        static void Drop(Entry e)
        {
            if (e.Tex != null) Destroy(e.Tex);
            if (e.R != null && e.R.sprite != null) Destroy(e.R.sprite);
            if (e.Go != null) Destroy(e.Go);
        }

        public void Tick()
        {
            if (_sim == null) return;
            foreach (var e in _entries.Values) e.Seen = false;
            var factions = _sim.Factions;
            var settlements = _sim.Settlements.All;
            foreach (var f in factions.All)
            {
                if (!f.Alive || f.Built == 0) continue;
                var s = settlements[f.Id];
                Show(f.Id, s.X, s.Y, f.Built, f.Ruined, f.Tier, f.Demonic, (uint)f.Id);
            }
            // Ruins: everything stands broken.
            for (int k = 0; k < factions.Ruins.Count; k++)
            {
                var r = factions.Ruins[k];
                Show(1000000L + r.X * 2048L + r.Y, r.X, r.Y, r.Built, r.Built, r.Tier, r.Demonic, (uint)(r.X * 31 + r.Y));
            }
            var gone = new List<long>();
            foreach (var kv in _entries) if (!kv.Value.Seen) gone.Add(kv.Key);
            foreach (long k in gone) { Drop(_entries[k]); _entries.Remove(k); }
        }

        void Show(long id, int x, int y, uint built, uint ruined, byte tier, bool demonic, uint seed)
        {
            ulong key = built | ((ulong)ruined << 20) | ((ulong)tier << 40) | (demonic ? 1UL << 44 : 0);
            if (!_entries.TryGetValue(id, out var e))
            {
                var go = new GameObject("SonMon");
                go.transform.SetParent(transform, false);
                e = new Entry { Go = go, R = go.AddComponent<SpriteRenderer>(), Key = ulong.MaxValue };
                e.R.sortingOrder = SortingOrder;
                _entries[id] = e;
            }
            e.Seen = true;
            e.Go.transform.position = new Vector3(x + FactionSystem.LayoutX0, y + FactionSystem.LayoutY0, 0f);
            if (e.Key == key) return;
            e.Key = key;
            int w = SectArt.Width, h = SectArt.Height;
            if (e.Tex == null)
                e.Tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "SonMon", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            e.Tex.SetPixelData(SectArt.Compose(built, ruined, tier, demonic, seed), 0);
            e.Tex.Apply(false);
            if (e.R.sprite == null)
                e.R.sprite = Sprite.Create(e.Tex, new Rect(0, 0, w, h), Vector2.zero, SectArt.Px);
        }
    }
}
