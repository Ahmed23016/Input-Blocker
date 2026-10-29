namespace InputBlocker.Views;
public sealed class PasswordWindow : Window
{
    public PasswordWindow(App app, bool setup)
    {
        SetResourceReference(StyleProperty, typeof(Window));
        Title = setup ? "Welcome to Input Blocker" : "Change password"; Width = 510; Height = setup ? 640 : 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = setup ? "Let your work keep running." : "A new key to your desktop.", FontSize = 27, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Input Blocker covers your displays and deters casual keyboard and mouse use. It does not lock or secure your Windows account.\n\nEmergency release: Ctrl + Alt + F12. You can also use Ctrl + Alt + Delete and Task Manager.", Margin = new Thickness(0,16,0,22) });
        var current = new PasswordEntry("Current password");
        if (!setup && app.Settings.RequireCurrentPassword) panel.Children.Add(current);
        var next = new PasswordEntry("New password · at least 8 characters"); var confirm = new PasswordEntry("Confirm password");
        panel.Children.Add(next); panel.Children.Add(confirm);
        var status = new TextBlock { MinHeight = 42, Margin = new Thickness(0,8,0,0) }; panel.Children.Add(status);
        void Match() => status.Text = confirm.Value.Length == 0 ? "" : next.Value == confirm.Value ? "Passwords match" : "Passwords do not match";
        next.Changed += Match; confirm.Changed += Match;
        var accept = new CheckBox { Content = "I understand the limits and recovery shortcut." }; if (setup) panel.Children.Add(accept);
        var save = new Button { Content = setup ? "Finish setup" : "Save password" }; panel.Children.Add(save);
        save.Click += async (_, _) =>
        {
            if (next.Value != confirm.Value) { status.Text = "Passwords do not match."; return; }
            if (setup && accept.IsChecked != true) { status.Text = "Confirm that you understand recovery first."; return; }
            save.IsEnabled = false;
            string oldValue = current.Value, newValue = next.Value;
            try
            {
                var record = await Task.Run(() => setup ? Authentication.Create(newValue) : Authentication.Change(oldValue, newValue, app.Settings.Password!, app.Settings.RequireCurrentPassword));
                var oldRecord = app.Settings.Password; app.Settings.Password = record;
                try { app.Save(); } catch { app.Settings.Password = oldRecord; throw; }
                current.Clear(); next.Clear(); confirm.Clear(); app.Controller.Configure(); DialogResult = true;
            }
            catch (Exception ex) { status.Text = ex.Message; }
            finally { save.IsEnabled = true; }
        };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
