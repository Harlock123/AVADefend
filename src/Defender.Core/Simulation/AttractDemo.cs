namespace Defender.Core.Simulation;

/// <summary>
/// The scripted instructions/scoring demonstration (LEDRET, amode1.src:477-673), reproduced as behaviour with our
/// own artwork: a lander abducts the humanoid, the ship shoots it, catches the falling humanoid (500) and sets it
/// down, then each enemy appears, rises into the beam, explodes and re-appears in its display slot with its label.
/// Screen coordinates throughout (world BGL = 0, no scrolling). Timings are the source's NAP values.
/// </summary>
public sealed class AttractDemo
{
    private sealed class Actor
    {
        public Sprite Sprite = Sprites.Lander[0];
        public double X, Y, Vx, Vy;
        public int Appear;          // >0: materialising (size = Appear − 1)
        public bool Visible = true;
        public bool Facing = true;  // ship: true = right
    }

    private sealed class DemoBlast { public Sprite Sprite = Sprites.Pod; public int X, Y, CX, CY, Size = 0x100; }

    public static readonly (EnemyKind Kind, int X, int Y, string Name, string Points, int Indent)[] Roster =
    [
        (EnemyKind.Lander, 72, 96, "LANDER", "150", 12), (EnemyKind.Mutant, 136, 96, "MUTANT", "150", 12),
        (EnemyKind.Baiter, 204, 98, "BAITER", "200", 12), (EnemyKind.Bomber, 75, 152, "BOMBER", "250", 12),
        (EnemyKind.Pod, 139, 152, "POD", "1000", 0), (EnemyKind.Swarmer, 207, 154, "SWARMER", "150", 16),
    ];
    private static readonly (int X, int Y)[] LabelPos = [(56, 112), (120, 112), (190, 112), (56, 168), (128, 168), (184, 168)];

    public const int RescueFrames = 230 + 160 + 21 + 90 + 80 + 96;          // 677
    public const int RosterStepFrames = 95 + 23 + 32 + 32;                  // 182 per enemy
    public const int TotalFrames = RescueFrames + 6 * RosterStepFrames + 255 + 255;   // ≈ 2279

    private readonly Actor _ship = new() { Sprite = Sprites.ShipRight, X = 64, Y = 80 };
    private readonly Actor _human = new() { Sprite = Sprites.Humanoid, X = 240, Y = 219 };
    private readonly Actor _lander = new() { Sprite = Sprites.Lander[0], X = 237, Y = 64, Vy = 0.625, Appear = 48 };
    private Actor? _enemy;
    private readonly List<Actor> _slots = new();
    private readonly List<DemoBlast> _blasts = new();
    private Laser? _laser;
    private (string Text, int X, int Y)? _popup;
    private int _labelsShown;
    private bool _humanFalling, _carried;
    private int _fallFrames;

    public int Frame { get; private set; }
    public bool Finished => Frame >= TotalFrames;

    public void Step()
    {
        int t = Frame++;
        // ---- rescue sequence ----
        if (t == 230) { _lander.Vy = -0.6875; _human.Vy = -0.6875; }
        if (t == 390) Fire();
        if (t == 411)
        {
            _laser = null;
            Explode(_lander);
            _humanFalling = true; _human.Vy = 0;
            _ship.Vx = 2; _ship.Vy = 0.83;
        }
        if (t == 501)
        {
            _humanFalling = false; _carried = true;
            _popup = ("500", 255, 144);
            _ship.Vx = 0; _ship.Vy = 0.75;
        }
        if (t == 581)
        {
            _carried = false; _human.Vy = 0;
            _popup = ("500", 224, 224);
            _ship.Facing = false; _ship.Sprite = Sprites.ShipLeft; _ship.Vx = -2; _ship.Vy = -1.5;
        }
        if (t == 677) { _ship.Facing = true; _ship.Sprite = Sprites.ShipRight; _ship.Vx = _ship.Vy = 0; _popup = null; }

        // ---- enemy roster ----
        if (t >= RescueFrames && t < RescueFrames + 6 * RosterStepFrames)
        {
            int k = (t - RescueFrames) / RosterStepFrames, u = (t - RescueFrames) % RosterStepFrames;
            var r = Roster[k];
            if (u == 0) _enemy = new Actor { Sprite = SpriteOf(r.Kind), X = 248, Y = 160, Vy = -0.75, Appear = 48 };
            if (u == 95) Fire();
            if (u == 95 + 23)
            {
                _laser = null;
                if (_enemy is not null) Explode(_enemy);
                _enemy = null;
                _slots.Add(new Actor { Sprite = SpriteOf(r.Kind), X = r.X, Y = r.Y, Appear = 48 });
            }
            if (u == 95 + 23 + 32) _labelsShown = k + 1;
        }

        // ---- motion ----
        Move(_ship);
        if (_lander.Visible) Move(_lander);
        if (_enemy is not null) Move(_enemy);
        foreach (var s in _slots) if (s.Appear > 0) s.Appear--;
        if (_humanFalling)
        {
            if (++_fallFrames % 2 == 0) _human.Vy += 8 / 256.0;
            _human.Y += _human.Vy;
        }
        else if (_carried) { _human.X = _ship.X + 4; _human.Y = _ship.Y + 10; }
        else if (!_lander.Visible || t < 230) { }
        else _human.Y += _human.Vy;
        if (_laser is { } l) { l.Head += 8; l.Fizzle += 6; l.Tail += 2; if (l.Head > Arcade.ScreenWidth) l.Head = Arcade.ScreenWidth; }
        foreach (var b in _blasts) b.Size += 0xAA;
        _blasts.RemoveAll(b => (b.Size >> 8) > 0x30);
    }

