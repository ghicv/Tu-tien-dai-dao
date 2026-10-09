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

        enum Sprite
        {
            BoltA, BoltB, Flash, Ring, Spark, Smoke, Fire0, Fire1, Fire2, Cloud, Beam, Drop, QiShot, Rift, RiftThin,
            Orb, Rock, Shard, Crescent, Blade, Leaf, Exclaim,                     // spells by element (devlog 32)
            LootSword, LootSlip, LootPill, LootGem, LootStones, LootCore, LootHerb, // a cơ duyên held up overhead
            IconHerb, IconHunt, IconCoin, IconFlag, IconLotus, IconHome, Count     // what they are out doing (devlog 33)
        }

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

        // Tin đồn: while a talked-about relic or hung thú is on the card, the edge of each place its word has
        // reached is drawn as a ring of pixel dots that marches slowly outward-round, so the player sees who knows.
        public Rumor ShownRumor;

        void DrawRumor(Rect view, float now)
        {
            var word = ShownRumor;
            if (word == null) return;
            var tint = new Color32(255, 214, 110, 200);
            foreach (var d in word.Discs)
            {
                if (d.X + d.R < view.xMin || d.X - d.R > view.xMax || d.Y + d.R < view.yMin || d.Y - d.R > view.yMax) continue;
                int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * d.R / 2.5f), 12, 220);
                float phase = now * 0.15f;
                for (int k = 0; k < n; k++)
                {
                    if (((k + (int)(now * 3f)) & 3) == 0) continue; // dashes, marching round
                    float a = (k + phase) / n * Mathf.PI * 2f;
                    float x = d.X + Mathf.Cos(a) * d.R, y = d.Y + Mathf.Sin(a) * d.R;
                    if (view.Contains(new Vector2(x, y))) Quad(Sprite.Spark, x, y, 2f, 2f, tint);
                }
            }
        }

        // A village praying to Thiên Đạo: incense smoke rising in a thin column from its heart, a golden fleck now and then.
        void DrawPrayers(Rect view, float dt)
        {
            var all = _sim.Settlements.All;
            foreach (var p in _sim.Faith.Open)
            {
                var s = all[p.Settlement];
                float x = s.X + 0.5f, y = s.Y + 1f;
                if (!view.Contains(new Vector2(x, y))) continue;
                if (_rand.NextDouble() < dt * 5f)
                    _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.3f, 0.3f), Y = y, VX = Rand(-0.15f, 0.15f), VY = Rand(0.8f, 1.3f),
                        Life = Rand(1.8f, 2.6f), Size = 1f, Color = new Color32(226, 222, 214, 255) });
                if (_rand.NextDouble() < dt * 2f)
                    _particles.Add(new Particle { Sprite = Sprite.Spark, X = x + Rand(-0.8f, 0.8f), Y = y + Rand(0.5f, 2.5f), VY = Rand(0.4f, 0.9f),
                        Life = Rand(0.6f, 1.1f), Size = 1f, Color = new Color32(255, 214, 110, 255) });
            }
        }

        // A Hóa Thần who crossed the land in a single day tore the void: a rift where they went in and one where
        // they came out. Only Hóa Thần are checked, and there are never many.
        readonly Dictionary<int, long> _rifted = new Dictionary<int, long>();

        void WatchRifts(Rect view)
        {
            var e = _sim.Entities;
            long tick = _sim.Clock.Tick;
            foreach (var c in _sim.Cultivation.All)
            {
                if (!c.Alive || c.Realm < Realm.HoaThan) continue;
                int id = c.Entity;
                float dx = e.X[id] - e.PrevX[id], dy = e.Y[id] - e.PrevY[id];
                if (dx * dx + dy * dy < CreatureSystem.RiftJump * CreatureSystem.RiftJump) continue;
                if (_rifted.TryGetValue(c.Index, out long seen) && seen == tick) continue;
                _rifted[c.Index] = tick;
                if (_effects.Count >= MaxEffects) continue;
                if (view.Contains(new Vector2(e.PrevX[id], e.PrevY[id]))) Spawn(Fx.VoidRift, e.PrevX[id], e.PrevY[id]);
                if (view.Contains(new Vector2(e.X[id], e.Y[id]))) Spawn(Fx.VoidRift, e.X[id], e.Y[id]);
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

        // An animal caught by a hunter: a slash, a spray of red, a tuft of fur.
        void Torn(float x, float y)
        {
            float cy = y + 0.4f;
            for (int k = 0; k < 3; k++)
                _particles.Add(new Particle { Sprite = Sprite.Spark, X = x - 0.3f + k * 0.3f, Y = cy + 0.4f, VY = -3f, Life = 0.2f, Size = 1.3f, Color = Claw });
            for (int k = 0; k < 7; k++)
                _particles.Add(new Particle { Sprite = Sprite.Drop, X = x, Y = cy, VX = Rand(-2f, 2f), VY = Rand(1f, 3.5f), Gravity = 12f, Life = Rand(0.4f, 0.7f), Size = 1f,
                    Color = new Color32(200, 30, 30, 255) });
            _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x, Y = cy, VY = 0.6f, Life = 0.45f, Size = 1f, Grow = 1f, Color = new Color32(196, 170, 140, 255) });
        }

        // ---------------------------------------------------------------- fights played out

        static readonly Color32[] KiemKhi =
        {
            new Color32(200, 200, 200, 255), new Color32(150, 205, 255, 255), new Color32(120, 255, 170, 255),
            new Color32(255, 220, 110, 255), new Color32(210, 150, 255, 255), new Color32(240, 250, 255, 255)
        };
        static readonly Color32 DemonQi = new Color32(255, 70, 70, 255);
        static readonly Color32 Claw = new Color32(255, 110, 70, 255);

        // The colour of each spell (Qi takes the caster's realm instead).
        static readonly Color32[] SpellColor =
        {
            new Color32(200, 200, 200, 255), // Qi
            new Color32(255, 236, 160, 255), // Kim: pale gold sword-light
            new Color32(120, 220, 110, 255), // Mộc
            new Color32(110, 180, 255, 255), // Thủy
            new Color32(255, 140, 50, 255),  // Hỏa
            new Color32(186, 146, 96, 255),  // Thổ
            new Color32(196, 206, 255, 255), // Lôi
            new Color32(210, 255, 226, 255), // Phong
            new Color32(176, 240, 255, 255), // Băng
            new Color32(214, 40, 70, 255),   // Ma công
            new Color32(255, 110, 70, 255),  // Claw
            new Color32(255, 120, 40, 255),  // Flame (dragon, qilin, fox fire)
            new Color32(150, 230, 80, 255),  // Venom
            new Color32(90, 170, 255, 255),  // Tide
            new Color32(226, 250, 240, 255), // Gale
            new Color32(204, 146, 255, 255), // Sonic
        };

        Color32 QiOf(Cultivator c) => c.Demonic ? DemonQi : KiemKhi[Mathf.Clamp((int)c.Realm, 0, KiemKhi.Length - 1)];

        // A cultivator fights with their method: its element, or ma công if they walk the demonic path, or plain
        // kiếm khí for a vạn năng method. A sect's members share its trấn phái công pháp, so a sect has its look.
        Spell SpellOf(Cultivator c)
        {
            if (c.Demonic) return Spell.Ma;
            var t = _sim.Techniques?.Of(c);
            return t == null || t.Element < 0 ? Spell.Qi : (Spell)((int)Spell.Kim + t.Element);
        }

        static Spell SpellOf(Beast b)
        {
            if (b == null || b.Grade < 3) return Spell.Claw; // the lesser ones only tear
            switch (b.Kind)
            {
                case BeastKind.Dragon: case BeastKind.Qilin: case BeastKind.Fox: return Spell.Flame;
                case BeastKind.Serpent: case BeastKind.Scorpion: return Spell.Venom;
                case BeastKind.Turtle: return Spell.Tide;
                case BeastKind.Eagle: return Spell.Gale;
                case BeastKind.Bat: return Spell.Sonic;
                case BeastKind.Chaos: return Spell.Ma;
                default: return Spell.Claw;
            }
        }

        Color32 ColorOf(Spell s, Cultivator c) => s == Spell.Qi && c != null ? QiOf(c) : SpellColor[(int)s];

        void StartFight(WorldEvent ev, int salt)
        {
            if (FightScenes.Active.Count >= FightScenes.Max) return;
            var all = _sim.Cultivation.All;
            Cultivator a = ev.A >= 0 && ev.A < all.Count ? all[ev.A] : null, b = ev.B >= 0 && ev.B < all.Count ? all[ev.B] : null;
            if (a == null) return;
            bool beast = ev.Fx >= Fx.BeastSlain;
            var f = FightScene.Make(Time.unscaledTime, ev.X, ev.Y, (int)(ev.Tick * 31 + salt), beast ? 7 : 9);
            // The beast in the fight, drawn as its own kind and size.
            var who = beast ? _sim.Beasts.LookNear(ev.X, ev.Y) : null;
            var beastLook = who != null ? SpriteLibrary.BeastUnit((int)who.Kind, who.Grade) : SpriteLibrary.Unit.Beast;
            float beastScale = who != null ? SpriteLibrary.BeastScale(who.Grade, who.Rampage) : 1f;
            var beastSpell = SpellOf(who);
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
                    f.WinnerSpell = SpellOf(a);
                    f.WinnerColor = ColorOf(f.WinnerSpell, a);
                    f.WinnerIdx = a.Index;
                    f.LoserLook = beastLook;
                    f.LoserScale = beastScale;
                    f.LoserSpell = beastSpell;
                    f.LoserColor = ColorOf(beastSpell, null);
                    f.LoserDies = true;
                    break;
                default: // a beast killed A, or A fled from it
                    f.WinnerLook = beastLook;
                    f.WinnerScale = beastScale;
                    f.WinnerSpell = beastSpell;
                    f.WinnerColor = ColorOf(beastSpell, null);
                    f.LoserLook = UnitRenderer.CultivatorLook(a);
                    f.LoserSpell = SpellOf(a);
                    f.LoserColor = ColorOf(f.LoserSpell, a);
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
            f.WinnerSpell = SpellOf(winner);
            f.LoserSpell = SpellOf(loser);
            f.WinnerColor = ColorOf(f.WinnerSpell, winner);
            f.LoserColor = ColorOf(f.LoserSpell, loser);
            f.WinnerIdx = winner.Index;
            f.LoserIdx = loser.Index;
            f.LoserDies = dies;
        }

        // Each blow: the caster gathers qi (a glint at the hands), the spell flies in its element's shape, and where it
        // lands it bursts in its own way. A dodged one sails past and scars the ground behind; the final one bursts big.
        void DrawFights(float now)
        {
            var list = FightScenes.Active;
            float dt = Time.unscaledDeltaTime;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var f = list[i];
                if (now > f.End) { list.RemoveAt(i); continue; }
                Vector2 w = f.WinnerPos + new Vector2(0f, 0.7f + f.Shift(true, now)), l = f.LoserPos + new Vector2(0f, 0.7f + (now > f.FinalBlow ? 0f : f.Shift(false, now)));
                for (int k = 0; k < f.Blows.Count; k++)
                {
                    var (at, byWinner) = f.Blows[k];
                    Vector2 from = byWinner ? w : l, to = byWinner ? l : w;
                    var spell = byWinner ? f.WinnerSpell : f.LoserSpell;
                    var color = byWinner ? f.WinnerColor : f.LoserColor;
                    bool dodged = f.Dodged(k);
                    if (dodged) to += (to - from) * 0.9f + new Vector2(0f, -0.6f); // past them, into the ground
                    float t = (now - at) / FightScene.Flight;
                    if (spell != Spell.Claw && t >= -0.6f && t < 0f) Charge(from, to, color, -t);
                    if (t >= 0f && t < 1f) DrawSpell(spell, from, to, t, color, now, dt);
                    if (k < f.Impacts || t < 1f) continue;
                    f.Impacts = k + 1;
                    bool final = k == f.Blows.Count - 1;
                    Impact(spell, to, from, color, final ? 2 : dodged ? 0 : 1);
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

        // Gathering qi before a cast: motes drawn in to the hands, then a glint.
        void Charge(Vector2 from, Vector2 to, Color32 color, float left)
        {
            float side = to.x < from.x ? -0.4f : 0.4f;
            var hand = from + new Vector2(side, -0.1f);
            if (left < 0.25f) Quad(Sprite.Flash, hand.x, hand.y, 1f, 1f, new Color32(color.r, color.g, color.b, 220));
            else if (_rand.NextDouble() < 0.5)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                var p = hand + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.9f;
                _particles.Add(new Particle { Sprite = Sprite.Spark, X = p.x, Y = p.y, VX = (hand.x - p.x) * 4f, VY = (hand.y - p.y) * 4f,
                    Life = 0.22f, Size = 1f, Color = color });
            }
        }

        // A spell in flight, t from 0 (cast) to 1 (it lands).
        void DrawSpell(Spell s, Vector2 from, Vector2 to, float t, Color32 c, float now, float dt)
        {
            bool left = to.x < from.x;
            var p = Vector2.Lerp(from, to, t);
            var faint = new Color32(c.r, c.g, c.b, 150);
            switch (s)
            {
                case Spell.Claw:
                    return; // the beast pounces instead (UnitRenderer)
                case Spell.Kim: // a flying sword of light, its wake glittering
                    Quad(Sprite.Blade, p.x, p.y, 1f, 1f, c, left);
                    Trail(Sprite.Spark, p, from, to, t, faint, dt, 30f);
                    break;
                case Spell.Moc: // a whirl of leaves
                    for (int j = 0; j < 4; j++)
                    {
                        float a = now * 16f + j * Mathf.PI * 0.5f;
                        Quad(Sprite.Leaf, p.x + Mathf.Cos(a) * 0.4f, p.y + Mathf.Sin(a) * 0.4f, 1f, 1f, new Color32(255, 255, 255, 255), j % 2 == 0);
                    }
                    break;
                case Spell.Thuy:
                case Spell.Tide: // a ball of water, dripping as it flies
                    Quad(Sprite.Orb, p.x, p.y, s == Spell.Tide ? 2f : 1f, s == Spell.Tide ? 2f : 1f, c);
                    if (_rand.NextDouble() < dt * 25f)
                        _particles.Add(new Particle { Sprite = Sprite.Drop, X = p.x, Y = p.y, VX = Rand(-0.5f, 0.5f), Gravity = 10f, Life = 0.35f, Size = 1f, Color = c });
                    break;
                case Spell.Hoa:
                case Spell.Flame: // a fireball (a dragon breathes a stream of them), embers and smoke behind
                    int frame = ((int)(now * 12f)) % 3;
                    Quad((Sprite)((int)Sprite.Fire0 + frame), p.x, p.y, 1f, 1f, new Color32(255, 255, 255, 255), left);
                    if (s == Spell.Flame)
                        for (int j = 1; j <= 2; j++)
                        {
                            var q = Vector2.Lerp(from, to, Mathf.Max(0f, t - j * 0.18f));
                            Quad((Sprite)((int)Sprite.Fire0 + (frame + j) % 3), q.x, q.y, 1f, 1f, new Color32(255, 255, 255, 255), left);
                        }
                    if (_rand.NextDouble() < dt * 40f)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = p.x + Rand(-0.2f, 0.2f), Y = p.y, VX = Rand(-0.4f, 0.4f), VY = Rand(0.8f, 1.8f),
                            Life = Rand(0.3f, 0.6f), Size = 1f, Color = new Color32(255, (byte)Rand(120, 230), 40, 255) });
                    break;
                case Spell.Tho: // a boulder hurled in an arc
                    p.y += Mathf.Sin(t * Mathf.PI) * 1.3f;
                    Quad(Sprite.Rock, p.x, p.y, 1f, 1f, new Color32(255, 255, 255, 255), ((int)(now * 10f) & 1) == 0);
                    break;
                case Spell.Loi: // a jagged arc of lightning from the hand, flickering every frame
                {
                    float len = (p - from).magnitude;
                    int n = Mathf.Max(2, Mathf.RoundToInt(len * 5f));
                    var dir = (p - from) / Mathf.Max(0.01f, len);
                    var nrm = new Vector2(-dir.y, dir.x);
                    for (int j = 0; j <= n; j++)
                    {
                        float u = (float)j / n;
                        var q = Vector2.Lerp(from, p, u) + nrm * Rand(-0.25f, 0.25f) * Mathf.Sin(u * Mathf.PI);
                        Quad(Sprite.Spark, q.x, q.y, 1f, 1f, j % 3 == 0 ? new Color32(255, 255, 255, 255) : c);
                    }
                    Quad(Sprite.Flash, p.x, p.y, 1f, 1f, new Color32(255, 255, 255, 255));
                    break;
                }
                case Spell.Phong:
                case Spell.Gale: // a crescent of wind, a fainter one chasing it
                    Quad(Sprite.Crescent, p.x, p.y, 1f, 1f, c, !left);
                    var back = Vector2.Lerp(from, to, Mathf.Max(0f, t - 0.22f));
                    Quad(Sprite.Crescent, back.x, back.y, 1f, 1f, faint, !left);
                    break;
                case Spell.Bang: // an ice spear shedding frost
                    Quad(Sprite.Shard, p.x, p.y, 1f, 1f, c, left);
                    if (_rand.NextDouble() < dt * 25f)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = p.x, Y = p.y, VY = -0.6f, Life = 0.5f, Size = 1f, Color = new Color32(235, 250, 255, 255) });
                    break;
                case Spell.Ma: // a ball of blood-dark qi, black smoke curling off it
                    Quad(Sprite.Orb, p.x, p.y, 1f, 1f, c);
                    if (_rand.NextDouble() < dt * 20f)
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = p.x, Y = p.y, VX = Rand(-0.3f, 0.3f), VY = Rand(0.2f, 0.6f),
                            Life = Rand(0.3f, 0.5f), Size = 1f, Color = new Color32(54, 18, 40, 255) });
                    break;
                case Spell.Venom: // a glob of poison, dripping
                    Quad(Sprite.Orb, p.x, p.y, 1f, 1f, c);
                    if (_rand.NextDouble() < dt * 20f)
                        _particles.Add(new Particle { Sprite = Sprite.Drop, X = p.x, Y = p.y, Gravity = 9f, Life = 0.4f, Size = 1f, Color = c });
                    break;
                case Spell.Sonic: // rings of sound rolling out
                    Quad(Sprite.Ring, p.x, p.y, 1f, 1f, c);
                    var ring = Vector2.Lerp(from, to, Mathf.Max(0f, t - 0.3f));
                    Quad(Sprite.Ring, ring.x, ring.y, 1f, 1f, faint);
                    break;
                default: // Qi: a bolt of kiếm khí with a short trail
                    Quad(Sprite.QiShot, p.x, p.y, 1f, 1f, c, left);
                    var trail = Vector2.Lerp(from, to, Mathf.Max(0f, t - 0.25f));
                    Quad(Sprite.Spark, trail.x, trail.y, 1.2f, 1.2f, faint);
                    break;
            }
        }

        void Trail(Sprite s, Vector2 p, Vector2 from, Vector2 to, float t, Color32 c, float dt, float rate)
        {
            if (_rand.NextDouble() >= dt * rate) return;
            var q = Vector2.Lerp(from, to, Mathf.Max(0f, t - 0.1f));
            _particles.Add(new Particle { Sprite = s, X = q.x + Rand(-0.1f, 0.1f), Y = q.y + Rand(-0.1f, 0.1f), Life = 0.25f, Size = 1f, Color = c });
        }

        // Where a spell lands. power: 0 it missed (it scars the ground), 1 a hit, 2 the final blow.
        void Impact(Spell s, Vector2 at, Vector2 from, Color32 c, int power)
        {
            float x = at.x, y = at.y;
            int n = power == 2 ? 14 : power == 1 ? 7 : 3;
            float away = Mathf.Sign(at.x - from.x);
            switch (s)
            {
                case Spell.Claw: // three red slashes raked down
                    Burst(x, y, Claw, n, power == 2 ? 6f : 3.5f, power == 2);
                    for (int k = 0; k < 3; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = x - 0.3f + k * 0.3f, Y = y + 0.4f, VY = -3f, Life = 0.2f, Size = 1.3f, Color = Claw });
                    for (int k = 0; k < power; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Drop, X = x, Y = y, VX = Rand(-2f, 2f), VY = Rand(1f, 3f), Gravity = 12f, Life = 0.5f, Size = 1f,
                            Color = new Color32(200, 30, 30, 255) });
                    break;
                case Spell.Moc: // leaves scattered
                    for (int k = 0; k < n; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Leaf, X = x, Y = y, VX = Rand(-2.5f, 2.5f), VY = Rand(0.5f, 3f), Gravity = 4f,
                            Life = Rand(0.5f, 0.9f), Size = 1f, Color = new Color32(255, 255, 255, 255) });
                    break;
                case Spell.Thuy:
                case Spell.Tide:
                case Spell.Venom: // a splash
                    for (int k = 0; k < n + 3; k++)
                    {
                        float a = Rand(0.3f, Mathf.PI - 0.3f);
                        _particles.Add(new Particle { Sprite = Sprite.Drop, X = x + Rand(-0.3f, 0.3f), Y = y, VX = Mathf.Cos(a) * Rand(1f, 3f), VY = Mathf.Sin(a) * Rand(2f, 5f),
                            Gravity = 14f, Life = Rand(0.4f, 0.7f), Size = 1f, Color = c });
                    }
                    break;
                case Spell.Hoa:
                case Spell.Flame: // it bursts into flame, smoke rolling up
                    Burst(x, y, c, n, power == 2 ? 6f : 4f, true);
                    for (int k = 0; k < (power == 2 ? 4 : 2); k++)
                        _particles.Add(new Particle { Sprite = (Sprite)((int)Sprite.Fire0 + k % 3), X = x + Rand(-0.5f, 0.5f), Y = y - 0.4f, VY = Rand(0.3f, 0.8f),
                            Life = Rand(0.35f, 0.6f), Size = 1f, Color = new Color32(255, 255, 255, 255) });
                    break;
                case Spell.Tho: // the boulder shatters: chips and dust
                    for (int k = 0; k < n; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = x, Y = y, VX = Rand(-3f, 3f), VY = Rand(1f, 4f), Gravity = 14f,
                            Life = Rand(0.4f, 0.7f), Size = 1f, Color = new Color32(132, 96, 64, 255) });
                    for (int k = 0; k < 3; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.5f, 0.5f), Y = y - 0.3f, VX = Rand(-0.6f, 0.6f), VY = Rand(0.2f, 0.6f),
                            Life = Rand(0.5f, 0.9f), Size = 1f, Grow = 0.5f, Color = new Color32(160, 130, 96, 255) });
                    break;
                case Spell.Loi: // a crack of white, sparks everywhere
                    _particles.Add(new Particle { Sprite = Sprite.Flash, X = x, Y = y, Life = 0.12f, Size = 2f, Color = new Color32(255, 255, 255, 255) });
                    Burst(x, y, c, n + 4, 7f, false);
                    break;
                case Spell.Bang: // it shatters into frost
                    for (int k = 0; k < n; k++)
                        _particles.Add(new Particle { Sprite = k % 3 == 0 ? Sprite.Shard : Sprite.Spark, X = x, Y = y, VX = Rand(-3f, 3f), VY = Rand(0.5f, 3f), Gravity = 8f,
                            Life = Rand(0.35f, 0.6f), Size = 1f, Color = k % 2 == 0 ? c : new Color32(240, 252, 255, 255) });
                    break;
                case Spell.Phong:
                case Spell.Gale:
                case Spell.Sonic: // a shock ring, the air cut sideways
                    _particles.Add(new Particle { Sprite = Sprite.Ring, X = x, Y = y, Life = 0.25f, Size = 1f, Grow = 6f, Color = c });
                    for (int k = 0; k < n; k++)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = x, Y = y + Rand(-0.4f, 0.4f), VX = away * Rand(2f, 5f), VY = Rand(-0.5f, 0.5f),
                            Life = Rand(0.2f, 0.4f), Size = 1f, Color = c });
                    break;
                case Spell.Ma: // a dark blast, black smoke
                    Burst(x, y, c, n, 5f, false);
                    for (int k = 0; k < (power == 2 ? 4 : 2); k++)
                        _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.5f, 0.5f), Y = y, VX = Rand(-0.5f, 0.5f), VY = Rand(0.4f, 1f),
                            Life = Rand(0.5f, 0.9f), Size = 1f, Grow = 0.6f, Color = new Color32(54, 18, 40, 255) });
                    break;
                default: // Kim, Qi: sparks
                    Burst(x, y, c, n, power == 2 ? 6f : 3.5f, power == 2);
                    if (s == Spell.Kim) _particles.Add(new Particle { Sprite = Sprite.Flash, X = x, Y = y, Life = 0.1f, Size = 1f, Color = c });
                    break;
            }
            if (power == 0) // a miss leaves a scorch of dust where it hit the ground
                _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x, Y = y, VY = 0.4f, Life = 0.5f, Size = 1f, Grow = 1f, Color = new Color32(150, 126, 96, 255) });
            else
                FightScenes.Hurt.Add(new HurtZone { X = x, Y = y - 0.7f, R = power == 2 ? 1.6f : 0.9f, Start = Time.unscaledTime, Kill = 0f });
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
                case Fx.VoidRift:
                    e.Duration = 1f;
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
            WatchRifts(view);

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
            DrawSkirmishes(view, now);
            DrawShots(now, dt);
            DrawChases(view, now, dt);
            DrawLoot(view, now, dt);
            DrawPrayers(view, Time.unscaledDeltaTime);
            DrawRumor(view, now);
            foreach (var p in FightScenes.Poofs) Poof(p.x, p.y + 0.4f);
            FightScenes.Poofs.Clear();
            foreach (var p in FightScenes.Kills) Torn(p.x, p.y);
            FightScenes.Kills.Clear();
            foreach (var p in FightScenes.Dust) Dust(p.x, p.y, 1f);
            FightScenes.Dust.Clear();
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

                case Fx.VoidRift:
                {
                    // Opens thin, gapes, seals thin again; a flash as it opens, void motes drifting out while it gapes.
                    var tear = t < 0.12f || t > 0.82f ? Sprite.RiftThin : Sprite.Rift;
                    Quad(tear, e.X, e.Y + 1.4f, 1f, 1f, new Color32(255, 255, 255, 255));
                    if (t < 0.1f) Quad(Sprite.Flash, e.X, e.Y + 1.4f, 2f, 2f, new Color32(220, 190, 255, 220));
                    if (tear == Sprite.Rift && _rand.NextDouble() < 0.5)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = e.X + Rand(-0.6f, 0.6f), Y = e.Y + Rand(0.4f, 2.4f), VX = Rand(-1.2f, 1.2f), VY = Rand(-0.3f, 0.6f),
                            Life = Rand(0.3f, 0.6f), Size = 1f, Color = _rand.NextDouble() < 0.5 ? new Color32(170, 110, 255, 255) : new Color32(235, 225, 255, 255) });
                    break;
                }

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


        // ---------------------------------------------------------------- what the living are doing, on the map

        bool Position(int entity, out float x, out float y, out bool flying)
        {
            var e = _sim.Entities;
            float frac = _sim.TickFraction;
            x = Mathf.Lerp(e.PrevX[entity], e.X[entity], frac);
            y = Mathf.Lerp(e.PrevY[entity], e.Y[entity], frac);
            flying = e.Flying[entity] && (e.X[entity] != e.PrevX[entity] || e.Y[entity] != e.PrevY[entity]);
            return e.Species[entity] != Species.None;
        }

        bool Shown(Cultivator c) => c != null && c.Alive && c.Entity >= 0 && (_sim.Cultivation.IsShownOnMap(c) || c.Watched) && !FightScenes.Hides(c.Index);

        static Sprite LootSprite(Loot l)
        {
            switch (l)
            {
                case Loot.Treasure: return Sprite.LootSword;
                case Loot.Technique: return Sprite.LootSlip;
                case Loot.Pill: return Sprite.LootPill;
                case Loot.Stones: return Sprite.LootStones;
                case Loot.BeastCore: return Sprite.LootCore;
                case Loot.Herb: return Sprite.LootHerb;
                default: return Sprite.LootGem;
            }
        }

        static Color32 LootGlow(Loot l)
        {
            switch (l)
            {
                case Loot.Treasure: return new Color32(255, 230, 130, 255);
                case Loot.Technique: return new Color32(150, 255, 190, 255);
                case Loot.Pill: return new Color32(255, 200, 120, 255);
                case Loot.Stones: return new Color32(140, 255, 170, 255);
                case Loot.BeastCore: return new Color32(255, 110, 90, 255);
                case Loot.Herb: return new Color32(140, 240, 120, 255);
                default: return new Color32(150, 220, 255, 255);
            }
        }

        // Cơ duyên: whoever just won something walks off holding it up over their head, the prize bobbing a pixel,
        // a glow beating round it and motes rising off it; the moment they get it, a pillar of light.
        readonly Dictionary<int, long> _lootSeen = new Dictionary<int, long>();

        void DrawLoot(Rect view, float now, float dt)
        {
            long tick = _sim.Clock.Tick;
            foreach (var c in _sim.Cultivation.All)
            {
                bool loot = c.LootUntil > tick && c.Loot != Loot.None;
                if ((!loot && c.Errand == Errand.None) || !Shown(c)) continue;
                if (!Position(c.Entity, out float x, out float y, out bool flying) || !view.Contains(new Vector2(x, y))) continue;
                if (!loot)
                {
                    DrawErrand(c, x, y + (flying ? 0.7f : 0f), now, dt);
                    continue;
                }
                if (!_lootSeen.TryGetValue(c.Index, out long until) || until != c.LootUntil)
                {
                    _lootSeen[c.Index] = c.LootUntil;
                    if (_effects.Count < MaxEffects) Spawn(Fx.LightPillar, x, y);
                    Spawn(Fx.Blessing, x, y + 1f);
                }
                float hx = x, hy = y + (flying ? 0.7f : 0f) + 2.2f + ((((int)(now * 3f)) + c.Index) & 1) / CellPx;
                var glow = LootGlow(c.Loot);
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f + c.Index);
                Quad(Sprite.Flash, hx, hy, 2f, 2f, new Color32(glow.r, glow.g, glow.b, (byte)(100 + 100 * pulse)));
                if (pulse > 0.5f) Quad(Sprite.Ring, hx, hy, 1f, 1f, new Color32(glow.r, glow.g, glow.b, 200));
                Quad(LootSprite(c.Loot), hx, hy, 1f, 1f, new Color32(255, 255, 255, 255));
                if (_rand.NextDouble() < dt * 8f)
                    _particles.Add(new Particle { Sprite = Sprite.Spark, X = hx + Rand(-0.6f, 0.6f), Y = hy + Rand(-0.3f, 0.3f), VY = Rand(0.6f, 1.4f),
                        Life = Rand(0.5f, 0.9f), Size = 1f, Color = glow });
            }
        }

        // What they are out doing, as a small sign over their head; and at the spot, the work itself: kneeling among the
        // herbs (green motes rising off the ground), sitting with the Dao (motes of their realm's colour drawn up round
        // them), haggling in the market (a glint of linh thạch).
        void DrawErrand(Cultivator c, float x, float y, float now, float dt)
        {
            Sprite icon;
            switch (c.Errand)
            {
                case Errand.Herbs: icon = Sprite.IconHerb; break;
                case Errand.Hunt: icon = Sprite.IconHunt; break;
                case Errand.Market: icon = Sprite.IconCoin; break;
                case Errand.Patrol: icon = Sprite.IconFlag; break;
                case Errand.Ponder: icon = Sprite.IconLotus; break;
                default: icon = Sprite.IconHome; break;
            }
            Quad(icon, x, y + 2.1f, 1f, 1f, new Color32(255, 255, 255, 255));
            bool atWork = c.Away && !c.Travelling;
            if (!atWork) return;
            switch (c.Errand)
            {
                case Errand.Herbs:
                    if (_rand.NextDouble() < dt * 6f)
                        _particles.Add(new Particle { Sprite = _rand.NextDouble() < 0.3 ? Sprite.Leaf : Sprite.Spark, X = x + Rand(-0.8f, 0.8f), Y = y + Rand(-0.2f, 0.2f),
                            VY = Rand(0.4f, 0.9f), Life = Rand(0.5f, 0.9f), Size = 1f, Color = new Color32(150, 240, 120, 255) });
                    break;
                case Errand.Ponder:
                {
                    var tint = KiemKhi[Mathf.Clamp((int)c.Realm, 0, KiemKhi.Length - 1)];
                    if (_rand.NextDouble() < dt * 8f)
                    {
                        float a = Rand(0f, Mathf.PI * 2f);
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = x + Mathf.Cos(a) * 1.4f, Y = y + 0.6f + Mathf.Sin(a) * 0.7f,
                            VX = -Mathf.Cos(a) * 1.2f, VY = 0.8f, Life = 0.9f, Size = 1f, Color = tint });
                    }
                    float k = (now * 0.5f + c.Index * 0.37f) % 1f;
                    Quad(Sprite.Ring, x, y + 0.3f, 1f + k * 2f, 1f + k, new Color32(tint.r, tint.g, tint.b, (byte)(160 * (1f - k))));
                    break;
                }
                case Errand.Market:
                    if (_rand.NextDouble() < dt * 2f)
                        _particles.Add(new Particle { Sprite = Sprite.Spark, X = x + Rand(-0.4f, 0.4f), Y = y + 1f, VY = Rand(0.6f, 1.2f), Gravity = 3f,
                            Life = 0.6f, Size = 1f, Color = new Color32(140, 255, 200, 255) });
                    break;
            }
        }

        // A beast on the hunt wears a red "!" (the one who hunts does, if a cultivator hunts the beast); whoever runs
        // kicks up dust. When the two close in and are a match, they trade spells on the run.
        void DrawChases(Rect view, float now, float dt)
        {
            var e = _sim.Entities;
            foreach (var b in _sim.Beasts.All)
            {
                if (!b.Alive || b.ChaseEntity < 0 || b.ChaseEntity >= e.Count) continue;
                if (!Position(b.Entity, out float bx, out float by, out _) || !Position(b.ChaseEntity, out float qx, out float qy, out _)) continue;
                bool seeB = view.Contains(new Vector2(bx, by)), seeQ = view.Contains(new Vector2(qx, qy));
                if (!seeB && !seeQ) continue;
                var c = e.Species[b.ChaseEntity] == Species.Cultivator ? _sim.Cultivation.ForEntity(b.ChaseEntity) : null;
                if (c != null && !Shown(c)) seeQ = false;
                float body = SpriteLibrary.BeastScale(b.Grade, b.Rampage);
                bool blink = (((int)(now * 4f)) & 1) == 0;
                var red = new Color32(255, 255, 255, 255);
                if (b.ChaseMode == BeastSystem.ChaseHunted)
                {
                    if (seeQ && blink) Quad(Sprite.Exclaim, qx, qy + 2.1f, 1f, 1f, red);
                    if (seeB) Dust(bx, by, dt);
                }
                else
                {
                    if (seeB && blink) Quad(Sprite.Exclaim, bx, by + 1.2f + 1.3f * body, 1f, 1f, red);
                    if (b.ChaseMode == BeastSystem.ChaseClash && seeQ && !blink) Quad(Sprite.Exclaim, qx, qy + 2.1f, 1f, 1f, red);
                    if (seeQ && b.ChaseMode == BeastSystem.ChaseHunts) Dust(qx, qy, dt);
                }
                if (c == null || b.ChaseMode == BeastSystem.ChaseHunts || !seeB || !seeQ) continue;
                float dx = qx - bx, dy = qy - by;
                if (dx * dx + dy * dy > 49f || _rand.NextDouble() >= dt * 1.5f) continue;
                // Spells traded while they close: the cultivator's method against the beast's own.
                var from = new Vector2(qx, qy + 0.7f);
                var to = new Vector2(bx, by + 0.6f * body);
                if (_rand.NextDouble() < 0.6) Shoot(SpellOf(c), from, to, ColorOf(SpellOf(c), c));
                else
                {
                    var bs = SpellOf(b);
                    if (bs != Spell.Claw) Shoot(bs, to, from, ColorOf(bs, null));
                }
            }
        }

        void Dust(float x, float y, float dt)
        {
            if (_rand.NextDouble() >= dt * 6f) return;
            _particles.Add(new Particle { Sprite = Sprite.Smoke, X = x + Rand(-0.3f, 0.3f), Y = y + 0.1f, VX = Rand(-0.3f, 0.3f), VY = Rand(0.2f, 0.5f),
                Life = Rand(0.35f, 0.6f), Size = 1f, Color = new Color32(176, 150, 112, 255) });
        }

        // Loose spells: the skirmishes round a contested bí cảnh and the running fights of a chase. Unlike the fight
        // scenes these settle nothing; the sim does that. They show the fighting while it goes on.
        struct Shot
        {
            public Spell Spell;
            public Vector2 From, To;
            public float At;
            public Color32 Color;
        }

        const int MaxShots = 48;
        readonly List<Shot> _shots = new List<Shot>();

        void Shoot(Spell s, Vector2 from, Vector2 to, Color32 color)
        {
            if (_shots.Count >= MaxShots) return;
            if (_rand.NextDouble() < 0.3) to += (to - from).normalized * 1.5f + new Vector2(0f, -0.5f); // a miss
            _shots.Add(new Shot { Spell = s, From = from, To = to, At = Time.unscaledTime + 0.35f, Color = color });
        }

        void DrawShots(float now, float dt)
        {
            for (int i = _shots.Count - 1; i >= 0; i--)
            {
                var s = _shots[i];
                float t = (now - s.At) / FightScene.Flight;
                if (t < 0f) { if (s.Spell != Spell.Claw) Charge(s.From, s.To, s.Color, -t * FightScene.Flight); continue; }
                if (t < 1f) { DrawSpell(s.Spell, s.From, s.To, t, s.Color, now, dt); continue; }
                Impact(s.Spell, s.To, s.From, s.Color, 1);
                _shots.RemoveAt(i);
            }
        }

        // A contest over a bí cảnh: the sides' people round it hurl spells at each other across the field.
        readonly List<(int who, int side)> _contestants = new List<(int, int)>();
        float _nextSkirmish;

        void DrawSkirmishes(Rect view, float now)
        {
            if (now < _nextSkirmish) return;
            _nextSkirmish = now + 0.25f;
            var all = _sim.Cultivation.All;
            foreach (var r in _sim.Relics.All)
            {
                if (!r.Open || !_sim.Relics.Contested(r) || !view.Contains(new Vector2(r.X, r.Y))) continue;
                _sim.Relics.ContestantsOf(r, _contestants);
                if (_contestants.Count < 2) continue;
                for (int tries = 0; tries < 3; tries++)
                {
                    var (ai, aside) = _contestants[_rand.Next(_contestants.Count)];
                    var (bi, bside) = _contestants[_rand.Next(_contestants.Count)];
                    if (aside == bside || ai < 0 || bi < 0 || ai >= all.Count || bi >= all.Count) continue;
                    Cultivator a = all[ai], b = all[bi];
                    if (!Shown(a) || !Shown(b)) continue;
                    if (!Position(a.Entity, out float ax, out float ay, out _) || !Position(b.Entity, out float bx, out float by, out _)) continue;
                    float dx = bx - ax, dy = by - ay;
                    if (dx * dx + dy * dy > 144f) continue; // within twelve cells of each other
                    Shoot(SpellOf(a), new Vector2(ax, ay + 0.7f), new Vector2(bx, by + 0.7f), ColorOf(SpellOf(a), a));
                    break;
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

            // Hư không liệt phùng: a jagged black gash in the air, edged in violet with pale flecks, widest in the
            // middle. The thin one is the same tear just opening or sealing.
            Color32[] Tear(int w, int h, float width)
            {
                var b = new Color32[w * h];
                var core = new Color32(14, 6, 24, 255);
                var edge = new Color32(146, 84, 226, 255);
                var rim = new Color32(226, 206, 255, 255);
                int mid = w / 2;
                for (int y = 0; y < h; y++)
                {
                    int x0 = mid + ((y / 3) % 3 == 1 ? 1 : (y / 3) % 3 == 2 ? -1 : 0); // a zigzag, three pixels a step
                    int half = Mathf.RoundToInt(width * Mathf.Sin(Mathf.PI * (y + 0.5f) / h));
                    for (int x = 0; x < w; x++)
                    {
                        int d = Mathf.Abs(x - x0);
                        if (d < half) b[y * w + x] = core;
                        else if (d == half) b[y * w + x] = edge;
                        else if (d == half + 1 && (x + y) % 3 == 0) b[y * w + x] = rim;
                    }
                }
                return b;
            }
            var rift = Tear(11, 26, 2.6f);
            var riftThin = Tear(7, 20, 0.6f);
            Put(Sprite.Rift, 11, 26, (x, y) => rift[y * 11 + x]);
            Put(Sprite.RiftThin, 7, 20, (x, y) => riftThin[y * 7 + x]);

            // Hand-drawn pieces, a character per pixel ('.' empty), top row first. White and grey ('w', 'l', 's') take
            // the tint of the spell; the rest keep their colour.
            var ink = new Dictionary<char, Color32>
            {
                ['w'] = white, ['l'] = light, ['s'] = shade, ['k'] = new Color32(24, 18, 22, 255),
                ['r'] = new Color32(222, 44, 44, 255), ['o'] = new Color32(255, 150, 50, 255), ['y'] = new Color32(255, 224, 96, 255),
                ['g'] = new Color32(96, 196, 104, 255), ['G'] = new Color32(52, 132, 72, 255), ['c'] = new Color32(150, 236, 255, 255),
                ['b'] = new Color32(70, 130, 220, 255), ['n'] = new Color32(132, 96, 64, 255), ['N'] = new Color32(92, 66, 44, 255),
                ['p'] = new Color32(186, 120, 250, 255), ['m'] = new Color32(196, 210, 226, 255),
            };
            void Art(Sprite s, params string[] rows)
            {
                int h = rows.Length, w = rows[0].Length;
                Put(s, w, h, (x, y) => x < rows[h - 1 - y].Length && ink.TryGetValue(rows[h - 1 - y][x], out var c) ? c : default);
            }
            Art(Sprite.Orb, ".sls.", "slwls", "lwwwl", "slwls", ".sls.");
            Art(Sprite.Rock, ".nN..", "nnnN.", "nNnnN", ".nnN.", "..N..");
            Art(Sprite.Shard, "....ll..", "swwwwwwl", "....ll..");
            Art(Sprite.Crescent, "..ll...", ".l.....", "l......", "l......", "l......", ".l.....", "..ll...");
            Art(Sprite.Blade, "y.......", "ylwwwwwl", "y.......");
            Art(Sprite.Leaf, ".g", "gG");
            Art(Sprite.Exclaim, "krk", "krk", "krk", "krk", ".k.", "krk", "krk");
            Art(Sprite.LootSword, ".......wk", "......wlk", ".....wlk.", "....wlk..", "y..wlk...", ".yylk....", "..yy.....", ".k.y.....", "k........");
            Art(Sprite.LootSlip, ".kkkkkkk.", "kgggggcgk", "kgGgggggk", "kkkkkkkkk", "rrrrrrrrr", "kgggggcgk", "kgGgggggk", ".kkkkkkk.");
            Art(Sprite.LootPill, "...kk....", "..knnk...", "..kNnk...", ".knnnnk..", "knnnnnnk.", "knnyynnk.", "knnyynnk.", ".knnnnk..", "..kkkk...");
            Art(Sprite.LootGem, "....w....", "...wcw...", "..wccbw..", ".wccbbbw.", "wccbbbbbw", ".wcbbbbw.", "..wbbbw..", "...wbw...", "....w....");
            Art(Sprite.LootStones, "...c.....", "..cgc..c.", "..gGg.cgc", ".cgGgcgGg", ".gGGggGGg", "kkkkkkkkk");
            Art(Sprite.LootCore, "..kkkk...", ".korrok..", "kowyrrrk.", "kryrrrrk.", "krrrrrrk.", ".krrrrk..", "..kkkk...");
            Art(Sprite.LootHerb, "...r...", "..rrr..", "...g...", ".g.g.g.", "ggGgGgg", ".gGgGg.", "..gGg..", "...G...", "..nnn..");
            Art(Sprite.IconHerb, "..g..", ".ggg.", "g.G.g", "..G..", ".nnn.");
            Art(Sprite.IconHunt, "....w", "...w.", "y.w..", ".y...", "y.y..");
            Art(Sprite.IconCoin, ".ccc.", "cwccb", "ccccb", ".cbb.", "..b..");
            Art(Sprite.IconFlag, "krrr.", "krrrr", "krrr.", "k....", "k....");
            Art(Sprite.IconLotus, "..p..", ".ppp.", "pp.pp", ".ppp.", "..g..");
            Art(Sprite.IconHome, "..r..", ".rrr.", "rrrrr", ".nwn.", ".nkn.");

            atlas = new Texture2D(tw, th, TextureFormat.RGBA32, false) { name = "FxAtlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            atlas.SetPixelData(px, 0);
            atlas.Apply(false);
        }
    }
}
