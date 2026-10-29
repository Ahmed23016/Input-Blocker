using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using InputBlocker.Infrastructure;
namespace InputBlocker.Services;
// Hooks live on their own message-pump thread, never on the WPF rendering/authentication thread.
internal sealed class HookService : IDisposable
{
    private readonly NativeMethods.HookProc keyboardCallback, mouseCallback;
    private nint keyboard, mouse;
    private Dispatcher? dispatcher;
    private Thread? thread;
    private nint[] windows = [];
    private bool ctrl, alt, shift;
    public event Action? Emergency;
    public HookService() { keyboardCallback = Keyboard; mouseCallback = Mouse; }
    public void Install(nint[] handles)
    {
        if (thread != null) throw new InvalidOperationException("Hooks already installed.");
        windows = handles;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                ctrl = (NativeMethods.GetAsyncKeyState(0x11) & 0x8000) != 0;
                alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;
                shift = (NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0;
                keyboard = NativeMethods.SetWindowsHookEx(13, keyboardCallback, NativeMethods.GetModuleHandle(null), 0);
                if (keyboard == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Keyboard hook installation failed.");
                mouse = NativeMethods.SetWindowsHookEx(14, mouseCallback, NativeMethods.GetModuleHandle(null), 0);
                if (mouse == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Mouse hook installation failed.");
                ready.SetResult();
                Dispatcher.Run();
            }
            catch (Exception ex) { ready.TrySetException(ex); }
            finally { Remove(); }
        }) { IsBackground = true, Name = "Input Blocker hooks" };
        thread.Start();
        try { ready.Task.GetAwaiter().GetResult(); } catch { Dispose(); throw; }
    }
    private nint Keyboard(int code, nint wp, nint lp)
    {
        if (code < 0) return NativeMethods.CallNextHookEx(0, code, wp, lp);
        var data = Marshal.PtrToStructure<NativeMethods.Keyboard>(lp);
        bool down = wp == 0x100 || wp == 0x104;
        if (data.Key is 0x11 or 0xA2 or 0xA3) ctrl = down;
        if (data.Key is 0x12 or 0xA4 or 0xA5) alt = down;
        if (data.Key is 0x10 or 0xA0 or 0xA1) shift = down;
        if (down && ctrl && alt && data.Key == 0x7B) { Emergency?.Invoke(); return 1; }
        // Never suppress the secure-attention Delete key or the Task Manager shortcut.
        if (ctrl && alt && data.Key == 0x2E || ctrl && shift && data.Key == 0x1B) return NativeMethods.CallNextHookEx(0, code, wp, lp);
        if ((data.Flags & 0x10) != 0) return 1; // No implicit synthetic-input bypass.
        if (!windows.Contains(NativeMethods.GetForegroundWindow())) return 1;
        if (data.Key is 0x5B or 0x5C || (ctrl && data.Key == 0x1B) || (alt && data.Key is 0x09 or 0x1B or 0x73 or 0x20)) return 1;
        return NativeMethods.CallNextHookEx(0, code, wp, lp);
    }
    private nint Mouse(int code, nint wp, nint lp)
    {
        if (code < 0) return NativeMethods.CallNextHookEx(0, code, wp, lp);
        var data = Marshal.PtrToStructure<NativeMethods.Mouse>(lp);
        if ((data.Flags & 1) != 0) return 1;
        var target = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(data.Point), 2);
        return windows.Contains(target) ? NativeMethods.CallNextHookEx(0, code, wp, lp) : 1;
    }
    private void Remove()
    {
        // Windows removes hooks when their owning thread exits, even if unhook reports failure.
        if (keyboard != 0) { NativeMethods.UnhookWindowsHookEx(keyboard); keyboard = 0; }
        if (mouse != 0) { NativeMethods.UnhookWindowsHookEx(mouse); mouse = 0; }
    }
    public void Dispose()
    {
        dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
        if (thread != null && thread != Thread.CurrentThread && !thread.Join(2000))
            Environment.FailFast("Input hook thread did not stop; terminating to restore Windows input.");
        thread = null; dispatcher = null; windows = [];
    }
}
