using System.Windows;
using System.Windows.Threading;
using InputBlocker;
using InputBlocker.Core;
using InputBlocker.Services;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        int result = 1;
        using var deadline = new Timer(_ => Environment.Exit(2), null, 10000, Timeout.Infinite);
        var app = new SmokeApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        // Drive a dedicated dispatcher without invoking the production OnStartup/settings path.
        var dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.BeginInvoke(async () =>
        {
            BlockingController? controller = null;
            var barrier = new DesktopBarrier(_ => Task.FromResult<string?>(null), () => controller?.Recover());
            controller = new BlockingController(barrier, true);
            try
            {
                await controller.ActivateAsync();
                if (controller.State != BlockingState.Blocked || app.Windows.Count == 0) throw new Exception("Barrier did not activate.");
                int displays = app.Windows.Count;
                await Task.Delay(150);
                controller.Recover();
                if (controller.State != BlockingState.Unlocked || app.Windows.Count != 0) throw new Exception("Recovery did not clean up.");
                await controller.ActivateAsync();
                controller.Unlock();
                if (controller.State != BlockingState.Unlocked || app.Windows.Count != 0) throw new Exception("Unlock did not clean up.");
                Console.WriteLine($"PASS: {displays} native overlay(s), real hook installation, recovery, reactivation, unlock and resource cleanup.");
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { controller.Recover(); dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
        });
        Dispatcher.Run();
        return result;
    }
    private sealed class SmokeApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }
}
