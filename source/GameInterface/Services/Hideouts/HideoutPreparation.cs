using GameInterface.Services.MapEvents.Extensions;
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

/// <summary>Answers whether a hideout can be attacked and which entry state a player party sees while an attack is set up.</summary>
internal interface IHideoutPreparation
{
    Hero GetAttackLeader(Settlement settlement);
    HideoutEntryState GetState(Settlement settlement, MobileParty party);
    bool HasBandits(Settlement settlement);
    bool CanBeAttacked(Settlement settlement);
}

/// <summary>Reads hideout attack eligibility and attack setup state from the settlement and the registered players.</summary>
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
            !battle.IsSendTroopsHideout())
            return HideoutEntryState.Join;

        var preparing = GetPreparingParty(settlement);
        return preparing != null && preparing != party ? HideoutEntryState.Waiting : HideoutEntryState.Start;
    }

    /// <summary>Whether any bandit party is inside. Not Hideout.IsInfested, which needs more parties than native attacks do.</summary>
    public bool HasBandits(Settlement settlement) => settlement.Parties.Any(party => party.IsBandit);

    /// <summary>Matches the native hideout attack menus: a bandit party is inside and the cooldown is over.</summary>
    public bool CanBeAttacked(Settlement settlement) =>
        HasBandits(settlement) && settlement.Hideout.NextPossibleAttackTime.IsPast;
}

internal enum HideoutEntryState
{
    Start,
    Waiting,
    Join,
}
