namespace Common;

/// <summary>Receives the timing of every <see cref="GameThread"/> frame while it is set as <see cref="GameThread.FrameObserver"/>.</summary>
public interface IGameThreadFrameObserver
{
    /// <summary>Called after each frame's queue drain, with <see cref="System.Diagnostics.Stopwatch"/> timestamps.</summary>
    void OnFrame(long drainStartedTimestamp, long drainFinishedTimestamp);
}
