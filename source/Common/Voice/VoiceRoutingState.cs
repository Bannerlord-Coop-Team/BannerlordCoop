using System;
using System.Collections.Generic;

namespace Common.Voice;

public interface IVoiceRoutingState
{
    bool ChangeContext(string connection, VoicePosition position);
    bool Update(string connection, VoicePacket packet, long now);
    IReadOnlyList<VoiceRoute> Route(string connection, VoicePacket packet, VoiceRanges ranges, long now);
    void Remove(string connection);
    void Clear();
}

public sealed class VoiceRoute
{
    public string Connection { get; }
    public long ListenerEpoch { get; }
    public float Gain { get; }
    public VoiceRoute(string connection, long listenerEpoch, float gain)
    {
        Connection = connection;
        ListenerEpoch = listenerEpoch;
        Gain = gain;
    }
}

/// <summary>Owned by the game thread. Reliable context establishes permission, never inferred from audio arrival.</summary>
public sealed class VoiceRoutingState : IVoiceRoutingState
{
    private sealed class Participant
    {
        public VoicePosition Position;
        public long Updated;
        public uint Sequence;
        public bool HasSequence;
    }

    private readonly Dictionary<string, Participant> participants = new();
    private readonly IVoicePolicy policy;

    public VoiceRoutingState(IVoicePolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        this.policy = policy;
    }

    public bool ChangeContext(string connection, VoicePosition position)
    {
        if (string.IsNullOrEmpty(connection) || position == null || !position.IsFinite ||
            position.Context == null || position.Context.Length > 256 || position.Epoch <= 0) return false;
        if (participants.TryGetValue(connection, out var previous) && position.Epoch <= previous.Position.Epoch)
            return false;
        if (previous == null && participants.Count >= 10) return false;
        participants[connection] = new Participant { Position = position, Updated = long.MinValue };
        return true;
    }

    public bool Update(string connection, VoicePacket packet, long now)
    {
        if (packet == null || !packet.IsValid || !participants.TryGetValue(connection, out var participant)) return false;
        if (packet.Position.Epoch != participant.Position.Epoch || packet.Position.Context != participant.Position.Context)
            return false;
        if (participant.HasSequence && unchecked((int)(packet.StateSequence - participant.Sequence)) <= 0) return false;
        participant.Position = packet.Position;
        participant.Sequence = packet.StateSequence;
        participant.HasSequence = true;
        participant.Updated = now;
        return true;
    }

    public IReadOnlyList<VoiceRoute> Route(string connection, VoicePacket packet, VoiceRanges ranges, long now)
    {
        var routes = new List<VoiceRoute>();
        if (ranges == null || !ranges.IsValid || !participants.TryGetValue(connection, out var speaker) ||
            !speaker.HasSequence || packet.Position.Epoch != speaker.Position.Epoch ||
            packet.StateSequence != speaker.Sequence || now - speaker.Updated > 500) return routes;
        foreach (var pair in participants)
        {
            if (pair.Key == connection || !pair.Value.HasSequence || now - pair.Value.Updated > 500) continue;
            float gain = policy.Gain(speaker.Position, pair.Value.Position, ranges);
            if (gain > 0) routes.Add(new VoiceRoute(pair.Key, pair.Value.Position.Epoch, gain));
        }
        return routes;
    }

    public void Remove(string connection) => participants.Remove(connection);
    public void Clear() => participants.Clear();
}
