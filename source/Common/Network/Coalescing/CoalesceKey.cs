using System;

namespace Common.Network.Coalescing;

/// <summary>
/// Identifies one coalesced update slot. Repeated enqueues with an equal key collapse into a single
/// merged payload that is flushed once per server tick.
/// </summary>
/// <remarks>
/// <para><see cref="Channel"/> names the logical send path, one per consumer / message family.</para>
/// <para><see cref="InstanceId"/> is the network id of the object being updated (the object-manager
/// string id).</para>
/// <para><see cref="Member"/> is the part of the object being updated: a field name, or a composite
/// element id encoded into the string (for example "itemId:modifierId"). It is empty when the whole
/// object is the unit being sent, such as a snapshot.</para>
/// </remarks>
public readonly struct CoalesceKey : IEquatable<CoalesceKey>
{
    public string Channel { get; }
    public string InstanceId { get; }
    public string Member { get; }

    public CoalesceKey(string channel, string instanceId, string member = "")
    {
        Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        InstanceId = instanceId ?? throw new ArgumentNullException(nameof(instanceId));
        Member = member ?? string.Empty;
    }

    public bool Equals(CoalesceKey other) =>
        string.Equals(Channel, other.Channel, StringComparison.Ordinal) &&
        string.Equals(InstanceId, other.InstanceId, StringComparison.Ordinal) &&
        string.Equals(Member, other.Member, StringComparison.Ordinal);

    public override bool Equals(object obj) => obj is CoalesceKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (Channel?.GetHashCode() ?? 0);
            hash = (hash * 31) + (InstanceId?.GetHashCode() ?? 0);
            hash = (hash * 31) + (Member?.GetHashCode() ?? 0);
            return hash;
        }
    }

    public override string ToString() => $"{Channel}/{InstanceId}/{Member}";
}
