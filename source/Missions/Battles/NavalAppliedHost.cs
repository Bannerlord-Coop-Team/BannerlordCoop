using Common.Messaging;
using Missions.Messages;
using System;

namespace Missions.Battles;

/// <summary>[Game thread] The battle host and host epoch whose AI hull authority this client has applied.</summary>
public interface INavalAppliedHost : IAppliedHostEpoch, IDisposable
{
    /// <summary>The host the AI hulls last moved to, or null before a host change applied here.</summary>
    string Host { get; }

    /// <summary>Records the host change whose AI hull move this client just applied.</summary>
    void Apply(string host, int epoch);
}

/// <inheritdoc cref="INavalAppliedHost"/>
public class NavalAppliedHost : INavalAppliedHost
{
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;

    // Starts at the epoch the session holds as the battle opens here, which is 0 until this client knows an assignment.
    public NavalAppliedHost(IMessageBroker messageBroker, IBattleSession session)
    {
        this.messageBroker = messageBroker;
        this.session = session;
        AppliedHostEpoch = session.HostEpoch;
        messageBroker.Subscribe<BattleHostAssignmentApplied>(Handle_BattleHostAssignmentApplied);
    }

    public int AppliedHostEpoch { get; private set; }

    public string Host { get; private set; }

    public void Apply(string host, int epoch)
    {
        Host = host;
        AppliedHostEpoch = epoch;
    }

    // [Game thread] The first assignment this client learns is the epoch it joined at. A host change only follows a known
    // assignment, so its epoch never lands here first; it waits for its queued hull move to call Apply.
    private void Handle_BattleHostAssignmentApplied(MessagePayload<BattleHostAssignmentApplied> payload)
    {
        var assignment = payload.What.Assignment;
        if (AppliedHostEpoch != 0 || assignment.MapEventId != session.InstanceId) return;

        AppliedHostEpoch = assignment.Epoch;
    }

    // Drops the assignment subscription with the mission's services.
    public void Dispose() => messageBroker.Unsubscribe<BattleHostAssignmentApplied>(Handle_BattleHostAssignmentApplied);
}
