using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Messages;

/// <summary>The local client observed its controlled army leader after campaign movement.</summary>
public readonly struct ArmyLeaderPositionObserved : IEvent
{
    public MobileParty LeaderParty { get; }

    public ArmyLeaderPositionObserved(MobileParty leaderParty)
    {
        LeaderParty = leaderParty;
    }
}

/// <summary>Requests formation-only convergence of a client-controlled army leader on the server.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestArmyLeaderPositionConvergence : ICommand
{
    [ProtoMember(1)]
    public string LeaderPartyId { get; }

    [ProtoMember(2)]
    public CampaignVec2 Position { get; }

    public NetworkRequestArmyLeaderPositionConvergence(string leaderPartyId, CampaignVec2 position)
    {
        LeaderPartyId = leaderPartyId;
        Position = position;
    }
}
