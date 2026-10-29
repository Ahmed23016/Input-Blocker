namespace InputBlocker;
public partial class MainWindow : Window
{
    private App OwnerApp => (App)Application.Current;
    public MainWindow()
    {
        InitializeComponent();
        SetResourceReference(StyleProperty, typeof(Window));
        Closing += (_, e) => { if (!OwnerApp.Exiting && OwnerApp.Settings.CloseToTray) { e.Cancel = true; Hide(); } else if (!OwnerApp.Exiting) { e.Cancel = true; OwnerApp.Exit(); } };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && OwnerApp.Settings.MinimizeToTray) Hide(); };
    }
    private void ChangePassword(object sender, RoutedEventArgs e) => OwnerApp.ShowPassword(false);
    private void About(object sender, RoutedEventArgs e) => MessageBox.Show(this, "Input Blocker 1.0\nA local, offline Windows utility.\n\nThis is a casual deterrent, not Windows authentication. Background work continues, but desktop automation may be blocked.\n\nRecovery: Ctrl+Alt+F12, or Ctrl+Alt+Delete → Task Manager → end Input Blocker.\nNo telemetry. No administrator rights required.", "About Input Blocker");
}
