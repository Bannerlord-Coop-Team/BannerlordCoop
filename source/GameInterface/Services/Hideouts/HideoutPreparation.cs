using GameInterface.Services.Players;
using GameInterface.Services.ObjectManager;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Hideouts;

internal interface IHideoutPreparation
{
    Hero GetAttackLeader(Settlement settlement);
    HideoutEntryState GetState(Settlement settlement, MobileParty party);
}

internal sealed class HideoutPreparation : IHideoutPreparation
{
    private readonly IPlayerManager players;
    private readonly IObjectManager objectManager;

    public HideoutPreparation(IPlayerManager players, IObjectManager objectManager)
    {
        if (players == null) throw new ArgumentNullException(nameof(players));
        if (objectManager == null) throw new ArgumentNullException(nameof(objectManager));
        this.players = players;
        this.objectManager = objectManager;
    }

    private MobileParty GetPreparingParty(Settlement settlement) => settlement?.IsHideout == true
        ? settlement.Parties.FirstOrDefault(party => party.IsActive && players.Contains(party))
        : null;

    public Hero GetAttackLeader(Settlement settlement)
    {
        var party = settlement?.Party.MapEvent?.AttackerSide.LeaderParty?.MobileParty ?? GetPreparingParty(settlement);
        if (party == null || !objectManager.TryGetId(party, out var partyId)) return null;
        // Player parties can have no native component leader on the server.
        var player = players.Players.FirstOrDefault(candidate => candidate.MobilePartyId == partyId);
        return player != null && objectManager.TryGetObject<Hero>(player.HeroId, out var hero) ? hero : null;
    }

    public HideoutEntryState GetState(Settlement settlement, MobileParty party)
    {
        var battle = settlement?.Party.MapEvent;
        if (battle?.IsHideoutBattle == true && !battle.IsFinalized && battle.BattleState == BattleState.None &&
            (battle.Component as HideoutEventComponent)?.IsSendTroops != true)
            return HideoutEntryState.Join;

        var preparing = GetPreparingParty(settlement);
        return preparing != null && preparing != party ? HideoutEntryState.Waiting : HideoutEntryState.Start;
    }
}

internal enum HideoutEntryState
{
    Start,
    Waiting,
    Join,
}
