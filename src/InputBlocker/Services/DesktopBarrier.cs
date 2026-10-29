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
        // IsVisible only means an HWND exists; it does not mean the password UI was drawn.
        var prepared = overlays.ToArray();
        await Task.WhenAll(prepared.Select(w => w.Rendered)).WaitAsync(TimeSpan.FromSeconds(3));
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (overlays.Count != screens.Length || prepared.Any(w => !w.IsReady || w.Handle == 0))
            throw new InvalidOperationException("The unlock screen did not finish rendering. Input remains available.");
        if (!prepared.Single(w => w.IsPrimary).FocusUnlock())
            throw new InvalidOperationException("The unlock password field could not receive focus. Input remains available.");
    }
    public void InstallHooks()
    {
        if (overlays.Count == 0 || overlays.Any(w => !w.IsReady))
            throw new InvalidOperationException("Cannot block input without rendered overlays.");
        hooks.Emergency -= Emergency; hooks.Emergency += Emergency;
        hooks.RevealRequested -= RevealRequested; hooks.RevealRequested += RevealRequested;
        hooks.Install(overlays.Select(w => w.Handle).ToArray());
    }
    private void RevealRequested() => Application.Current.Dispatcher.BeginInvoke(RevealUnlock, DispatcherPriority.Send);
    internal void RevealUnlock()
    {
        // A queued request after recovery must not recreate any windows.
        if (overlays.Count == 0) return;
        try
        {
            var primary = overlays.Single(w => w.IsPrimary);
            if (primary.IsUnlockVisible) return;
            if (!primary.ShowUnlockScreen()) { recover(); return; }
            hooks.AllowUnlockInput();
        }
        catch { recover(); }
    }
    private void Emergency() => Application.Current.Dispatcher.BeginInvoke(recover, DispatcherPriority.Send);
    public void ReleaseHooks() => hooks.Dispose();
    public void CloseOverlays() { foreach (var window in overlays.ToArray()) window.Release(); overlays.Clear(); }
}
