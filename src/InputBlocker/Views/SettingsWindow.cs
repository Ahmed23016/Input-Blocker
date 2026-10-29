using System.Text.Json;
namespace InputBlocker.Views;
public sealed class SettingsWindow : Window
{
    public SettingsWindow(App app)
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Title = "Input Blocker settings"; Width = 560; Height = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var draft = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(app.Settings))!;
        var panel = new StackPanel { Margin = new Thickness(30) };
        void Heading(string title) => panel.Children.Add(new TextBlock { Text = title, FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,18,0,8) });
        CheckBox Toggle(string title, bool value, Action<bool> update)
        {
            var box = new CheckBox { Content = title, IsChecked = value }; box.Checked += (_, _) => update(true); box.Unchecked += (_, _) => update(false); panel.Children.Add(box); return box;
        }
        Heading("General");
        Toggle("Start with Windows", draft.StartWithWindows, v => draft.StartWithWindows = v);
        Toggle("Minimize to tray", draft.MinimizeToTray, v => draft.MinimizeToTray = v);
        Toggle("Close to tray", draft.CloseToTray, v => draft.CloseToTray = v);
        Toggle("Show notifications", draft.ShowNotifications, v => draft.ShowNotifications = v);
        Heading("Input blocking");
        Toggle("Enable global shortcut", draft.EnableShortcut, v => draft.EnableShortcut = v);
        panel.Children.Add(new TextBlock { Text = "Shortcut: Ctrl + Alt + letter (optionally Shift)" });
        Toggle("Include Shift", draft.ShortcutModifiers == 7, v => draft.ShortcutModifiers = v ? 7u : 3u);
        var key = new ComboBox { ItemsSource = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToArray(), SelectedItem = ((char)draft.ShortcutKey).ToString() }; panel.Children.Add(key);
        Toggle("Automatically block after inactivity", draft.AutoBlock, v => draft.AutoBlock = v);
        panel.Children.Add(new TextBlock { Text = "Inactivity duration (minutes)" });
        var duration = new ComboBox { ItemsSource = new[] { 1,5,10,15,30 }, SelectedItem = draft.IdleMinutes }; panel.Children.Add(duration);
        Toggle("Show a cancellable 3-second activation countdown", draft.Countdown, v => draft.Countdown = v);
        Heading("Security");
        Toggle("Require current password to change password", draft.RequireCurrentPassword, v => draft.RequireCurrentPassword = v);
        panel.Children.Add(new TextBlock { Text = "Turning this off allows anyone at the unlocked dashboard to replace your password.", FontSize = 12 });
        var password = new Button { Content = "Change password" }; password.Click += (_, _) => app.ShowPassword(false); panel.Children.Add(password);
        var recovery = new Button { Content = "Recovery instructions" }; recovery.Click += (_, _) => MessageBox.Show(this, "Press Ctrl+Alt+F12 to release input without a password.\n\nIf the app stops responding, press Ctrl+Alt+Delete, open Task Manager and end InputBlocker.exe. Windows removes the hooks when the process exits. Signing out or restarting also recovers the desktop.\n\nDisplay, sleep, and session changes release blocking automatically. No watchdog restarts this app.", "Safe recovery"); panel.Children.Add(recovery);
        Heading("Appearance");
        var theme = new ComboBox { ItemsSource = new[] { "System", "Dark", "Light" }, SelectedItem = draft.Theme }; panel.Children.Add(theme);
        Toggle("Reduced motion", draft.ReducedMotion, v => draft.ReducedMotion = v);
        var error = new TextBlock { Margin = new Thickness(0,14,0,4), Foreground = Brushes.IndianRed }; panel.Children.Add(error);
        var save = new Button { Content = "Save settings" }; panel.Children.Add(save);
        save.Click += (_, _) =>
        {
            try
            {
                draft.ShortcutKey = ((string)key.SelectedItem)[0]; draft.IdleMinutes = (int)duration.SelectedItem; draft.Theme = (string)theme.SelectedItem;
                draft.Password = app.Settings.Password; // A password change may have happened in the child dialog.
                app.ApplySettings(draft); DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
