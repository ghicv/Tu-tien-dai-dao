using System.Collections.Generic;
using ThienDao.Sim;
using ThienDao.World;
using UnityEngine;
using UnityEngine.Rendering;
using Unit = ThienDao.Render.SpriteLibrary.Unit;

namespace ThienDao.Render
{
    // Draws every visible moving thing as one dynamic quad mesh (a single draw call).
    // Villagers strolling between houses and fields are presentation only: they are not simulation state.
    public sealed class UnitRenderer : MonoBehaviour
    {
        const int MaxQuads = 30000;
        const float HideBelowPixelsPerCell = 1.5f;
        const float VillagersFromPixelsPerCell = 3f;

        struct Villager
        {
            public float X, Y, TX, TY, Wait;
            public Unit Look;
        }

        Simulation _sim;
        Mesh _mesh;
        MeshRenderer _renderer;
        Material _material;
        int[] _triangles;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<Color32> _colors = new List<Color32>();
        readonly Dictionary<int, List<Villager>> _villagers = new Dictionary<int, List<Villager>>();
        readonly List<int> _staleVillages = new List<int>();
        System.Random _rand = new System.Random(1);

        public int QuadsLastFrame { get; private set; }

        public void Init(Simulation sim)
        {
            _sim = sim;
            _villagers.Clear();
            _rand = new System.Random((int)sim.World.Seed);
            if (_mesh != null) return;

            _mesh = new Mesh { name = "Units", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            _triangles = new int[MaxQuads * 6];
            for (int q = 0; q < MaxQuads; q++)
            {
                int v = q * 4, t = q * 6;
                _triangles[t] = v;
                _triangles[t + 1] = v + 1;
                _triangles[t + 2] = v + 2;
                _triangles[t + 3] = v;
                _triangles[t + 4] = v + 2;
                _triangles[t + 5] = v + 3;
            }
            var go = new GameObject("Units");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { mainTexture = SpriteLibrary.UnitAtlas };
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = 5;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        public void Tick(Camera cam, float pixelsPerCell)
        {
            if (_sim == null) return;
            _verts.Clear();
            _uvs.Clear();
            _colors.Clear();

            if (pixelsPerCell >= HideBelowPixelsPerCell)
            {
                Vector3 bl = cam.ViewportToWorldPoint(Vector3.zero), tr = cam.ViewportToWorldPoint(Vector3.one);
                var view = Rect.MinMaxRect(bl.x - 2f, bl.y - 2f, tr.x + 2f, tr.y + 2f);
                DrawCreatures(view);
                if (pixelsPerCell >= VillagersFromPixelsPerCell) DrawVillagers(view);
                else _villagers.Clear();
            }

            _mesh.Clear();
            QuadsLastFrame = _verts.Count / 4;
            if (QuadsLastFrame == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0, QuadsLastFrame * 6, 0, false);
            _mesh.bounds = new Bounds(new Vector3(512f, 512f, 0f), new Vector3(4096f, 4096f, 10f));
        }

        static Unit UnitFor(Species s)
        {
            switch (s)
            {
                case Species.Deer: return Unit.Deer;
                case Species.Rabbit: return Unit.Rabbit;
                case Species.Wolf: return Unit.Wolf;
                default: return Unit.Migrants;
            }
        }

        void DrawCreatures(Rect view)
        {
            var e = _sim.Entities;
            float frac = _sim.TickFraction;
            float time = Time.time;
            for (int id = 0; id < e.Count; id++)
            {
                var s = e.Species[id];
                if (s == Species.None) continue;
                float x = Mathf.Lerp(e.PrevX[id], e.X[id], frac);
                float y = Mathf.Lerp(e.PrevY[id], e.Y[id], frac);
                if (!view.Contains(new Vector2(x, y))) continue;
                bool moving = e.X[id] != e.PrevX[id] || e.Y[id] != e.PrevY[id];
                bool left = e.TX[id] < e.X[id] - 0.01f;
                int frame = moving && !_sim.Paused ? ((int)(time * 6f) + id) & 1 : 0;
                AddQuad(UnitFor(s), frame, x, y, left);
            }
        }

        void DrawVillagers(Rect view)
        {
            var settlements = _sim.Settlements.All;
            var objects = _sim.World.Objects;
            float dt = _sim.Paused ? 0f : Time.deltaTime;
            float time = Time.time;

            _staleVillages.Clear();
            foreach (var key in _villagers.Keys) _staleVillages.Add(key);

            foreach (var s in settlements)
            {
                if (!s.Alive || !view.Overlaps(new Rect(s.X - 30f, s.Y - 30f, 60f, 60f))) continue;
                _staleVillages.Remove(s.Id);
                if (!_villagers.TryGetValue(s.Id, out var list))
                {
                    list = new List<Villager>();
                    _villagers[s.Id] = list;
                }
                int want = Mathf.Clamp(s.Population / 5, 1, 20);
                while (list.Count > want) list.RemoveAt(list.Count - 1);
                while (list.Count < want)
                {
                    var p = RandomSpot(s, objects);
                    list.Add(new Villager { X = p.x, Y = p.y, TX = p.x, TY = p.y, Wait = (float)_rand.NextDouble() * 2f, Look = (Unit)((int)Unit.Villager0 + _rand.Next(4)) });
                }

                for (int k = 0; k < list.Count; k++)
                {
                    var v = list[k];
                    float dx = v.TX - v.X, dy = v.TY - v.Y, d = Mathf.Sqrt(dx * dx + dy * dy);
                    bool moving = false;
                    if (v.Wait > 0f) v.Wait -= dt;
                    else if (d < 0.05f)
                    {
                        var p = RandomSpot(s, objects);
                        v.TX = p.x;
                        v.TY = p.y;
                        v.Wait = 0.5f + (float)_rand.NextDouble() * 2.5f;
                    }
                    else
                    {
                        float step = Mathf.Min(d, 1.6f * dt);
                        v.X += dx / d * step;
                        v.Y += dy / d * step;
                        moving = dt > 0f;
                    }
                    list[k] = v;
                    if (!view.Contains(new Vector2(v.X, v.Y))) continue;
                    int frame = moving ? ((int)(time * 7f) + k) & 1 : 0;
                    AddQuad(v.Look, frame, v.X, v.Y, dx < 0f);
                }
            }
            foreach (int id in _staleVillages) _villagers.Remove(id);
        }

        // A house doorstep or a field cell of the settlement.
        Vector2 RandomSpot(Settlement s, WorldObjects objects)
        {
            if (s.Farms.Count > 0 && (_rand.NextDouble() < 0.6 || s.Houses.Count == 0))
            {
                int cell = s.Farms[_rand.Next(s.Farms.Count)];
                int w = _sim.World.W;
                return new Vector2(cell % w + (float)_rand.NextDouble(), cell / w + (float)_rand.NextDouble());
            }
            if (s.Houses.Count > 0)
            {
                var h = objects.Get(s.Houses[_rand.Next(s.Houses.Count)]);
                return new Vector2(h.X + 1.5f + (float)(_rand.NextDouble() - 0.5), h.Y - 0.1f);
            }
            return new Vector2(s.X + 0.5f, s.Y + 0.5f);
        }

        void AddQuad(Unit unit, int frame, float x, float y, bool flip)
        {
            if (_verts.Count >= MaxQuads * 4) return;
            var sp = SpriteLibrary.UnitSprite(unit, frame);
            var uv = SpriteLibrary.UnitUv(unit, frame);
            float w = sp.W / (float)WorldRenderer.CellPx, h = sp.H / (float)WorldRenderer.CellPx;
            float x0 = x - w * 0.5f, y0 = y - 0.15f;
            _verts.Add(new Vector3(x0, y0, 0f));
            _verts.Add(new Vector3(x0, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0, 0f));
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
            _uvs.Add(new Vector2(u0, uv.yMin));
            _uvs.Add(new Vector2(u0, uv.yMax));
            _uvs.Add(new Vector2(u1, uv.yMax));
            _uvs.Add(new Vector2(u1, uv.yMin));
            var white = new Color32(255, 255, 255, 255);
            _colors.Add(white);
            _colors.Add(white);
            _colors.Add(white);
            _colors.Add(white);
        }
    }
}
