using InputBlocker.Core;
using Xunit;

namespace InputBlocker.Tests;
public class KeyboardInputTests
{
    [Theory]
    [InlineData(0xA2)][InlineData(0xA3)][InlineData(0xA4)][InlineData(0xA5)]
    [InlineData(0xA0)][InlineData(0xA1)][InlineData(0x4C)]
    public void ActivationShortcutReleasesPassDuringBlackout(uint key) =>
        Assert.False(KeyboardInputPolicy.Suppress(false, true, key, false, false, true, true));

    [Fact]
    public void HotkeyActivationRevealThenPasswordKeepsModifierEventsBalanced()
    {
        // These keys were already delivered before the hotkey installed hooks.
        var windowsDown = new HashSet<uint> { 0xA2, 0xA4, 0xA0, 0x4C };
        foreach (uint key in windowsDown.ToArray())
            if (!KeyboardInputPolicy.Suppress(false, true, key, false, false, true, true)) windowsDown.Remove(key);
        Assert.Empty(windowsDown);

        foreach (uint key in new uint[] { 0xA2, 0xA0 })
            if (!KeyboardInputPolicy.Suppress(false, true, key, true, false, true, false)) windowsDown.Add(key);
        Assert.Equal(2, windowsDown.Count);
        Assert.True(Shortcut.IsRevealChord(true, true, false, 0xBB));
        foreach (uint key in windowsDown.ToArray())
            if (!KeyboardInputPolicy.Suppress(true, true, key, false, false, true, false)) windowsDown.Remove(key);
        Assert.Empty(windowsDown);
        Assert.False(KeyboardInputPolicy.Suppress(true, true, 0x41, true, false, false, false));
    }

    [Theory]
    [InlineData(0xA2)][InlineData(0xA0)][InlineData(0xA4)]
    public void ConcealedModifierPressesReachOnlyOverlay(uint key)
    {
        Assert.False(KeyboardInputPolicy.Suppress(false, true, key, true, false, false, false));
        Assert.True(KeyboardInputPolicy.Suppress(false, false, key, true, false, false, false));
    }

    [Fact]
    public void BlackoutStillSuppressesOrdinaryTypingAndInjectedInput()
    {
        Assert.True(KeyboardInputPolicy.Suppress(false, true, 0x41, true, false, false, false));
        Assert.True(KeyboardInputPolicy.Suppress(true, false, 0x41, true, false, false, false));
        Assert.True(KeyboardInputPolicy.Suppress(true, true, 0x41, true, true, false, false));
        Assert.True(KeyboardInputPolicy.Suppress(false, true, 0xA4, false, true, false, false));
    }
}
