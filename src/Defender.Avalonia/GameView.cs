using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Defender.Avalonia.Rendering;
using Defender.Core;

namespace Defender.Avalonia;

/// <summary>
/// Single control that presents the game: one WriteableBitmap at the arcade's 292×240 visible
/// resolution, scaled to fit with preserved aspect ratio. No per-entity controls.
/// </summary>
public sealed class GameView : Control
{
    private readonly WriteableBitmap _bitmap = new(new PixelSize(SoftwareRenderer.Width, SoftwareRenderer.Height),
        new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
    private TimeSpan? _last;
    private bool _attached;

    public GameView(GameHost host)
    {
        Host = host;
        Renderer = new SoftwareRenderer
        {
            HighScoreProvider = () => Host.Session.HighScores,
        };
        ClipToBounds = true;
    }

    public GameHost Host { get; }
    public SoftwareRenderer Renderer { get; }
    public long FramesPresented { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnFrame(TimeSpan now)
    {
        if (!_attached) return;
        var dt = _last is { } l ? now - l : TimeSpan.Zero;
        _last = now;
        try
        {
            Host.Frame(dt);
            RenderFrame();
        }
        finally
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    /// <summary>Runs the software renderer and uploads to the bitmap (also used by headless tests).</summary>
    public unsafe void RenderFrame()
    {
        var s = Host.Settings;
        Renderer.ShowControlHints = s.ShowControlHints;
        Renderer.BoldScanner = s.BoldScanner;
        Renderer.GameSpeed = Host.Scheduler.TimeScale;
        Renderer.StatusLine = Host.Messages.LastOrDefault();
        Renderer.Render(Host.Snapshot);
        var snap = Host.Snapshot;
        // Cocktail table: player two sits opposite, so their turns are drawn rotated 180°.
        if (s.CocktailFlip && snap.PlayerCount == 2 && snap.CurrentPlayer == 1 && !snap.Demo) Array.Reverse(Renderer.Pixels);
        using (var fb = _bitmap.Lock())
        {
            fixed (uint* src = Renderer.Pixels)
                for (int y = 0; y < SoftwareRenderer.Height; y++)
                    Buffer.MemoryCopy(src + y * SoftwareRenderer.Width, (byte*)fb.Address + y * fb.RowBytes,
                        fb.RowBytes, SoftwareRenderer.Width * 4);
        }
        FramesPresented++;
        InvalidateVisual();
    }

    public Rect DestinationRect(Size bounds)
    {
        double scale = Math.Min(bounds.Width / SoftwareRenderer.Width, bounds.Height / SoftwareRenderer.Height);
        if (Host.Settings.IntegerScaling && scale >= 1)
        {
            // Snap to whole device pixels so every arcade pixel is the same size (HiDPI-aware).
            double dpi = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            scale = Math.Floor(scale * dpi) / dpi;
        }
        var size = new Size(SoftwareRenderer.Width * scale, SoftwareRenderer.Height * scale);
        return new Rect(new Point((bounds.Width - size.Width) / 2, (bounds.Height - size.Height) / 2), size);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
        bool smooth = Host.Settings.Mode == GameMode.Modern && Host.Settings.SmoothScaling;
        using (context.PushRenderOptions(new RenderOptions
               {
                   BitmapInterpolationMode = smooth ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None,
               }))
        {
            context.DrawImage(_bitmap, new Rect(0, 0, SoftwareRenderer.Width, SoftwareRenderer.Height), DestinationRect(Bounds.Size));
        }
    }
}
