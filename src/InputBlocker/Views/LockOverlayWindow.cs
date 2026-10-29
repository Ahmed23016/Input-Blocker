using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using InputBlocker.Infrastructure;
namespace InputBlocker.Views;
internal sealed class LockOverlayWindow : Window
{
    private bool allowClose;
    private readonly TaskCompletionSource rendered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StackPanel panel;
    private readonly PasswordEntry? passwordInput;
    private readonly Border blackout = new() { Background = Brushes.Black };
    public bool IsUnlockVisible => blackout.Visibility == Visibility.Collapsed;
    public bool IsPrimary { get; }
    public Task Rendered => rendered.Task;
    public bool IsReady => rendered.Task.IsCompletedSuccessfully && IsVisible &&
        panel.Opacity == 1 && panel.ActualWidth > 0 && panel.ActualHeight > 0 &&
        ActualWidth > 0 && ActualHeight > 0;
    public nint Handle => new WindowInteropHelper(this).Handle;
    public LockOverlayWindow(bool primary, Func<string, Task<string?>> unlock)
    {
        IsPrimary = primary;
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "Input Blocker — Input Blocked"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = new SolidColorBrush(Color.FromRgb(16,17,20)); Foreground = Brushes.White;
        // The unlock UI must never depend on a fade animation completing.
        panel = new StackPanel { MaxWidth = 390, Margin = new Thickness(24), Opacity = 1,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        SourceInitialized += (_, _) =>
        {
            // Keep fullscreen rendering independent of the GPU/driver path.
            if (PresentationSource.FromVisual(this) is HwndSource source)
                source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
        };
        ContentRendered += (_, _) => rendered.TrySetResult();
        Closed += (_, _) => rendered.TrySetCanceled();
        panel.Children.Add(new TextBlock { Text = "\uE72E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 54, Foreground = new SolidColorBrush(Color.FromRgb(129,140,248)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,26) });
        panel.Children.Add(new TextBlock { Text = "Input Blocked", FontSize = 32, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Your applications are still running in the background.", TextAlignment = TextAlignment.Center, Foreground = Brushes.LightGray, Margin = new Thickness(0,14,0,30) });
        if (primary)
        {
            var password = new PasswordEntry("Unlock password", false);
            passwordInput = password;
            var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(248,113,113)), MinHeight = 44, Margin = new Thickness(0,8,0,0) };
            var button = new Button { Content = "Unlock", Background = new SolidColorBrush(Color.FromRgb(129,140,248)), Foreground = Brushes.Black };
            async void Submit(object? sender, RoutedEventArgs args)
            {
                if (!button.IsEnabled) return;
                button.IsEnabled = false;
                string value = password.Value; password.Clear();
                try { error.Text = await unlock(value) ?? ""; }
                catch { error.Text = "Unable to verify. Use Ctrl + Alt + F12 to recover."; }
                finally { button.IsEnabled = true; password.FocusInput(); }
            }
            button.Click += Submit;
            password.KeyDown += (sender, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit(sender, e); } };
            panel.Children.Add(password); panel.Children.Add(button); panel.Children.Add(error);
        }
        else panel.Children.Add(new TextBlock { Text = "Enter your password on the primary display.", TextAlignment = TextAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "", TextAlignment = TextAlignment.Center, Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0,30,0,0) });
        var layout = new Grid();
        layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center });
        // Keep the prepared unlock UI underneath an opaque cover, with no initial flash.
        layout.Children.Add(blackout);
        Content = layout;
        Cursor = Cursors.None;
        Closing += (_, e) => { if (!allowClose) e.Cancel = true; };
    }
    public bool ShowUnlockScreen()
    {
        if (!IsPrimary || !IsReady) return false;
        passwordInput!.Clear();
        blackout.Visibility = Visibility.Collapsed;
        Cursor = Cursors.Arrow;
        return FocusUnlock();
    }
    public bool FocusUnlock()
    {
        if (!IsPrimary || !IsReady) return false;
        Activate(); passwordInput!.FocusInput();
        return passwordInput.IsKeyboardFocusWithin;
    }
    public void Place(System.Drawing.Rectangle bounds)
    {
        if (!NativeMethods.SetWindowPos(Handle, -1, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040)) throw new Win32Exception("Unable to cover monitor.");
    }
    public void Release() { allowClose = true; Close(); }
}
