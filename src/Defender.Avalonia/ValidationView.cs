using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Defender.Core.Simulation;
using SkiaSharp;

namespace Defender.Avalonia;

/// <summary>
/// Milestone-2 pipeline check: fixed 60 Hz simulation driving a scrolling starfield and terrain,
/// rendered by SkiaSharp into a 292x240 WriteableBitmap, scaled nearest-neighbour to the window.
/// </summary>
public sealed class ValidationView : Control
{
    private const int W = 292, H = 240, WorldWidth = 2048;
    private readonly WriteableBitmap _fb = new(new PixelSize(W, H), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
    private readonly FixedStepScheduler _sched = new(60);
    private readonly (int x, int y)[] _stars;
    private readonly int[] _terrain = new int[WorldWidth];
    private TimeSpan? _last;
    private int _cameraX, _tick;

    public ValidationView()
    {
        var rng = new Random(1981);
        _stars = Enumerable.Range(0, 40).Select(_ => (rng.Next(WorldWidth), rng.Next(20, 200))).ToArray();
        int h = 220;
        for (int x = 0; x < WorldWidth; x++) { h = Math.Clamp(h + rng.Next(-1, 2), 200, 236); _terrain[x] = h; }
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        var dt = _last is { } l ? now - l : TimeSpan.Zero;
        _last = now;
        int ticks = _sched.Advance(dt);
        for (int i = 0; i < ticks; i++) { _tick++; _cameraX = WorldMath.Wrap(_cameraX + 3, WorldWidth); }
        Draw();
        InvalidateVisual();
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    private void Draw()
    {
        using var fb = _fb.Lock();
        var info = new SKImageInfo(W, H, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info, fb.Address, fb.RowBytes);
        var c = surface.Canvas;
        c.Clear(SKColors.Black);
        using var p = new SKPaint { IsAntialias = false };
        // Stars at half parallax rate (placeholder until research confirms behaviour).
        p.Color = SKColors.White;
        foreach (var (sx, sy) in _stars)
        {
            int x = WorldMath.Wrap(sx - _cameraX / 2, WorldWidth);
            if (x < W) c.DrawPoint(x, sy, p);
        }
        p.Color = new SKColor(0xC0, 0x60, 0x00);
        for (int x = 0; x < W; x++) c.DrawPoint(x, _terrain[WorldMath.Wrap(_cameraX + x, WorldWidth)], p);
        p.Color = SKColors.Blue; p.Style = SKPaintStyle.Stroke;
        c.DrawRect(0, 0, W - 1, 30, p);
        p.Style = SKPaintStyle.Fill; p.Color = SKColors.Yellow;
        c.DrawRect((_tick / 2) % W, 100, 10, 4, p);
    }

    public override void Render(DrawingContext context)
    {
        double scale = Math.Min(Bounds.Width / W, Bounds.Height / H);
        var size = new Size(W * scale, H * scale);
        var dest = new Rect(new Point((Bounds.Width - size.Width) / 2, (Bounds.Height - size.Height) / 2), size);
        context.DrawImage(_fb, new Rect(0, 0, W, H), dest);
    }
}
