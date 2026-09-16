namespace Common.Messaging;

/// <summary>Opt-in combination of adjacent replay messages before the final join baseline.</summary>
public interface IJoinCatchUpMergeMessage : IMessage
{
    // Only a lookup hint; TryMerge must check the complete identity and mutation semantics.
    string JoinCatchUpMergeKey { get; }

    bool TryMerge(IMessage next, out IMessage merged);
}
