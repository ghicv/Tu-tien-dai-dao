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

        enum Sprite { BoltA, BoltB, Flash, Ring, Spark, Smoke, Fire0, Fire1, Fire2, Cloud, Beam, Drop, QiShot, Count }

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
            FightScenes.Clear();
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
                if (ev.X < 0f || !view.Contains(new Vector2(ev.X, ev.Y))) continue;
                if (ev.Fx >= Fx.DuelKill)
                {
                    StartFight(ev, k);
                    continue;
                }
                // Anyone who dies without a fight on screen (old age, a bolt, a flood) goes up in white smoke.
                bool death = ev.Kind == EventKind.Death || (ev.Kind == EventKind.Beast && ev.Text.Contains("chết"));
                if (death) Poof(ev.X, ev.Y + 0.4f);
                if (ev.Fx == Fx.None) continue;
                Hurt(ev.Fx, ev.X, ev.Y);
                if (_effects.Count >= MaxEffects) continue;
                Spawn(ev.Fx, ev.X, ev.Y);
            }
        }

        // Effects that harm the living mark a zone where creatures blink white, and some of the stand-ins die.
        void Hurt(Fx fx, float x, float y)
        {
            float r, kill;
            switch (fx)
            {
                case Fx.Stampede: r = 7f; kill = 0.3f; break;
                case Fx.Quake: r = 10f; kill = 0.25f; break;
                case Fx.Eruption: r = 12f; kill = 0.45f; break;
                case Fx.Lightning: r = 3.5f; kill = 0.5f; break;
                case Fx.Tribulation: r = 7f; kill = 0.3f; break;
                case Fx.Storm: r = 8f; kill = 0.12f; break;
                case Fx.Miasma: r = 6f; kill = 0.2f; break;
                case Fx.Explosion:
                case Fx.DemonBlast: r = 2.5f; kill = 0.2f; break;
                case Fx.Splash: r = 2.5f; kill = 0.3f; break;
                default: return;
            }
            FightScenes.Hurt.Add(new HurtZone { X = x, Y = y, R = r, Start = Time.unscaledTime, Kill = kill });
        }

        // White smoke where something died; then it is gone.
        void Poof(float x, float y)
        {
            for (int s = 0; s < 5; s++)
                _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.5f, 0.5f), Y = y + Rand(-0.2f, 0.4f), VX = Rand(-0.5f, 0.5f), VY = Rand(0.5f, 1.2f),
                    Life = Rand(0.4f, 0.75f), Size = 1f, Grow = s == 0 ? 2f : 0f, Color = new Color32(245, 245, 245, 255) });
            _particles.Add(new Particle { Sprite = Sprite.Flash, X = x, Y = y, Life = 0.15f, Size = 1f, Color = new Color32(255, 255, 255, 255) });
        }

        // ---------------------------------------------------------------- fights played out

        static readonly Color32[] KiemKhi =
        {
            new Color32(200, 200, 200, 255), new Color32(150, 205, 255, 255), new Color32(120, 255, 170, 255),
            new Color32(255, 220, 110, 255), new Color32(210, 150, 255, 255), new Color32(240, 250, 255, 255)
        };
        static readonly Color32 DemonQi = new Color32(255, 70, 70, 255);
        static readonly Color32 Claw = new Color32(255, 110, 70, 255);

        Color32 QiOf(Cultivator c) => c.Demonic ? DemonQi : KiemKhi[Mathf.Clamp((int)c.Realm, 0, KiemKhi.Length - 1)];

        void StartFight(WorldEvent ev, int salt)
        {
            if (FightScenes.Active.Count >= FightScenes.Max) return;
            var all = _sim.Cultivation.All;
            Cultivator a = ev.A >= 0 && ev.A < all.Count ? all[ev.A] : null, b = ev.B >= 0 && ev.B < all.Count ? all[ev.B] : null;
            if (a == null) return;
            bool beast = ev.Fx >= Fx.BeastSlain;
            var f = FightScene.Make(Time.unscaledTime, ev.X, ev.Y, (int)(ev.Tick * 31 + salt), beast ? 5 : 7);
            // The beast in the fight, drawn as its own kind and size.
            var who = beast ? _sim.Beasts.LookNear(ev.X, ev.Y) : null;
            var beastLook = who != null ? SpriteLibrary.BeastUnit((int)who.Kind, who.Grade) : SpriteLibrary.Unit.Beast;
            switch (ev.Fx)
            {
                case Fx.DuelKill: // A died at B's hand
                    if (b == null) return;
                    Set(f, b, a, true);
                    break;
                case Fx.DuelFlee: // A beat B
                    if (b == null) return;
                    Set(f, a, b, false);
                    break;
                case Fx.BeastSlain: // A cut down a beast
                    f.WinnerLook = UnitRenderer.CultivatorLook(a);
                    f.WinnerColor = QiOf(a);
                    f.WinnerIdx = a.Index;
                    f.LoserLook = beastLook;
                    f.LoserColor = Claw;
                    f.LoserClaws = true;
                    f.LoserDies = true;
                    break;
                default: // a beast killed A, or A fled from it
                    f.WinnerLook = beastLook;
                    f.WinnerColor = Claw;
                    f.WinnerClaws = true;
                    f.LoserLook = UnitRenderer.CultivatorLook(a);
                    f.LoserColor = QiOf(a);
                    f.LoserIdx = a.Index;
                    f.LoserDies = ev.Fx == Fx.BeastKill;
                    break;
            }
            FightScenes.Active.Add(f);
        }

        void Set(FightScene f, Cultivator winner, Cultivator loser, bool dies)
        {
            f.WinnerLook = UnitRenderer.CultivatorLook(winner);
            f.LoserLook = UnitRenderer.CultivatorLook(loser);
            f.WinnerColor = QiOf(winner);
            f.LoserColor = QiOf(loser);
            f.WinnerIdx = winner.Index;
            f.LoserIdx = loser.Index;
            f.LoserDies = dies;
        }

        // Kiếm khí flying between the fighters, sparks where it lands, a burst on the final blow, dust when one falls.
        void DrawFights(float now)
        {
            var list = FightScenes.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                if (now > f.End) { list.RemoveAt(i); continue; }
                Vector2 w = f.WinnerPos + new Vector2(0f, 0.7f), l = f.LoserPos + new Vector2(0f, 0.7f);
                for (int k = 0; k < f.Blows.Count; k++)
                {
                    var (at, byWinner) = f.Blows[k];
                    Vector2 from = byWinner ? w : l, to = byWinner ? l : w;
                    var color = byWinner ? f.WinnerColor : f.LoserColor;
                    bool claws = byWinner ? f.WinnerClaws : f.LoserClaws;
                    float t = (now - at) / FightScene.Flight;
                    if (t >= 0f && t < 1f && !claws)
                    {
                        // A bolt of kiếm khí with a short trail.
                        var p = Vector2.Lerp(from, to, t);
                        Quad(Sprite.QiShot, p.x, p.y, 1f, 1f, color, to.x < from.x);
                        var trail = Vector2.Lerp(from, to, Mathf.Max(0f, t - 0.25f));
                        Quad(Sprite.Spark, trail.x, trail.y, 1.2f, 1.2f, new Color32(color.r, color.g, color.b, 160));
                    }
                    if (k < f.Impacts || t < 1f) continue;
                    // It lands: sparks (claw marks are a red slash), and the final blow bursts.
                    f.Impacts = k + 1;
                    bool final = k == f.Blows.Count - 1;
                    Burst(to.x, to.y, claws ? Claw : color, final ? 14 : 6, final ? 6f : 3.5f, final);
                    if (claws)
                        for (int s = 0; s < 3; s++)
                            _particles.Add(new Particle { Sprite = Sprite.Spark, X = to.x - 0.3f + s * 0.3f, Y = to.y + 0.4f, VY = -3f,
                                Life = 0.2f, Size = 1.3f, Color = Claw });
                }
                float sinceFinal = now - f.FinalBlow;
                if (sinceFinal >= 0f && sinceFinal < 0.3f)
                    Quad(Sprite.Ring, l.x, l.y - 0.4f, 0.6f + sinceFinal * 6f, 0.4f + sinceFinal * 3f, new Color32(255, 240, 220, (byte)(220 * (1f - sinceFinal / 0.3f))));
                if (f.LoserDies && !f.Smoked && sinceFinal > 0.15f)
                {
                    f.Smoked = true;
                    Poof(l.x, l.y - 0.3f); // the fallen goes up in white smoke and is gone
                }
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
                case Fx.Quake:
                    e.Duration = 2.4f;
                    for (int k = 0; k < 10; k++) // dust thrown up across the shaken ground
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-7f, 7f), Y = y + Rand(-5f, 5f), VX = Rand(-0.4f, 0.4f), VY = Rand(0.3f, 1f),
                            Life = Rand(1.2f, 2.2f), Size = Rand(1f, 1.8f), Grow = 0.6f, Color = new Color32(160, 130, 96, 170) });
                    break;
                case Fx.Eruption:
                    e.Duration = 6f;
                    Burst(x, y + 1f, new Color32(255, 120, 40, 255), 30, 9f);
                    break;
                case Fx.Miasma:
                    e.Duration = 3f;
                    break;
                case Fx.Rain:
                case Fx.Snow:
                case Fx.Storm:
                    e.Duration = kind == Fx.Storm ? 3f : 3.5f;
                    break;
                case Fx.Stampede:
                    e.Duration = 2.2f;
                    for (int k = 0; k < 7; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-5f, 5f), Y = y + Rand(-3f, 3f), VX = Rand(-2f, 2f), VY = Rand(0.2f, 0.8f),
                            Life = Rand(0.8f, 1.6f), Size = Rand(0.8f, 1.4f), Grow = 0.7f, Color = new Color32(140, 120, 100, 160) });
                    break;
            }
            _effects.Add(e);
        }

        void Burst(float x, float y, Color32 color, int count, float speed, bool smoke = true)
        {
            for (int k = 0; k < count; k++)
            {
                float a = Rand(0f, Mathf.PI * 2f), v = Rand(speed * 0.4f, speed);
                _particles.Add(new Particle { Sprite = Sprite.Spark, X = x, Y = y, VX = Mathf.Cos(a) * v, VY = Mathf.Sin(a) * v + 2f, Gravity = 8f,
                    Life = Rand(0.4f, 0.8f), Size = Rand(0.8f, 1.5f), Color = color });
            }
            for (int k = 0; k < (smoke ? 2 : 0); k++)
                _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.5f, 0.5f), Y = y + Rand(0f, 0.5f), VX = Rand(-0.3f, 0.3f), VY = Rand(0.4f, 0.9f),
                    Life = Rand(0.6f, 1f), Size = 1f, Color = new Color32(96, 92, 96, 255) });
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
            DrawFights(now);
            DrawRelicBeacons(view, now);
            foreach (var p in FightScenes.Poofs) Poof(p.x, p.y + 0.4f);
            FightScenes.Poofs.Clear();
            FightScenes.Hurt.RemoveAll(z => now - z.Start > FightScenes.HurtTime);

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
                // Pixel smoke stays solid, swells in whole steps and then is simply gone; translucent puffs
                // stacked on each other read as blur. Sparks and drops fade in hard steps.
                c.a = p.Sprite == Sprite.Smoke ? (byte)255 : (byte)(c.a * k);
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

                case Fx.Quake:
                    // Shock waves rolling out from the epicentre.
                    for (int k = 0; k < 3; k++)
                    {
                        float st = t - k * 0.35f;
                        if (st < 0f || st > 1.4f) continue;
                        Quad(Sprite.Ring, e.X, e.Y, 1f + st * 9f, 0.6f + st * 5f, new Color32(190, 150, 100, (byte)(200 * (1f - st / 1.4f))));
                    }
                    break;

                case Fx.Eruption:
                {
                    // A column of fire and ash out of the crater, flecks of lava raining back down.
                    float fade = Mathf.Clamp01((e.Duration - t) / 1.5f);
                    Quad(Sprite.Flash, e.X, e.Y + 1f, 3f, 3f, new Color32(255, 140, 50, (byte)(200 * fade)));
                    if (_rand.NextDouble() < 0.8 * fade)
                        _particles.Add(new Particle { Sprite = (Sprite)((int)Sprite.Fire0 + _rand.Next(3)), X = e.X + Rand(-0.6f, 0.6f), Y = e.Y + 1f,
                            VX = Rand(-2.5f, 2.5f), VY = Rand(6f, 11f), Gravity = 9f, Life = Rand(0.9f, 1.6f), Size = Rand(0.8f, 1.3f), Color = new Color32(255, 255, 255, 255) });
                    if (_rand.NextDouble() < 0.5 * fade)
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = e.X + Rand(-0.8f, 0.8f), Y = e.Y + Rand(2f, 4f), VX = Rand(-0.3f, 0.6f), VY = Rand(1.5f, 3f),
                            Life = Rand(2f, 3.5f), Size = Rand(1.2f, 2f), Grow = 1.2f, Color = new Color32(60, 54, 52, 200) });
                    break;
                }

                case Fx.Miasma:
                    // Sickly green vapour rising over the stricken village.
                    if (_rand.NextDouble() < 0.5 * Mathf.Clamp01((e.Duration - t) / 1f))
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = e.X + Rand(-4f, 4f), Y = e.Y + Rand(-2f, 3f), VX = Rand(-0.2f, 0.2f), VY = Rand(0.3f, 0.8f),
                            Life = Rand(1.5f, 2.5f), Size = Rand(1f, 1.6f), Grow = 0.5f, Color = new Color32(120, 200, 90, 140) });
                    break;

                case Fx.Stampede:
                    if (_rand.NextDouble() < 0.4)
                        Burst(e.X + Rand(-5f, 5f), e.Y + Rand(-3f, 3f), new Color32(230, 60, 50, 255), 4, 3f);
                    break;

                case Fx.Rain:
                case Fx.Snow:
                case Fx.Storm:
                {
                    // Clouds over the spot; rain streaks, drifting snow, or rain driven sideways by the gale.
                    float fade = Mathf.Clamp01(Mathf.Min(t / 0.4f, (e.Duration - t) / 0.8f));
                    var cloud = e.Kind == Fx.Snow ? new Color32(220, 228, 240, 255) : new Color32(70, 76, 96, 255);
                    cloud.a = (byte)(190 * fade);
                    float drift = e.Kind == Fx.Storm ? Mathf.Sin(t * 3f) * 1.5f : 0f;
                    Quad(Sprite.Cloud, e.X - 3f + drift, e.Y + 9f, 2f, 2f, cloud);
                    Quad(Sprite.Cloud, e.X + 3f + drift, e.Y + 9.5f, 2.2f, 2.2f, cloud);
                    int drops = e.Kind == Fx.Storm ? 4 : 2;
                    for (int k = 0; k < drops && fade > 0.2f; k++)
                    {
                        bool snow = e.Kind == Fx.Snow;
                        _particles.Add(new Particle
                        {
                            Sprite = snow ? Sprite.Spark : Sprite.Drop, X = e.X + Rand(-6f, 6f), Y = e.Y + 8.5f,
                            VX = e.Kind == Fx.Storm ? Rand(4f, 7f) : snow ? Rand(-0.6f, 0.6f) : 0f, VY = snow ? Rand(-2.5f, -1.5f) : Rand(-14f, -10f),
                            Life = snow ? Rand(2.5f, 3.5f) : Rand(0.6f, 0.8f), Size = snow ? 0.8f : 1f,
                            Color = snow ? new Color32(250, 250, 255, 230) : new Color32(170, 200, 255, 200)
                        });
                    }
                    if (e.Kind == Fx.Storm && _rand.NextDouble() < 0.03) _particles.Add(new Particle { Sprite = Sprite.Flash, X = e.X + Rand(-4f, 4f), Y = e.Y + 9f,
                        Life = 0.15f, Size = 2.5f, Color = new Color32(230, 235, 255, 220) });
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

        // Bí cảnh the world knows of stand out from afar: a column of light in their colour pulsing in hard steps,
        // motes rising off them; while sects fight over one, a red ring beats around it.
        void DrawRelicBeacons(Rect view, float now)
        {
            foreach (var r in _sim.Relics.All)
            {
                if (!r.Discovered || !r.Open || r.ObjectId < 0) continue;
                float x = r.X + 0.5f, y = r.Y + 0.5f;
                if (!view.Contains(new Vector2(x, y))) continue;
                Color32 col = r.Kind == RelicKind.Treasure ? new Color32(255, 230, 120, 255)
                    : r.Kind == RelicKind.Tomb ? new Color32(120, 250, 200, 255)
                    : r.Kind == RelicKind.Ancient ? new Color32(190, 140, 255, 255)
                    : r.Kind == RelicKind.Ruins ? new Color32(255, 170, 110, 255)
                    : new Color32(140, 220, 255, 255);
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 2.2f + r.Index * 1.7f);
                col.a = (byte)(150 + 105 * pulse);
                QuadBottom(Sprite.Beam, x, y + 0.5f, 1f + (r.Tier >= 4 ? 1f : 0f), col, 2f);
                // A halo at its foot, beating with the light.
                Quad(Sprite.Ring, x, y + 0.4f, 2f + pulse * 2f, 1f + pulse, new Color32(col.r, col.g, col.b, (byte)(120 + 100 * pulse)));
                if (_rand.NextDouble() < 0.15 + 0.05 * r.Tier)
                    _particles.Add(new Particle
                    {
                        Sprite = Sprite.Spark, X = x + Rand(-1f, 1f), Y = y + Rand(0f, 1f), VY = Rand(1.2f, 2.6f), Life = Rand(1.2f, 2.2f), Size = 1f,
                        Color = new Color32(col.r, col.g, col.b, 230)
                    });
                if (_sim.Relics.Contested(r))
                {
                    float k = (now * 0.8f + r.Index * 0.3f) % 1f;
                    Quad(Sprite.Ring, x, y + 0.2f, 1f + k * 5f, 0.6f + k * 2.5f, new Color32(255, 80, 60, (byte)(220 * (1f - k))));
                }
            }
        }

        // Pixel art rules for every effect: sprites only grow by whole multiples (so a pixel stays a square block),
        // sit on the 8-px-per-cell grid like the map, and fade in a few hard steps instead of a smooth blur.
        static int Whole(float scale) => Mathf.Max(1, Mathf.RoundToInt(scale));

        // Centred quad scaled from the sprite's native size.
        void Quad(Sprite s, float x, float y, float sx, float sy, Color32 color, bool flip = false)
        {
            var size = _size[(int)s];
            float w = size.x * Whole(sx), h = size.y * Whole(sy);
            AddQuad(s, x - w * 0.5f, y - h * 0.5f, w, h, color, flip);
        }

        // Anchored at the bottom centre (bolts, beams strike down to the ground).
        void QuadBottom(Sprite s, float x, float y, float scale, Color32 color, float widthScale = 1f)
        {
            var size = _size[(int)s];
            float w = size.x * Whole(scale * widthScale), h = size.y * Whole(scale);
            AddQuad(s, x - w * 0.5f, y, w, h, color);
        }

        static byte StepAlpha(byte a) => a < 40 ? (byte)0 : a < 120 ? (byte)96 : a < 200 ? (byte)170 : (byte)255;

        void AddQuad(Sprite s, float x0, float y0, float w, float h, Color32 color, bool flip = false)
        {
            if (_verts.Count >= MaxQuads * 4) return;
            color.a = StepAlpha(color.a);
            if (color.a == 0) return;
            x0 = Mathf.Round(x0 * CellPx) / CellPx;
            y0 = Mathf.Round(y0 * CellPx) / CellPx;
            var uv = _uv[(int)s];
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
            _verts.Add(new Vector3(x0, y0, 0f));
            _verts.Add(new Vector3(x0, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0 + h, 0f));
            _verts.Add(new Vector3(x0 + w, y0, 0f));
            _uvs.Add(new Vector2(u0, uv.yMin));
            _uvs.Add(new Vector2(u0, uv.yMax));
            _uvs.Add(new Vector2(u1, uv.yMax));
            _uvs.Add(new Vector2(u1, uv.yMin));
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

            // Pixel art only: every pixel is either fully there or not, in two or three flat shades (the vertex colour
            // tints the white ones). No soft gradients: those read as blur next to the map's chunky pixels.
            var white = new Color32(255, 255, 255, 255);
            var light = new Color32(214, 214, 222, 255);
            var shade = new Color32(170, 172, 186, 255);
            Color32 Hard(bool on, Color32 c) => on ? c : default;
            float Dist(int x, int y, float cx, float cy) => Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));

            // Jagged bolt: a one-pixel white core with a pale blue pixel either side, zigzagging down in steps.
            Color32[] Bolt(int seed)
            {
                const int w = 16, h = 96;
                var b = new Color32[w * h];
                var r = new System.Random(seed);
                int x = 8;
                var edge = new Color32(170, 205, 255, 255);
                for (int y = h - 1; y >= 0; y--)
                {
                    if (y % 4 == 0) x = Mathf.Clamp(x + r.Next(-2, 3), 3, 12);
                    if (b[y * w + x - 1].a == 0) b[y * w + x - 1] = edge;
                    if (b[y * w + x + 1].a == 0) b[y * w + x + 1] = edge;
                    b[y * w + x] = white;
                    if (y > 20 && r.NextDouble() < 0.04) // short branch, a pixel staircase
                    {
                        int dir = r.Next(2) == 0 ? -1 : 1;
                        for (int k = 1; k < 7 && y - k >= 0; k++)
                        {
                            int bx = x + dir * ((k + 1) / 2);
                            if (bx >= 0 && bx < w) b[(y - k) * w + bx] = edge;
                        }
                    }
                }
                return b;
            }

            var boltA = Bolt(11);
            var boltB = Bolt(29);
            Put(Sprite.BoltA, 16, 96, (x, y) => boltA[y * 16 + x]);
            Put(Sprite.BoltB, 16, 96, (x, y) => boltB[y * 16 + x]);
            // Light pillar: white core, pale sides, thinning out at the top in a checker dither.
            Put(Sprite.Beam, 8, 96, (x, y) =>
            {
                bool core = x == 3 || x == 4, side = x == 2 || x == 5;
                if (!core && !side) return default;
                if (y > 60 && ((x + y) & 1) == 1) return default;
                if (y > 82 && !core) return default;
                return core ? white : light;
            });
            // Impact star: a plus with short diagonals.
            Put(Sprite.Flash, 9, 9, (x, y) =>
            {
                int dx = Mathf.Abs(x - 4), dy = Mathf.Abs(y - 4);
                if (dx <= 1 && dy <= 1) return white;
                if (dx == 0 || dy == 0) return dx + dy <= 4 ? (dx + dy <= 2 ? white : light) : default;
                return dx == dy && dx == 2 ? light : default;
            });
            // Shock ring: a one-pixel circle.
            Put(Sprite.Ring, 15, 15, (x, y) => Hard(Mathf.Abs(Dist(x, y, 7.5f, 7.5f) - 6f) < 0.55f, white));
            // Cloud: a lumpy pixel bank, lit on top.
            Put(Sprite.Cloud, 32, 12, (x, y) =>
            {
                bool inside = false;
                for (int k = 0; k < 4; k++)
                    if (Dist(x, y, 5f + k * 7.5f, 5f + (k % 2) * 2f) <= 4.5f + (k % 2)) inside = true;
                if (!inside) return default;
                return y >= 7 ? white : y >= 4 ? light : shade;
            });
            // Smoke puff: a round blob with a highlight and a darker rim.
            Put(Sprite.Smoke, 8, 8, (x, y) =>
            {
                float d = Dist(x, y, 4f, 4f);
                if (d > 3.7f) return default;
                if (x <= 3 && y >= 4 && d < 2.6f) return white;
                return d > 2.9f ? shade : light;
            });
            Put(Sprite.Spark, 2, 2, (x, y) => white);
            Put(Sprite.Drop, 3, 3, (x, y) => Hard(x == 1 || y == 1, white));
            // Kiếm khí: a short streak of light, bright head, fading tail in steps.
            Put(Sprite.QiShot, 7, 3, (x, y) =>
            {
                if (y == 1) return x >= 4 ? white : x >= 2 ? light : shade;
                return x >= 3 && x <= 5 ? light : default;
            });
            // Flames: three flat bands, red outside, orange, a yellow heart.
            for (int f = 0; f < 3; f++)
            {
                int frame = f;
                Put((Sprite)((int)Sprite.Fire0 + f), 8, 12, (x, y) =>
                {
                    int sway = ((y + frame * 3) / 3) % 2 == 0 ? 0 : (frame == 1 ? 1 : -1);
                    int half = Mathf.Max(0, 3 - y / 3);
                    int d = Mathf.Abs(x - 4 - sway);
                    if (d > half || y > 10) return default;
                    if (d == half) return new Color32(220, 70, 30, 255);
                    return d <= half - 2 && y < 7 ? new Color32(255, 236, 120, 255) : new Color32(255, 150, 40, 255);
                });
            }

            atlas = new Texture2D(tw, th, TextureFormat.RGBA32, false) { name = "FxAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixelData(px, 0);
            atlas.Apply(false);
        }
    }
}
