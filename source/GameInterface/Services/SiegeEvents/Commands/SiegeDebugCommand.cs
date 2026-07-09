using Autofac;
using Common;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Patches;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

using GameInterface.Services.SiegeEngines;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;
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
        Campaign.Current.SiegeEventManager.StartSiegeEvent(settlement, besieger);

        // AiMilitaryBehavior re-scores this lord every hour and, since nothing backs this forced siege,
        // flips its DefaultBehavior off BesiegeSettlement so the camp ejects it. Pin the party so that
        // hourly think is skipped and DefaultBehavior stays put; the siege still advances to an assault
        // because that is driven by the siege/encounter system, not this think.
        SiegeDebugPinPatch.Pin(besieger);

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

    // coop.debug.siege.dump_engines
    /// <summary>
    /// Dumps every active siege's engines with their co-op registry id, hitpoints, progress and aim.
    /// Read-only; run on BOTH the server and a client and compare. Matching ids with matching hitpoints
    /// means the sync works (a stale on-screen value is then a UI-refresh bug); a differing or UNREGISTERED
    /// id on the client means it is rendering a local duplicate the server's hitpoint/aim updates never reach.
    /// </summary>
    /// <param name="args">no args</param>
    /// <returns>Result of the operation as a string</returns>
    [CommandLineArgumentFunction("dump_engines", "coop.debug.siege")]
    public static string DumpEngines(List<string> args)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            return "Unable to resolve ObjectManager";
        }

        var siegeEvents = SiegeContainerLookup.ActiveSieges().ToList();
        if (siegeEvents.Count == 0)
        {
            return "No active sieges";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[{(ModInformation.IsServer ? "SERVER" : "CLIENT")}] siege engines:");

        foreach (var siegeEvent in siegeEvents)
        {
            sb.AppendLine($"Siege of {siegeEvent.BesiegedSettlement?.StringId} (SiegeEvent {IdOf(objectManager, siegeEvent)})");
            DumpSide(sb, objectManager, "ATTACKER", siegeEvent.BesiegerCamp?.SiegeEngines);
            DumpSide(sb, objectManager, "DEFENDER", siegeEvent.BesiegedSettlement?.SiegeEngines);
        }

        return sb.ToString();
    }

    private static void DumpSide(StringBuilder sb, IObjectManager objectManager, string label, SiegeEnginesContainer container)
    {
        if (container == null)
        {
            sb.AppendLine($"  {label}: no container");
            return;
        }

        DumpEngine(sb, objectManager, $"  {label} prep    ", container.SiegePreparations);
        foreach (var engine in container.DeployedSiegeEngines)
        {
            DumpEngine(sb, objectManager, $"  {label} deployed", engine);
        }
        foreach (var engine in container.ReservedSiegeEngines)
        {
            DumpEngine(sb, objectManager, $"  {label} reserve ", engine);
        }
    }

    private static void DumpEngine(StringBuilder sb, IObjectManager objectManager, string slot, SiegeEngineConstructionProgress engine)
    {
        if (engine == null) return;

        var ranged = engine.RangedSiegeEngine;
        var aim = ranged != null ? $"{ranged.CurrentTargetType}[{ranged.CurrentTargetIndex}]" : "none";
        sb.AppendLine($"{slot}: type={engine.SiegeEngine?.StringId} id={IdOf(objectManager, engine)} " +
            $"hp={engine.Hitpoints:0}/{engine.MaxHitPoints:0} prog={engine.Progress:0.00} redeploy={engine.RedeploymentProgress:0.00} aim={aim}");
    }

    private static string IdOf(IObjectManager objectManager, object obj)
    {
        return obj != null && objectManager.TryGetId(obj, out var id) ? id : "UNREGISTERED";
    }
}
