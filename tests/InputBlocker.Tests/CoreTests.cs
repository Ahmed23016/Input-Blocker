using InputBlocker.Core;
using Xunit;
namespace InputBlocker.Tests;
public class CoreTests
{
    [Fact] public void PasswordsUseRandomSaltsAndUnicode()
    {
        const string password = "雪🌙pässword!";
        var a = Authentication.Create(password); var b = Authentication.Create(password);
        Assert.NotEqual(a.Salt, b.Salt); Assert.NotEqual(a.Hash, b.Hash);
        Assert.True(Authentication.Verify(password, a)); Assert.False(Authentication.Verify("incorrect", a));
    }
    [Fact] public void PasswordChangeRequiresCurrentSecret()
    {
        var a = Authentication.Create("original password");
        Assert.Throws<UnauthorizedAccessException>(() => Authentication.Change("wrong", "new password", a));
        var b = Authentication.Change("original password", "new password", a);
        Assert.True(Authentication.Verify("new password", b)); Assert.False(Authentication.Verify("original password", b));
    }
    [Theory][InlineData("")][InlineData("1234567")][InlineData("🌙🌙🌙🌙")]
    public void ShortPasswordsRejected(string value) => Assert.Throws<ArgumentException>(() => Authentication.Create(value));
    [Fact] public void MalformedHashRejected() => Assert.Throws<InvalidDataException>(() => Authentication.Validate(new PasswordRecord("AA==", "AA==")));
    [Fact] public void SettingsRoundTripAndInvalidSettings()
    {
        string dir = Path.Combine(Path.GetTempPath(), "InputBlockerTest-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json")); Assert.Null(store.Load().Password);
            var settings = new AppSettings { Password = Authentication.Create("test password"), Theme = "Light", AutoBlock = true };
            store.Save(settings); Assert.Equal(settings.Password, store.Load().Password); Assert.True(store.Load().AutoBlock);
            settings.IdleMinutes = 4; Assert.Throws<InvalidDataException>(() => store.Save(settings));
            Assert.Equal(5, store.Load().IdleMinutes);
            File.WriteAllText(Path.Combine(dir,"settings.json"), "broken"); Assert.ThrowsAny<Exception>(() => store.Load());
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    [Theory][InlineData(7,76,true)][InlineData(3,65,true)][InlineData(0,65,false)][InlineData(8,76,false)][InlineData(7,123,false)]
    public void ShortcutsValidated(uint mods, uint key, bool expected) => Assert.Equal(expected, Shortcut.IsValid(mods,key));
    [Fact] public void ThrottleEnforcesDelayAndResets()
    {
        var t = new AttemptThrottle(); var now = DateTimeOffset.UtcNow;
        t.Failed(now); t.Failed(now); t.Failed(now); Assert.False(t.CanAttempt(now)); Assert.True(t.CanAttempt(now.AddSeconds(6))); t.Reset(); Assert.True(t.CanAttempt(now));
    }
    [Fact] public async Task NormalTransitionsPreserveOrdering()
    {
        var barrier = new FakeBarrier(); var c = new BlockingController(barrier, false);
        await c.ActivateAsync(); Assert.Empty(barrier.Events); c.Configure();
        var states = new List<BlockingState>(); c.Changed += () => states.Add(c.State);
        await c.ActivateAsync(); await c.ActivateAsync(); c.Unlock();
        Assert.Equal(new[] { "prepare", "install", "release", "close" }, barrier.Events);
        Assert.Equal(new[] { BlockingState.PreparingToBlock, BlockingState.Blocked, BlockingState.Unlocking, BlockingState.Unlocked }, states);
    }
    [Theory][InlineData(true,false)][InlineData(false,true)]
    public async Task FailedHooksOrPartialOverlaysCleanUp(bool prepareFail, bool installFail)
    {
        var barrier = new FakeBarrier { PrepareFail = prepareFail, InstallFail = installFail };
        var c = new BlockingController(barrier,true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.ActivateAsync());
        Assert.Equal(BlockingState.Unlocked,c.State); Assert.Equal(new[] { "release", "close" }, barrier.Events.TakeLast(2));
        if (prepareFail) Assert.DoesNotContain("install",barrier.Events);
    }
    [Fact] public async Task RecoveryDuringPrepareNeverInstallsHooks()
    {
        var ready = new TaskCompletionSource(); var barrier = new FakeBarrier { Preparation = ready.Task };
        var c = new BlockingController(barrier,true); var activation = c.ActivateAsync(); c.Recover(); ready.SetResult(); await activation;
        Assert.DoesNotContain("install",barrier.Events); Assert.Equal(BlockingState.Unlocked,c.State);
    }
    [Fact] public async Task RecoveryClosesOverlayEvenWhenReleaseFails()
    {
        var barrier = new FakeBarrier(); var c = new BlockingController(barrier,true); await c.ActivateAsync(); barrier.ReleaseFail = true;
        Assert.Throws<InvalidOperationException>(() => c.Recover()); Assert.Equal("close",barrier.Events.Last()); Assert.Equal(BlockingState.Error,c.State);
    }
    private sealed class FakeBarrier : IBarrier
    {
        public List<string> Events = []; public bool PrepareFail, InstallFail, ReleaseFail; public Task Preparation = Task.CompletedTask;
        public async Task PrepareAsync() { Events.Add("prepare"); if (PrepareFail) throw new InvalidOperationException(); await Preparation; }
        public void InstallHooks() { Events.Add("install"); if (InstallFail) throw new InvalidOperationException(); }
        public void ReleaseHooks() { Events.Add("release"); if (ReleaseFail) throw new InvalidOperationException(); }
        public void CloseOverlays() => Events.Add("close");
    }
}
