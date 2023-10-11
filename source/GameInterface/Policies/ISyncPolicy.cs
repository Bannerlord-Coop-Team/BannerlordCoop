namespace GameInterface.Policies;

public interface ISyncPolicy
{
    bool AllowOriginalCalls { get; }
}
