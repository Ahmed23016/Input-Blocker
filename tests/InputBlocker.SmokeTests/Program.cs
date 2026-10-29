using System.Windows;
using System.Windows.Threading;
using InputBlocker;
using InputBlocker.Core;
using InputBlocker.Services;
using InputBlocker.Views;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                try { barrier.InstallHooks(); throw new Exception("Hooks accepted missing overlays."); }
                catch (InvalidOperationException) { }
                await barrier.PrepareAsync();
                var readyWindows = app.Windows.OfType<LockOverlayWindow>().ToArray();
                if (readyWindows.Length == 0 || readyWindows.Any(w => !w.IsReady)) throw new Exception("Overlay content not rendered before interception.");
                if (readyWindows.Any(w => ((HwndSource)PresentationSource.FromVisual(w)).CompositionTarget.RenderMode != RenderMode.SoftwareOnly))
                    throw new Exception("Overlay uses the hardware rendering path.");
                if (!readyWindows.Single(w => w.IsPrimary).IsKeyboardFocusWithin) throw new Exception("Unlock field did not get focus.");
                if (readyWindows.Any(w => w.IsUnlockVisible || !IsBlack(w))) throw new Exception("Initial overlay is not completely black.");
                Console.WriteLine("PASS: first render completed, opaque content, software rendering and unlock focus before hooks.");
                barrier.CloseOverlays();
                await controller.ActivateAsync();
                if (controller.State != BlockingState.Blocked || app.Windows.Count == 0) throw new Exception("Barrier did not activate.");
                int displays = app.Windows.Count;
                barrier.RevealUnlock();
                await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var revealed = app.Windows.OfType<LockOverlayWindow>().ToArray();
                if (!revealed.Single(w => w.IsPrimary).IsUnlockVisible || IsBlack(revealed.Single(w => w.IsPrimary)))
                    throw new Exception("Reveal did not display the unlock interface.");
                if (revealed.Where(w => !w.IsPrimary).Any(w => w.IsUnlockVisible || !IsBlack(w)))
                    throw new Exception("Secondary monitors should remain black.");
                if (controller.State != BlockingState.Blocked) throw new Exception("Reveal must not unlock.");
                Console.WriteLine("PASS: pure-black initial frames, primary-only reveal, secondary blackout and blocking retained.");
                await Task.Delay(150);
                controller.Recover();
                if (controller.State != BlockingState.Unlocked || app.Windows.Count != 0) throw new Exception("Recovery did not clean up.");
                await controller.ActivateAsync();
                if (app.Windows.OfType<LockOverlayWindow>().Any(w => w.IsUnlockVisible || !IsBlack(w)))
                    throw new Exception("Reactivation must start black again.");
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
    private static bool IsBlack(LockOverlayWindow window)
    {
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
            dc.DrawRectangle(new VisualBrush((Visual)window.Content), null, new Rect(0, 0, 64, 64));
        var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(drawing);
        var pixels = new byte[64 * 64 * 4]; bitmap.CopyPixels(pixels, 64 * 4, 0);
        for (int i = 0; i < pixels.Length; i += 4)
            if (pixels[i] != 0 || pixels[i+1] != 0 || pixels[i+2] != 0 || pixels[i+3] != 255) return false;
        return true;
    }
    private sealed class SmokeApp : App
    {
        protected override void OnStartup(StartupEventArgs e) { }
    }
}
