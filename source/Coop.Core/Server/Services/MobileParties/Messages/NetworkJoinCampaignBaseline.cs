using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.MobileParties.Data;
using ProtoBuf;
using System;

namespace Coop.Core.Server.Services.MobileParties.Messages;

/// <summary>Authoritative XP for one regular troop in a joining player's party.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct TroopXpBaselineEntry
{
    [ProtoMember(1)]
    public readonly string CharacterId;

    [ProtoMember(2)]
    public readonly int Xp;

    public TroopXpBaselineEntry(string characterId, int xp)
    {
        CharacterId = characterId;
        Xp = xp;
    }
}

/// <summary>Regular-troop XP for one member or prisoner roster.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct TroopRosterXpBaseline
{
    [ProtoMember(1)]
    public readonly string RosterId;

    [ProtoMember(2)]
    public readonly TroopXpBaselineEntry[] Entries;

    public TroopRosterXpBaseline(string rosterId, TroopXpBaselineEntry[] entries)
    {
        RosterId = rosterId;
        Entries = entries;
    }
}

/// <summary>
/// Authoritative time and mobile-party state baseline for a joining client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkJoinCampaignBaseline : IMessage
{
    [ProtoMember(1)]
    public readonly long ServerTicks;

    [ProtoMember(2)]
    public readonly MobilePartyJoinState[] PartyStates;

    [ProtoMember(3)]
    public readonly bool IsComplete;

    [ProtoMember(4)]
    public readonly TimeControlEnum TimeControlMode;

    [ProtoMember(5)]
    public readonly TroopRosterXpBaseline[] TroopXpBaselines;

    public NetworkJoinCampaignBaseline(
        long serverTicks,
        TimeControlEnum timeControlMode,
        MobilePartyJoinState[] partyStates,
        bool isComplete = true,
        TroopRosterXpBaseline[] troopXpBaselines = null)
    {
        ServerTicks = serverTicks;
        TimeControlMode = timeControlMode;
        PartyStates = partyStates;
        IsComplete = isComplete;
        TroopXpBaselines = troopXpBaselines ?? Array.Empty<TroopRosterXpBaseline>();
    }
}
