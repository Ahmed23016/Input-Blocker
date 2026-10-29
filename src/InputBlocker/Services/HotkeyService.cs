using System.Windows.Interop;
using InputBlocker.Infrastructure;
namespace InputBlocker.Services;
internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource source;
    private readonly Action activate, recover;
    public HotkeyService(Action activate, Action recover)
    {
        this.activate = activate; this.recover = recover;
        source = new HwndSource(new HwndSourceParameters("Input Blocker hotkeys") { ParentWindow = new nint(-3) });
        source.AddHook(WndProc);
        if (!NativeMethods.RegisterHotKey(source.Handle, 2, 3 | 0x4000, 0x7B)) { source.Dispose(); throw new InvalidOperationException("Recovery shortcut Ctrl+Alt+F12 is unavailable. Close the conflicting application and restart."); }
    }
    public bool Configure(AppSettings settings)
    {
        NativeMethods.UnregisterHotKey(source.Handle, 1);
        return !settings.EnableShortcut || NativeMethods.RegisterHotKey(source.Handle, 1, settings.ShortcutModifiers | 0x4000, settings.ShortcutKey);
    }
    private nint WndProc(nint hwnd, int msg, nint wp, nint lp, ref bool handled)
    {
        if (msg == 0x0312) { handled = true; if (wp == 2) recover(); else if (wp == 1) activate(); }
        return 0;
    }
    public void Dispose() { NativeMethods.UnregisterHotKey(source.Handle, 1); NativeMethods.UnregisterHotKey(source.Handle, 2); source.Dispose(); }
}
