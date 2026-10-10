namespace CoopMcpServer;

public interface IBuildCleanupRecovery
{
    void Retain(Func<Task> cleanup);
    void RetainLease(IDisposable lease);
    Task RecoverAsync();
}

// The lifecycle gate serializes recovery and new builds; this owner outlives tool requests.
public sealed class BuildCleanupRecovery : IBuildCleanupRecovery
{
    private Func<Task> pending;
    private IDisposable lease;

    public void Retain(Func<Task> cleanup)
    {
        if (pending != null) throw new InvalidOperationException("Build cleanup is already pending.");
        pending = cleanup;
    }

    public void RetainLease(IDisposable lease)
    {
        if (pending == null) throw new InvalidOperationException("No build cleanup is pending.");
        this.lease = lease;
    }

    public async Task RecoverAsync()
    {
        if (pending == null) return;
        try { await pending(); }
        catch (Exception error) { throw new BuildCleanupException(this, error); }
        pending = null;
        lease?.Dispose();
        lease = null;
    }
}

public sealed class BuildCleanupException : IOException
{
    private readonly IBuildCleanupRecovery recovery;
    public BuildCleanupException(IBuildCleanupRecovery recovery, Exception inner)
        : base("Build cleanup is unconfirmed. Starts and deployments remain blocked; retry to recover.", inner)
    {
        this.recovery = recovery;
    }

    public void RetainLease(IDisposable lease) => recovery.RetainLease(lease);
}
