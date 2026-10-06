using System.Collections.Generic;
using ThienDao.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThienDao.Render
{
    // Plays visual effects for simulation events (Thiên phạt, thiên kiếp, đột phá, tẩu hỏa nhập ma, chết đuối…).
    // Runs on real time so effects play even while the simulation is paused or fast-forwarding.
    public sealed class FxRenderer : MonoBehaviour
    {
        const int MaxQuads = 4000;
        const int MaxEffects = 48;
        const float CellPx = WorldRenderer.CellPx;

        enum Sprite { BoltA, BoltB, Flash, Ring, Spark, Smoke, Fire0, Fire1, Fire2, Cloud, Beam, Drop, Count }

        struct Particle
        {
            public Sprite Sprite;
            public float X, Y, VX, VY, Gravity, Age, Life, Size, Grow;
            public Color32 Color;
        }

        struct Strike
        {
            public float X, Y, At, Height;
            public Color32 Color;
        }

        sealed class Effect
        {
            public Fx Kind;
            public float X, Y, Start, Duration;
            public readonly List<Strike> Strikes = new List<Strike>();
        }

        Simulation _sim;
        long _seen;
        Mesh _mesh;
        Material _material;
        Rect[] _uv;
        Vector2[] _size; // sprite size in world units
        int[] _triangles;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Vector2> _uvs = new List<Vector2>();
        readonly List<Color32> _colors = new List<Color32>();
        readonly List<Effect> _effects = new List<Effect>();
        readonly List<Particle> _particles = new List<Particle>();
        System.Random _rand = new System.Random(7);

        public void Init(Simulation sim)
        {
            _sim = sim;
            _seen = sim.Events.TotalAdded;
            _effects.Clear();
            _particles.Clear();
            if (_mesh != null) return;

            BuildAtlas(out var atlas);
            _mesh = new Mesh { name = "Fx", indexFormat = IndexFormat.UInt32 };
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
            var go = new GameObject("Fx");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            _material = new Material(shader) { mainTexture = atlas };
            mr.sharedMaterial = _material;
            mr.sortingOrder = 15;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        float Rand(float a, float b) => a + (float)_rand.NextDouble() * (b - a);

        // ---------------------------------------------------------------- spawning

        void PickUpEvents(Rect view)
        {
            var events = _sim.Events.Recent;
            long fresh = _sim.Events.TotalAdded - _seen;
            _seen = _sim.Events.TotalAdded;
            int start = Mathf.Max(0, events.Count - (int)Mathf.Min(fresh, events.Count));
            for (int k = start; k < events.Count; k++)
            {
                var ev = events[k];
                if (ev.Fx == Fx.None || ev.X < 0f || _effects.Count >= MaxEffects) continue;
                if (!view.Contains(new Vector2(ev.X, ev.Y))) continue;
                Spawn(ev.Fx, ev.X, ev.Y);
            }
        }

        public void Spawn(Fx kind, float x, float y)
        {
            float now = Time.unscaledTime;
            var e = new Effect { Kind = kind, X = x, Y = y, Start = now };
            switch (kind)
            {
                case Fx.Lightning:
                    e.Duration = 2.5f;
                    e.Strikes.Add(new Strike { X = x, Y = y, At = now, Height = 22f, Color = new Color32(210, 230, 255, 255) });
                    e.Strikes.Add(new Strike { X = x + Rand(-0.4f, 0.4f), Y = y, At = now + 0.12f, Height = 22f, Color = new Color32(255, 255, 255, 255) });
                    break;
                case Fx.Tribulation:
                    e.Duration = 4f;
                    for (int k = 0; k < 7; k++)
                        e.Strikes.Add(new Strike { X = x + Rand(-2.5f, 2.5f), Y = y + Rand(-1.5f, 1.5f), At = now + 0.4f + k * Rand(0.25f, 0.5f), Height = 16f,
                            Color = k % 2 == 0 ? new Color32(190, 160, 255, 255) : new Color32(230, 240, 255, 255) });
                    break;
                case Fx.Explosion:
                case Fx.DemonBlast:
                    e.Duration = 1.2f;
                    Burst(x, y + 0.6f, kind == Fx.DemonBlast ? new Color32(190, 70, 255, 255) : new Color32(255, 170, 60, 255), 14, 5f);
                    break;
                case Fx.Splash:
                    e.Duration = 1.2f;
                    for (int k = 0; k < 14; k++)
                    {
                        float a = Rand(0.3f, Mathf.PI - 0.3f);
                        _particles.Add(new Particle { Sprite = Sprite.Drop, X = x + Rand(-0.6f, 0.6f), Y = y, VX = Mathf.Cos(a) * Rand(1f, 3f), VY = Mathf.Sin(a) * Rand(3f, 6f),
                            Gravity = 14f, Life = Rand(0.5f, 0.9f), Size = 1f, Color = new Color32(200, 235, 255, 230) });
                    }
                    break;
                case Fx.LightPillar:
                    e.Duration = 2f;
                    break;
                case Fx.Blessing:
                    e.Duration = 1.6f;
                    for (int k = 0; k < 16; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = x + Rand(-1.2f, 1.2f), Y = y + Rand(0f, 1.5f), VX = Rand(-0.3f, 0.3f), VY = Rand(1.5f, 3f),
                            Life = Rand(0.8f, 1.5f), Size = Rand(0.8f, 1.4f), Color = new Color32(255, 230, 120, 255) });
                    break;
            }
            _effects.Add(e);
        }

        void Burst(float x, float y, Color32 color, int count, float speed)
        {
            for (int k = 0; k < count; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f), v = Rand(speed * 0.4f, speed);
                _particles.Add(new Particle { Sprite = Sprite.Spark, X = x, Y = y, VX = Mathf.Cos(a) * v, VY = Mathf.Sin(a) * v + 2f, Gravity = 8f,
                    Life = Rand(0.4f, 0.8f), Size = Rand(0.8f, 1.5f), Color = color });
            }
            for (int k = 0; k < 4; k++)
                _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.5f, 0.5f), Y = y + Rand(0f, 0.5f), VX = Rand(-0.3f, 0.3f), VY = Rand(0.4f, 0.9f),
                    Life = Rand(1.2f, 2f), Size = Rand(0.8f, 1.2f), Grow = 0.8f, Color = new Color32(70, 66, 64, 170) });
        }

        // ---------------------------------------------------------------- per frame

        public void Tick(Camera cam)
        {
            if (_sim == null) return;
            Vector3 bl = cam.ViewportToWorldPoint(Vector3.zero), tr = cam.ViewportToWorldPoint(Vector3.one);
            var view = Rect.MinMaxRect(bl.x - 4f, bl.y - 4f, tr.x + 4f, tr.y + 24f);
            PickUpEvents(view);

            _verts.Clear();
            _uvs.Clear();
            _colors.Clear();
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;

            for (int i = _effects.Count - 1; i >= 0; i--)
            {
                var e = _effects[i];
                float t = now - e.Start;
                if (t > e.Duration) { _effects.RemoveAt(i); continue; }
                DrawEffect(e, t, now);
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Life) { _particles.RemoveAt(i); continue; }
                p.VY -= p.Gravity * dt;
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                _particles[i] = p;
                float k = 1f - p.Age / p.Life;
                var c = p.Color;
                c.a = (byte)(c.a * k);
                float s = p.Size * (1f + p.Grow * p.Age);
                Quad(p.Sprite, p.X, p.Y, s, s, c);
            }

            _mesh.Clear();
            int quads = _verts.Count / 4;
            if (quads == 0) return;
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0, quads * 6, 0, false);
            _mesh.bounds = new Bounds(new Vector3(512f, 512f, 0f), new Vector3(4096f, 4096f, 10f));
        }

        void DrawEffect(Effect e, float t, float now)
        {
            switch (e.Kind)
            {
                case Fx.Lightning:
                case Fx.Tribulation:
                    if (e.Kind == Fx.Tribulation)
                    {
                        float fade = Mathf.Clamp01(Mathf.Min(t / 0.4f, (e.Duration - t) / 0.6f));
                        Quad(Sprite.Cloud, e.X, e.Y + 11f, 2.2f, 2.2f, new Color32(40, 34, 60, (byte)(200 * fade)));
                        Quad(Sprite.Cloud, e.X - 2.5f, e.Y + 10.5f, 1.6f, 1.6f, new Color32(50, 44, 70, (byte)(170 * fade)));
                        Quad(Sprite.Cloud, e.X + 2.8f, e.Y + 10.8f, 1.7f, 1.7f, new Color32(46, 40, 66, (byte)(180 * fade)));
                    }
                    for (int k = 0; k < e.Strikes.Count; k++)
                    {
                        var s = e.Strikes[k];
                        float st = now - s.At;
                        if (st < 0f) continue;
                        if (st < 0.35f)
                        {
                            // Bolt flickers between two shapes, then the impact flash fades.
                            var bolt = ((int)(st / 0.05f) & 1) == 0 ? Sprite.BoltA : Sprite.BoltB;
                            var c = s.Color;
                            c.a = (byte)(255 * (1f - st / 0.35f));
                            QuadBottom(bolt, s.X, s.Y, s.Height / _size[(int)bolt].y, c);
                            Quad(Sprite.Flash, s.X, s.Y + 0.3f, 1.6f + st * 4f, 1.6f + st * 4f, new Color32(255, 255, 255, (byte)(220 * (1f - st / 0.35f))));
                        }
                        if (st < 0.6f) Quad(Sprite.Ring, s.X, s.Y + 0.2f, 0.5f + st * 5f, 0.3f + st * 2.5f, new Color32(255, 240, 200, (byte)(200 * (1f - st / 0.6f))));
                        if (st >= 0f && st < Time.unscaledDeltaTime + 0.001f) Burst(s.X, s.Y + 0.3f, new Color32(255, 220, 120, 255), 10, 6f);
                        // Scorched ground keeps burning a little after the bolt.
                        if (e.Kind == Fx.Lightning && st > 0.1f && st < 2.3f)
                        {
                            var flame = (Sprite)((int)Sprite.Fire0 + ((int)(now * 10f) + k) % 3);
                            Quad(flame, s.X + (k - 0.5f) * 0.5f, s.Y + 0.5f, 1f, 1f, new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01((2.3f - st) / 0.6f))));
                        }
                    }
                    break;

                case Fx.Explosion:
                case Fx.DemonBlast:
                {
                    var tint = e.Kind == Fx.DemonBlast ? new Color32(200, 90, 255, 255) : new Color32(255, 190, 90, 255);
                    float k = t / e.Duration;
                    tint.a = (byte)(230 * (1f - k));
                    Quad(Sprite.Flash, e.X, e.Y + 0.6f, 1.2f + k * 3f, 1.2f + k * 3f, tint);
                    Quad(Sprite.Ring, e.X, e.Y + 0.4f, 0.8f + k * 6f, 0.5f + k * 3f, tint);
                    break;
                }

                case Fx.Splash:
                {
                    float k = t / e.Duration;
                    Quad(Sprite.Ring, e.X, e.Y + 0.1f, 0.6f + k * 4f, 0.3f + k * 1.6f, new Color32(220, 245, 255, (byte)(220 * (1f - k))));
                    break;
                }

                case Fx.LightPillar:
                {
                    float fade = Mathf.Clamp01(Mathf.Min(t / 0.25f, (e.Duration - t) / 0.8f));
                    float width = 0.8f + Mathf.Sin(t * 12f) * 0.08f;
                    QuadBottom(Sprite.Beam, e.X, e.Y, 1f, new Color32(255, 236, 150, (byte)(220 * fade)), width);
                    Quad(Sprite.Flash, e.X, e.Y + 0.6f, 2.4f, 2.4f, new Color32(255, 240, 180, (byte)(160 * fade)));
                    if (_rand.NextDouble() < 0.4)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = e.X + Rand(-0.4f, 0.4f), Y = e.Y + Rand(0f, 1f), VY = Rand(3f, 6f),
                            Life = Rand(0.6f, 1f), Size = 1f, Color = new Color32(255, 240, 170, 255) });
                    break;
                }
            }
        }

        // Centred quad scaled from the sprite's native size.
        void Quad(Sprite s, float x, float y, float sx, float sy, Color32 color)
        {
            var size = _size[(int)s];
            AddQuad(s, x - size.x * sx * 0.5f, y - size.y * sy * 0.5f, size.x * sx, size.y * sy, color);
        }

        // Anchored at the bottom centre (bolts, beams strike down to the ground).
        void QuadBottom(Sprite s, float x, float y, float scale, Color32 color, float widthScale = 1f)
        {
            var size = _size[(int)s];
            float w = size.x * scale * widthScale, h = size.y * scale;
            AddQuad(s, x - w * 0.5f, y, w, h, color);
        }

        void AddQuad(Sprite s, float x0, float y0, float w, float h, Color32 color)
        {
            if (_verts.Count >= MaxQuads * 4) return;
            var uv = _uv[(int)s];
            _verts.Add(new Vector3(x0, y0, 0f));
            _verts.Add(new Vector3(x0, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0, 0f));
            _uvs.Add(new Vector2(uv.xMin, uv.yMin));
            _uvs.Add(new Vector2(uv.xMin, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMax));
            _uvs.Add(new Vector2(uv.xMax, uv.yMin));
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
            _colors.Add(color);
        }

        // ---------------------------------------------------------------- procedural FX atlas

        void BuildAtlas(out Texture2D atlas)
        {
            const int tw = 256, th = 128;
            var px = new Color32[tw * th];
            _uv = new Rect[(int)Sprite.Count];
            _size = new Vector2[(int)Sprite.Count];
            var rng = new System.Random(1234);
            int cx = 0, cy = 0, rowH = 0;

            void Put(Sprite s, int w, int h, System.Func<int, int, Color32> paint)
            {
                if (cx + w > tw) { cx = 0; cy += rowH + 1; rowH = 0; }
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[(cy + y) * tw + cx + x] = paint(x, y);
                _uv[(int)s] = new Rect((cx + 0.5f) / tw, (cy + 0.5f) / th, (w - 1f) / tw, (h - 1f) / th);
                _size[(int)s] = new Vector2(w / CellPx, h / CellPx);
                cx += w + 1;
                rowH = Mathf.Max(rowH, h);
            }

            Color32 White(float a) => new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));

            // Jagged bolt: random walk from sky to ground with a hot core and soft glow.
            Color32[] Bolt(int seed)
            {
                const int w = 16, h = 96;
                var b = new Color32[w * h];
                var r = new System.Random(seed);
                float x = 8f;
                for (int y = h - 1; y >= 0; y--)
                {
                    if (y % 3 == 0) x = Mathf.Clamp(x + r.Next(-2, 3), 3f, 12f);
                    int xi = (int)x;
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int xx = xi + dx;
                        if (xx < 0 || xx >= w) continue;
                        float a = dx == 0 ? 1f : Mathf.Abs(dx) == 1 ? 0.75f : 0.25f;
                        var c = White(a);
                        if (Mathf.Abs(dx) > 0) { c.r = 180; c.g = 210; }
                        if (b[y * w + xx].a < c.a) b[y * w + xx] = c;
                    }
                    if (y > 20 && r.NextDouble() < 0.04) // short branch
                    {
                        int dir = r.Next(2) == 0 ? -1 : 1;
                        for (int k = 1; k < 8 && y - k >= 0; k++)
                        {
                            int bx = xi + dir * k / 2;
                            if (bx >= 0 && bx < w) b[(y - k) * w + bx] = White(0.6f - k * 0.06f);
                        }
                    }
                }
                return b;
            }

            var boltA = Bolt(11);
            var boltB = Bolt(29);
            Put(Sprite.BoltA, 16, 96, (x, y) => boltA[y * 16 + x]);
            Put(Sprite.BoltB, 16, 96, (x, y) => boltB[y * 16 + x]);
            Put(Sprite.Beam, 8, 96, (x, y) =>
            {
                float edge = 1f - Mathf.Abs(x + 0.5f - 4f) / 4f;
                return White(edge * edge * (1f - y / 96f) * 1.2f);
            });
            Put(Sprite.Flash, 32, 32, (x, y) =>
            {
                float d = Mathf.Sqrt((x + 0.5f - 16f) * (x + 0.5f - 16f) + (y + 0.5f - 16f) * (y + 0.5f - 16f)) / 16f;
                return White(Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
            });
            Put(Sprite.Ring, 32, 32, (x, y) =>
            {
                float d = Mathf.Sqrt((x + 0.5f - 16f) * (x + 0.5f - 16f) + (y + 0.5f - 16f) * (y + 0.5f - 16f));
                return White(1f - Mathf.Abs(d - 13f) / 2.5f);
            });
            Put(Sprite.Cloud, 64, 24, (x, y) =>
            {
                float a = 0f;
                for (int k = 0; k < 5; k++)
                {
                    float bx = 10f + k * 11f, by = 12f + (k % 2) * 3f, r = 9f + (k % 3) * 2f;
                    float d = Mathf.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by)) / r;
                    a = Mathf.Max(a, 1f - d);
                }
                return White(Mathf.Clamp01(a * 2f));
            });
            Put(Sprite.Smoke, 16, 16, (x, y) =>
            {
                float d = Mathf.Sqrt((x + 0.5f - 8f) * (x + 0.5f - 8f) + (y + 0.5f - 8f) * (y + 0.5f - 8f)) / 8f;
                return White((1f - d) * (0.7f + 0.3f * (float)rng.NextDouble()));
            });
            Put(Sprite.Spark, 4, 4, (x, y) => White(x == 0 || x == 3 || y == 0 || y == 3 ? 0.4f : 1f));
            Put(Sprite.Drop, 3, 3, (x, y) => White(x == 1 || y == 1 ? 1f : 0.3f));
            for (int f = 0; f < 3; f++)
            {
                int frame = f;
                Put((Sprite)((int)Sprite.Fire0 + f), 8, 12, (x, y) =>
                {
                    float sway = Mathf.Sin((y + frame * 3) * 0.7f) * 0.8f;
                    float width = 3.6f * (1f - y / 12f) + 0.4f;
                    float d = Mathf.Abs(x + 0.5f - 4f - sway);
                    if (d > width) return default;
                    float heat = 1f - d / width;
                    byte g = (byte)(80 + 170 * heat * (1f - y / 14f));
                    return new Color32(255, g, (byte)(40 * heat), (byte)(255 * Mathf.Clamp01(1.2f - y / 12f)));
                });
            }

            atlas = new Texture2D(tw, th, TextureFormat.RGBA32, false) { name = "FxAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixelData(px, 0);
            atlas.Apply(false);
        }
    }
}
