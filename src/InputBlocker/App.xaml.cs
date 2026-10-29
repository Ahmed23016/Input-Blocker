using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;
using InputBlocker.Infrastructure;
using InputBlocker.Services;
using InputBlocker.ViewModels;
using InputBlocker.Views;
namespace InputBlocker;
public partial class App : Application
{
    private Mutex? mutex;
    private SettingsStore store = null!;
    private HotkeyService? hotkeys;
    private System.Windows.Forms.NotifyIcon? tray;
    private DispatcherTimer? timer;
    private System.Threading.Timer? health;
    private long lastHeartbeat;
    private volatile bool barrierActive;
    private bool activating;
    private int activationGeneration;
    private DateTimeOffset autoEligible;
    private readonly AttemptThrottle throttle = new();
    private MainViewModel model = null!;
    public AppSettings Settings { get; private set; } = new();
    public BlockingController Controller { get; private set; } = null!;
    public bool Exiting { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, @"Local\InputBlocker-" + WindowsIdentity.GetCurrent().User!.Value, out bool first);
        if (!first) { MessageBox.Show("Input Blocker is already running. Open it from the system tray."); Shutdown(); return; }
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InputBlocker");
            var info = Directory.CreateDirectory(directory);
            var acl = new DirectorySecurity(); acl.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WindowsIdentity.GetCurrent().User!, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
                acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            info.SetAccessControl(acl);
            store = new SettingsStore(Path.Combine(directory, "settings.json")); Settings = store.Load(); ApplyTheme();
            SetStartup(Settings.StartWithWindows);
            var barrier = new DesktopBarrier(VerifyAsync, Recover);
            Controller = new BlockingController(barrier, Settings.Password != null);
            model = new MainViewModel(this);
            Controller.Changed += () => { barrierActive = Controller.State is BlockingState.Blocked or BlockingState.PreparingToBlock; model.Refresh(); if (tray != null) tray.Text = "Input Blocker — " + model.Status; };
            MainWindow = new MainWindow { DataContext = model };
            hotkeys = new HotkeyService(async () => await ActivateAsync(), Recover);
            if (!hotkeys.Configure(Settings)) MessageBox.Show("The activation shortcut is in use. Choose another in Settings. Button activation remains available.");
            CreateTray();
            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
            SystemEvents.SessionSwitch += SessionChanged;
            SystemEvents.UserPreferenceChanged += PreferenceChanged;
            DispatcherUnhandledException += (_, args) => { Recover(); args.Handled = true; MessageBox.Show("An unexpected error occurred. Input has been released. Restart Input Blocker before using it again."); Exit(); };
            timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, Tick, Dispatcher); timer.Start();
            lastHeartbeat = Environment.TickCount64;
            health = new System.Threading.Timer(_ => { if (barrierActive && Environment.TickCount64 - Interlocked.Read(ref lastHeartbeat) > 15000) Environment.FailFast("Unlock interface unresponsive. Terminating to release input."); }, null, 5000, 5000);
            autoEligible = DateTimeOffset.UtcNow.AddMinutes(Settings.IdleMinutes);
            if (!e.Args.Contains("--tray") || Settings.Password == null) MainWindow.Show();
            if (Settings.Password == null) { ShowPassword(true); if (Settings.Password == null) Exit(); }
        }
        catch (Exception ex) { MessageBox.Show("Input Blocker could not start safely.\n" + ex.Message, "Startup error"); Exit(); }
    }
    private void Tick(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref lastHeartbeat, Environment.TickCount64);
        if (Controller.State == BlockingState.Blocked)
        {
            // If another process becomes foreground (including Task Manager), fail open.
            var foreground = NativeMethods.GetForegroundWindow();
            bool ours = Windows.OfType<LockOverlayWindow>().Any(w => w.Handle == foreground);
            if (!ours) Recover();
            return;
        }
        if (Settings.AutoBlock && !activating && Controller.State == BlockingState.Unlocked && Windows.Count == 1 && DateTimeOffset.UtcNow >= autoEligible)
        {
            var input = new NativeMethods.LastInput { Size = 8 };
            if (NativeMethods.GetLastInputInfo(ref input) && unchecked((uint)Environment.TickCount - input.Time) >= Settings.IdleMinutes * 60000u) _ = ActivateAsync();
        }
    }
    public async Task ActivateAsync()
    {
        if (activating || Controller.State != BlockingState.Unlocked || Windows.OfType<Window>().Any(w => w != MainWindow && w.IsVisible)) return;
        activating = true;
        int activationToken = activationGeneration;
        try
        {
            Settings.Validate(); if (Settings.Password == null || hotkeys == null) throw new InvalidOperationException("Complete setup and recovery configuration first.");
            if (Settings.Countdown)
            {
                bool cancelled = false;
                var label = new TextBlock { Text = "Blocking in 3…", Margin = new Thickness(20), FontSize = 22 };
                var cancel = new Button { Content = "Cancel", Margin = new Thickness(20) };
                var panel = new StackPanel(); panel.Children.Add(label); panel.Children.Add(cancel);
                var window = new Window { Title = "Prepare to block", Width = 340, Height = 200, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterScreen };
                cancel.Click += (_, _) => { cancelled = true; window.Close(); }; window.Closed += (_, _) => cancelled = true; window.Show();
                for (int i = 3; i > 0 && !cancelled && activationToken == activationGeneration; i--) { label.Text = $"Blocking in {i}…"; await Task.Delay(1000); }
                bool proceed = !cancelled; window.Close(); if (!proceed) return;
            }
            if (activationToken != activationGeneration) return;
            await Controller.ActivateAsync();
        }
        catch (Exception ex) { Recover(); MessageBox.Show("Input was not blocked.\n" + ex.Message, "Safe activation failed"); }
        finally { activating = false; autoEligible = DateTimeOffset.UtcNow.AddMinutes(Settings.IdleMinutes); }
    }
    private async Task<string?> VerifyAsync(string value)
    {
        if (!throttle.CanAttempt(DateTimeOffset.UtcNow)) return $"Please wait {Math.Ceiling((throttle.NextAttempt - DateTimeOffset.UtcNow).TotalSeconds)} seconds before retrying.";
        var record = Settings.Password!;
        bool valid = await Task.Run(() => Authentication.Verify(value, record));
        if (Controller.State != BlockingState.Blocked) return null;
        if (!valid) { throttle.Failed(DateTimeOffset.UtcNow); return "That password did not match. Please try again."; }
        throttle.Reset(); Controller.Unlock(); autoEligible = DateTimeOffset.UtcNow.AddMinutes(Settings.IdleMinutes); return null;
    }
    public void Recover()
    {
        ++activationGeneration;
        if (Controller == null) return;
        try { Controller.Recover(); }
        catch { Environment.FailFast("Could not safely release input resources."); }
        autoEligible = DateTimeOffset.UtcNow.AddMinutes(Settings.IdleMinutes);
    }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Recover);
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(Recover);
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(Recover);
    private void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(ApplyTheme);
    public void Save() => store.Save(Settings);
    public void ApplySettings(AppSettings draft)
    {
        draft.Validate();
        if (!hotkeys!.Configure(draft)) { hotkeys.Configure(Settings); throw new InvalidOperationException("Shortcut already in use. Choose another letter."); }
        try { SetStartup(draft.StartWithWindows); store.Save(draft); }
        catch { hotkeys.Configure(Settings); SetStartup(Settings.StartWithWindows); throw; }
        Settings = draft; ApplyTheme(); model.Refresh(); autoEligible = DateTimeOffset.UtcNow.AddMinutes(Settings.IdleMinutes);
    }
    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("InputBlocker", $"\"{Environment.ProcessPath}\" --tray"); else key.DeleteValue("InputBlocker", false);
    }
    private void ApplyTheme()
    {
        bool light = Settings.Theme == "Light";
        if (Settings.Theme == "System")
        { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); light = key?.GetValue("AppsUseLightTheme") is int value && value != 0; }
        string[] colors = light ? ["#F7F8FA", "#FFFFFF", "#E9EBF2", "#18181B", "#71717A", "#6366F1"] : ["#101114", "#191B20", "#23262D", "#F5F5F7", "#A1A1AA", "#818CF8"];
        string[] names = ["BackgroundBrush", "SurfaceBrush", "ElevatedBrush", "TextBrush", "MutedBrush", "AccentBrush"];
        for (int i = 0; i < names.Length; i++) Resources[names[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
    public void ShowMain() { if (Controller.State != BlockingState.Unlocked && Controller.State != BlockingState.Unconfigured) return; MainWindow.Show(); MainWindow.WindowState = WindowState.Normal; MainWindow.Activate(); }
    public void ShowPassword(bool setup) { if (!setup && Controller.State != BlockingState.Unlocked) return; new PasswordWindow(this, setup) { Owner = MainWindow }.ShowDialog(); }
    public void ShowSettings() { if (Controller.State != BlockingState.Unlocked) return; ShowMain(); new SettingsWindow(this) { Owner = MainWindow }.ShowDialog(); }
    private void CreateTray()
    {
        tray = new System.Windows.Forms.NotifyIcon { Text = "Input Blocker — Unlocked", Visible = true, Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Input Blocker", null, (_, _) => ShowMain());
        menu.Items.Add("Block Input", null, async (_, _) => await ActivateAsync());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => ShowMain();
        if (Settings.ShowNotifications) tray.ShowBalloonTip(3000, "Input Blocker", "Available in the tray. Emergency release: Ctrl+Alt+F12.", System.Windows.Forms.ToolTipIcon.Info);
    }
    public new void Exit()
    {
        if (Exiting) return; Exiting = true; Recover();
        timer?.Stop(); health?.Dispose(); hotkeys?.Dispose(); tray?.Dispose();
        SystemEvents.DisplaySettingsChanged -= DisplayChanged; SystemEvents.PowerModeChanged -= PowerChanged; SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.UserPreferenceChanged -= PreferenceChanged;
        mutex?.Dispose(); Shutdown();
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e) { Recover(); base.OnSessionEnding(e); }
}
