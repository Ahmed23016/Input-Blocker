namespace InputBlocker.Core;

/// <summary>Normal key routing after recovery and reveal chords have been handled.</summary>
public static class KeyboardInputPolicy
{
    public static bool Suppress(bool unlockVisible, bool overlayForeground, uint key,
        bool down, bool injected, bool ctrl, bool alt)
    {
        if (injected) return true;
        // Key-up never types a character. In particular, preserve releases of keys
        // pressed before the hooks were installed by the activation shortcut.
        if (!down) return false;
        if (!overlayForeground) return true;
        // Keep Windows/WPF modifier state synchronized while the screen is black.
        // Ordinary character key-downs still cannot reach the concealed field.
        if (key is 0x10 or 0x11 or 0x12 or >= 0xA0 and <= 0xA5) return false;
        if (!unlockVisible) return true;
        return key is 0x5B or 0x5C || (ctrl && key == 0x1B) ||
            (alt && key is 0x09 or 0x1B or 0x73 or 0x20);
    }
}