    private static Sprite SpriteOf(EnemyKind k) => k switch
    {
        EnemyKind.Lander => Sprites.Lander[0], EnemyKind.Mutant => Sprites.Mutant, EnemyKind.Baiter => Sprites.Baiter,
        EnemyKind.Bomber => Sprites.Bomber, EnemyKind.Pod => Sprites.Pod, _ => Sprites.Swarmer,
    };

    private static void Move(Actor a)
    {
        if (a.Appear > 0) a.Appear--;
        a.X += a.Vx; a.Y += a.Vy;
    }

    private void Fire()
    {
        int origin = (int)_ship.X + 14;
        _laser = new Laser { Origin = origin, Head = origin, Fizzle = origin, Tail = origin, Y = (int)_ship.Y + 4, Dir = 1 };
    }

    private void Explode(Actor a)
    {
        a.Visible = false;
        int top = (int)a.X >> 1;
        _blasts.Add(new DemoBlast { Sprite = a.Sprite, X = top, Y = (int)a.Y, CX = top + (a.Sprite.Width + 1) / 4, CY = (int)a.Y + a.Sprite.Height / 2 });
    }

    /// <summary>Fills the playfield part of a snapshot (terrain, sprites, laser, effects, labels).</summary>
    public void Build(FrameSnapshot s, Terrain terrain)
    {
        for (int x = 0; x < Arcade.ScreenWidth; x++) s.TerrainY[x] = terrain.HeightAtUnits(x * Arcade.UnitsPerPixel);
        s.Sprites.Clear(); s.Blasts.Clear(); s.Lasers.Clear(); s.Popups.Clear(); s.Particles.Clear(); s.Stars.Clear(); s.Labels.Clear();
        void Draw(Actor a)
        {
            if (!a.Visible) return;
            if (a.Appear > 0)
            {
                int top = (int)a.X >> 1;
                s.Blasts.Add(new BlastDraw(a.Sprite, top, (int)a.Y, top + (a.Sprite.Width + 1) / 4, (int)a.Y + a.Sprite.Height / 2, a.Appear - 1));
            }
            else s.Sprites.Add(new SpriteDraw(a.Sprite, (int)a.X, (int)a.Y));
        }
        Draw(_human); Draw(_lander); Draw(_ship);
        if (_enemy is not null) Draw(_enemy);
        foreach (var a in _slots) Draw(a);
        foreach (var b in _blasts) s.Blasts.Add(new BlastDraw(b.Sprite, b.X, b.Y, b.CX, b.CY, b.Size >> 8));
        if (_laser is { } l) s.Lasers.Add(new LaserDraw(l.Tail, l.Fizzle, l.Head, l.Y, l.Dir));
        if (_popup is { } p) s.Popups.Add(new TextDraw(p.X, p.Y, p.Text, Pal.White));
        s.Labels.Add(new TextDraw(134, 48, "SCANNER", Pal.Laser));
        for (int i = 0; i < _labelsShown; i++)
        {
            var r = Roster[i];
            s.Labels.Add(new TextDraw(LabelPos[i].X, LabelPos[i].Y, r.Name, Pal.Laser));
            s.Labels.Add(new TextDraw(LabelPos[i].X + r.Indent, LabelPos[i].Y + 10, r.Points, Pal.Laser));
        }
    }
}
