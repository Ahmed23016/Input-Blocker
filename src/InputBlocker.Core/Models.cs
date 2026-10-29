namespace InputBlocker.Core;

public enum BlockingState { Unconfigured, Unlocked, PreparingToBlock, Blocked, Unlocking, Recovering, Error }
public sealed record PasswordRecord(string Salt, string Hash, int Iterations = 600_000);
public sealed class AppSettings
{
    public PasswordRecord? Password { get; set; }
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool EnableShortcut { get; set; } = true;
    public uint ShortcutModifiers { get; set; } = 7;
    public uint ShortcutKey { get; set; } = 0x4C;
    public bool AutoBlock { get; set; }
    public int IdleMinutes { get; set; } = 5;
    public bool Countdown { get; set; }
    public bool RequireCurrentPassword { get; set; } = true;
    public string Theme { get; set; } = "System";
    public bool ReducedMotion { get; set; }
    public void Validate()
    {
        if (!new[] { 1, 5, 10, 15, 30 }.Contains(IdleMinutes)) throw new InvalidDataException("Invalid inactivity duration.");
        if (!new[] { "System", "Dark", "Light" }.Contains(Theme)) throw new InvalidDataException("Invalid theme.");
        if (!Shortcut.IsValid(ShortcutModifiers, ShortcutKey)) throw new InvalidDataException("Use Ctrl+Alt with a letter, optionally Shift.");
        if (Password is not null) Authentication.Validate(Password);
    }
}
public static class Shortcut
{
    public static bool IsValid(uint modifiers, uint key) => (modifiers == 3 || modifiers == 7) && key is >= 0x41 and <= 0x5A;
    public static string Display(uint modifiers, uint key) => $"Ctrl + Alt + {(modifiers == 7 ? "Shift + " : "")}{(char)key}";
}
