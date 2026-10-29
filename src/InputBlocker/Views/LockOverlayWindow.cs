using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using InputBlocker.Infrastructure;
namespace InputBlocker.Views;
internal sealed class LockOverlayWindow : Window
{
    private bool allowClose;
    public nint Handle => new WindowInteropHelper(this).Handle;
    public LockOverlayWindow(bool primary, Func<string, Task<string?>> unlock)
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "Input Blocker — Input Blocked"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = new SolidColorBrush(Color.FromRgb(16,17,20)); Foreground = Brushes.White;
        var panel = new StackPanel { Width = 390, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = "\uE72E", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 54, Foreground = new SolidColorBrush(Color.FromRgb(129,140,248)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,26) });
        panel.Children.Add(new TextBlock { Text = "Input Blocked", FontSize = 32, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Your applications are still running in the background.", TextAlignment = TextAlignment.Center, Foreground = Brushes.LightGray, Margin = new Thickness(0,14,0,30) });
        if (primary)
        {
            var password = new PasswordEntry("Unlock password", false);
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
            Loaded += (_, _) => { Activate(); password.FocusInput(); };
        }
        else panel.Children.Add(new TextBlock { Text = "Enter your password on the primary display.", TextAlignment = TextAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "", TextAlignment = TextAlignment.Center, Foreground = Brushes.Gray, FontSize = 12, Margin = new Thickness(0,30,0,0) });
        Content = panel;
        Loaded += (_, _) =>
        {
            if (!((App)Application.Current).Settings.ReducedMotion && SystemParameters.ClientAreaAnimation)
                panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        };
        Closing += (_, e) => { if (!allowClose) e.Cancel = true; };
    }
    public void Place(System.Drawing.Rectangle bounds)
    {
        if (!NativeMethods.SetWindowPos(Handle, -1, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040)) throw new Win32Exception("Unable to cover monitor.");
    }
    public void Release() { allowClose = true; Close(); }
}
