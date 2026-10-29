using System.Windows.Threading;
using InputBlocker.Views;
namespace InputBlocker.Services;
internal sealed class DesktopBarrier(Func<string, Task<string?>> unlock, Action recover) : IBarrier
{
    private readonly List<LockOverlayWindow> overlays = [];
    private readonly HookService hooks = new();
    public async Task PrepareAsync()
    {
        var screens = System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.Primary).ToArray();
        if (screens.Length == 0) throw new InvalidOperationException("No active displays.");
        foreach (var screen in screens)
        {
            var overlay = new LockOverlayWindow(screen.Primary, unlock);
            overlays.Add(overlay); overlay.Show(); overlay.Place(screen.Bounds);
        }
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (overlays.Count != screens.Length || overlays.Any(w => !w.IsVisible || w.Handle == 0)) throw new InvalidOperationException("Overlay initialization incomplete.");
    }
    public void InstallHooks()
    {
        hooks.Emergency -= Emergency; hooks.Emergency += Emergency;
        hooks.Install(overlays.Select(w => w.Handle).ToArray());
    }
    private void Emergency() => Application.Current.Dispatcher.BeginInvoke(recover, DispatcherPriority.Send);
    public void ReleaseHooks() => hooks.Dispose();
    public void CloseOverlays() { foreach (var window in overlays.ToArray()) window.Release(); overlays.Clear(); }
}
