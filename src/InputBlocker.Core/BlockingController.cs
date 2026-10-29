namespace InputBlocker.Core;
public interface IBarrier
{
    Task PrepareAsync();
    void InstallHooks();
    void ReleaseHooks();
    void CloseOverlays();
}
public sealed class BlockingController(IBarrier barrier, bool configured)
{
    public BlockingState State { get; private set; } = configured ? BlockingState.Unlocked : BlockingState.Unconfigured;
    public event Action? Changed;
    private int generation;
    private void Set(BlockingState state) { State = state; Changed?.Invoke(); }
    public void Configure() { if (State == BlockingState.Unconfigured) Set(BlockingState.Unlocked); }
    public async Task ActivateAsync()
    {
        if (State != BlockingState.Unlocked) return;
        int token = ++generation;
        Set(BlockingState.PreparingToBlock);
        try
        {
            await barrier.PrepareAsync();
            if (token != generation) return;
            barrier.InstallHooks();
            Set(BlockingState.Blocked);
        }
        catch { Recover(); throw; }
    }
    public void Unlock()
    {
        if (State != BlockingState.Blocked) return;
        Set(BlockingState.Unlocking);
        Cleanup();
        Set(BlockingState.Unlocked);
    }
    public void Recover()
    {
        ++generation;
        if (State == BlockingState.Unconfigured) { Cleanup(); return; }
        Set(BlockingState.Recovering);
        try { Cleanup(); Set(BlockingState.Unlocked); }
        catch { Set(BlockingState.Error); throw; }
    }
    private void Cleanup() { try { barrier.ReleaseHooks(); } finally { barrier.CloseOverlays(); } }
}
