using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

using GameInterface.Services.SiegeEngines;
namespace GameInterface.Services.SiegeEvents.Commands;

public class SiegeDebugCommand
{
    // coop.debug.siege.start
    /// <summary>
    /// Starts a siege of a settlement, led by the given party or the strongest hostile lord party when
    /// none is given. Server only; the siege replicates to clients.
    /// </summary>
    /// <param name="args">first arg : settlementId ; optional second arg : besieger partyId</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("start", "coop.debug.siege")]
    public static string StartSiege(List<string> args)
    {
        if (args.Count < 1 || args.Count > 2)
        {
            return "Usage: coop.debug.siege.start <settlementId> [besiegerPartyId]";
        }

        if (ModInformation.IsClient)
        {
            return "This command can only be used by the server";
        }

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        if (!objectManager.TryGetObject<Settlement>(args[0], out var settlement))
        {
            return $"Settlement with id {args[0]} not found";
        }

        if (!settlement.IsFortification)
        {
            return $"{settlement.Name} is not a fortification";
        }

        if (settlement.SiegeEvent != null)
        {
            return $"{settlement.Name} is already under siege";
        }

        MobileParty besieger;
        if (args.Count == 2)
        {
            if (!objectManager.TryGetObject(args[1], out besieger))
            {
                return $"Party with id {args[1]} not found";
            }
        }
        else
        {
            besieger = MobileParty.AllLordParties
                .Where(party => party.MapFaction?.IsAtWarWith(settlement.MapFaction) == true
                    && party.LeaderHero != null && party.CurrentSettlement == null
                    && party.MapEvent == null && party.BesiegerCamp == null && party.Army == null)
                .OrderByDescending(party => party.Party.CalculateCurrentStrength())
                .FirstOrDefault();
            if (besieger == null)
            {
                return $"No hostile lord party available to besiege {settlement.Name}; pass a partyId explicitly";
            }
        }

        // Put the besieger at the gate and commit its AI to the siege.
        besieger.Position = settlement.GatePosition;
        besieger.SetMoveBesiegeSettlement(settlement, MobileParty.NavigationType.Default);

        // A lone forced besieger is re-tasked away by AiMilitaryBehavior's hourly think, which doesn't
        // back this siege (DoNotMakeNewDecisions only gates short-term initiative, not the hourly
        // re-task), so it wanders off after an hour. Wrap it in a Besieger army — the vanilla mechanism
        // for a held AI siege — so the think stops re-deciding and it stays. A minor-faction lord with
        // no kingdom just gets the behavior flags, which hold only briefly.
        if (besieger.Army == null && besieger.LeaderHero != null && besieger.MapFaction is Kingdom kingdom)
        {
            kingdom.CreateArmy(besieger.LeaderHero, settlement, Army.ArmyTypes.Besieger);
        }

        Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besieger);
        besieger.Ai.SetDoNotMakeNewDecisions(true);

        return $"{besieger.Name} ({besieger.StringId}) is now besieging {settlement.Name}";
    }

    // coop.debug.siege.list
    /// <summary>
    /// Lists the active sieges with their preparation progress and deployed engine counts.
    /// </summary>
    /// <param name="args">no args</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("list", "coop.debug.siege")]
    public static string ListSieges(List<string> args)
    {
        var siegeEvents = SiegeContainerLookup.ActiveSieges().ToList();
        if (siegeEvents.Count == 0)
        {
            return "No active sieges";
        }

        var sb = new StringBuilder();
        foreach (var siegeEvent in siegeEvents)
        {
            var camp = siegeEvent.BesiegerCamp;
            sb.AppendLine($"{siegeEvent.BesiegedSettlement?.Name} ({siegeEvent.BesiegedSettlement?.StringId}): " +
                $"leader={camp?.LeaderParty?.Name} preparation={camp?.SiegeEngines?.SiegePreparations?.Progress:0.00} " +
                $"attackerEngines={camp?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0} " +
                $"defenderEngines={siegeEvent.BesiegedSettlement?.SiegeEngines?.DeployedSiegeEngines?.Count ?? 0} " +
                $"strategy={camp?.SiegeStrategy?.Name}");
        }

        return sb.ToString();
    }
}
