using System.ComponentModel;
using InputBlocker.Infrastructure;
namespace InputBlocker.ViewModels;
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly App app;
    public MainViewModel(App app)
    {
        this.app = app;
        BlockCommand = new RelayCommand(async () => await app.ActivateAsync(), () => app.Controller.State == BlockingState.Unlocked);
        SettingsCommand = new RelayCommand(() => app.ShowSettings());
    }
    public string Status => app.Controller.State switch { BlockingState.Unlocked => "Unlocked", BlockingState.Blocked => "Input Blocked", _ => app.Controller.State.ToString() };
    public string ShortcutLabel => app.Settings.EnableShortcut ? Shortcut.Display(app.Settings.ShortcutModifiers, app.Settings.ShortcutKey) : "Global shortcut disabled";
    public RelayCommand BlockCommand { get; }
    public RelayCommand SettingsCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); BlockCommand.Refresh(); }
}
