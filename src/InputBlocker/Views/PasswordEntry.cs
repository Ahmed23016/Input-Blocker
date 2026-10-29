namespace InputBlocker.Views;
public sealed class PasswordEntry : StackPanel
{
    private readonly PasswordBox hidden = new() { MaxLength = 1024 };
    private readonly TextBox visible = new() { MaxLength = 1024, Visibility = Visibility.Collapsed };
    private readonly CheckBox reveal = new() { Content = "Show password" };
    public string Value => reveal.IsChecked == true ? visible.Text : hidden.Password;
    public event Action? Changed;
    public PasswordEntry(string label, bool allowReveal = true)
    {
        Children.Add(new TextBlock { Text = label }); Children.Add(hidden); Children.Add(visible);
        System.Windows.Automation.AutomationProperties.SetName(hidden, label);
        System.Windows.Automation.AutomationProperties.SetName(visible, label);
        if (allowReveal) Children.Add(reveal);
        reveal.Checked += (_, _) => { visible.Text = hidden.Password; hidden.Clear(); hidden.Visibility = Visibility.Collapsed; visible.Visibility = Visibility.Visible; visible.Focus(); };
        reveal.Unchecked += (_, _) => { hidden.Password = visible.Text; visible.Clear(); visible.Visibility = Visibility.Collapsed; hidden.Visibility = Visibility.Visible; hidden.Focus(); };
        hidden.PasswordChanged += (_, _) => Changed?.Invoke(); visible.TextChanged += (_, _) => Changed?.Invoke();
    }
    public void Clear() { hidden.Clear(); visible.Clear(); }
    public void FocusInput() { if (reveal.IsChecked == true) visible.Focus(); else hidden.Focus(); }
}
