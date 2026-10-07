using Silk.NET.SDL;

namespace Defender.Infrastructure;

/// <summary>Lazily loads the native SDL2 library once; null (with an error) if it cannot be loaded.</summary>
public static class SdlProvider
{
    private static readonly Lazy<(Sdl? api, string? error)> s_api = new(() =>
    {
        try
        {
            var sdl = Sdl.GetApi();
            // The window belongs to Avalonia, so SDL must deliver controller input without its own focused window.
            sdl.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
            return (sdl, null);
        }
        catch (Exception ex)
        {
            return (null, ex.GetType().Name + ": " + ex.Message);
        }
    });

    public static Sdl? Api => s_api.Value.api;
    public static string? Error => s_api.Value.error;
}
