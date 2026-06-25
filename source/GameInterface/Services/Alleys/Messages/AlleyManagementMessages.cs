using Common.Messaging;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

#nullable enable

namespace GameInterface.Services.Alleys.Messages;

// --- Local events: published on the requesting client from the menu/screen patches ---

/// <summary>
/// Player asked to abandon an owned alley. <see cref="FromClanScreen"/> mirrors vanilla
/// AbandonTheAlley(fromClanScreen): a clan-screen abandon forfeits the garrison, a menu/dialog
/// abandon returns the garrison troops to the owner's party.
/// </summary>
public readonly struct AbandonAlleyRequested : IEvent
{
    public readonly Alley Alley;
    public readonly bool FromClanScreen;
    public AbandonAlleyRequested(Alley alley, bool fromClanScreen)
    {
        Alley = alley;
        FromClanScreen = fromClanScreen;
    }
}

/// <summary>Player picked a new overseer for an owned alley.</summary>
public readonly struct ChangeAlleyOverseerRequested : IEvent
{
    public readonly Alley Alley;
    public readonly Hero NewOverseer;
    public ChangeAlleyOverseerRequested(Alley alley, Hero newOverseer)
    {
        Alley = alley;
        NewOverseer = newOverseer;
    }
}

/// <summary>Player edited the alley garrison in the manage-troops party screen.</summary>
public readonly struct SetAlleyGarrisonRequested : IEvent
{
    public readonly Alley Alley;
    public readonly TroopRoster NewGarrison;
    public SetAlleyGarrisonRequested(Alley alley, TroopRoster newGarrison)
    {
        Alley = alley;
        NewGarrison = newGarrison;
    }
}

// --- Networked client -> server requests ---

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestAbandonAlley : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    [ProtoMember(2)]
    public readonly bool FromClanScreen;
    public RequestAbandonAlley(string alleyId, bool fromClanScreen)
    {
        AlleyId = alleyId;
        FromClanScreen = fromClanScreen;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestChangeAlleyOverseer : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    [ProtoMember(2)]
    public readonly string NewOverseerId;
    public RequestChangeAlleyOverseer(string alleyId, string newOverseerId)
    {
        AlleyId = alleyId;
        NewOverseerId = newOverseerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestSetAlleyGarrison : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    [ProtoMember(2)]
    public readonly TroopRosterElementData[] Garrison;
    public RequestSetAlleyGarrison(string alleyId, TroopRosterElementData[] garrison)
    {
        AlleyId = alleyId;
        Garrison = garrison;
    }
}

// --- Networked server -> clients broadcasts ---

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlleyManagementUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    [ProtoMember(2)]
    public readonly string? OverseerId;
    [ProtoMember(3)]
    public readonly TroopRosterElementData[] Garrison;
    public NetworkAlleyManagementUpdated(string alleyId, string? overseerId, TroopRosterElementData[] garrison)
    {
        AlleyId = alleyId;
        OverseerId = overseerId;
        Garrison = garrison;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlleyManagementRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string AlleyId;
    public NetworkAlleyManagementRemoved(string alleyId) { AlleyId = alleyId; }
}
