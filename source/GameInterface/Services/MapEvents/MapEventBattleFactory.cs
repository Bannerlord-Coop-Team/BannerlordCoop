using Common.Logging;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Recreates the battle-type selection performed by
/// <c>TaleWorlds.CampaignSystem.Encounters.PlayerEncounter.StartBattleInternal</c> so the server can create the
/// authoritative <see cref="MapEvent"/> on a client's behalf.
/// </summary>
/// <remarks>
/// The branch order here mirrors vanilla <c>StartBattleInternal</c> exactly. The "force" decisions come from the
/// requesting client (<see cref="BattleCreationFlags"/>); the settlement/siege/blockade decisions are derived from
/// the parties' own state, which is already synchronized on the server.
/// </remarks>
internal sealed class MapEventBattleFactory
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventBattleFactory>();

    private MapEventBattleFactory() { }

    /// <summary>
    /// Creates the <see cref="MapEvent"/> the supplied parties would produce in <c>StartBattleInternal</c>.
    /// Must be called on the main thread inside an <see cref="Common.Util.AllowedThread"/> scope.
    /// </summary>
    /// <returns>The created <see cref="MapEvent"/>, or null if no proper type could be determined.</returns>
    public static MapEvent CreateMapEvent(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        var mapEventManager = Campaign.Current.MapEventManager;

        if (flags.ForceRaid)
            return RaidEventComponent.CreateRaidEvent(attacker, defender).MapEvent;

        if (flags.ForceSallyOut)
            return mapEventManager.StartSallyOutMapEvent(attacker, defender);

        if (flags.ForceVolunteers)
            return ForceVolunteersEventComponent.CreateForceSuppliesEvent(attacker, defender).MapEvent;

        if (flags.ForceSupplies)
            return ForceSuppliesEventComponent.CreateForceSuppliesEvent(attacker, defender).MapEvent;

        if (defender.IsSettlement)
        {
            if (defender.Settlement.IsFortification)
                return mapEventManager.StartSiegeMapEvent(attacker, defender);

            if (defender.Settlement.IsVillage)
                return RaidEventComponent.CreateRaidEvent(attacker, defender).MapEvent;

            if (defender.Settlement.IsHideout)
                return HideoutEventComponent.CreateHideoutEvent(attacker, defender, flags.ForceHideoutSendTroops).MapEvent;

            Logger.Error(
                "Proper map event type could not be determined for settlement battle. Attacker={Attacker}, Defender={Defender}",
                attacker.Name,
                defender.Name);
            return null;
        }

        if (flags.IsSallyOutAmbush)
            return SiegeAmbushEventComponent.CreateSiegeAmbushEvent(attacker, defender).MapEvent;

        if (flags.ForceBlockadeAttack)
            return BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attacker, defender, false).MapEvent;

        if (flags.ForceBlockadeSallyOutAttack)
            return BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attacker, defender, true).MapEvent;

        if (attacker.IsMobile
            && attacker.MobileParty.CurrentSettlement != null
            && attacker.MobileParty.CurrentSettlement.SiegeEvent != null)
        {
            if (attacker.MobileParty.IsTargetingPort)
                return BlockadeBattleMapEvent.CreateBlockadeBattleMapEvent(attacker, defender, true).MapEvent;

            return mapEventManager.StartSallyOutMapEvent(attacker, defender);
        }

        if (defender.IsMobile && defender.MobileParty.BesiegedSettlement != null)
            return mapEventManager.StartSiegeOutsideMapEvent(attacker, defender);

        return FieldBattleEventComponent.CreateFieldBattleEvent(attacker, defender).MapEvent;
    }
}
