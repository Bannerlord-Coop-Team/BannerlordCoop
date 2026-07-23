using Common;
using Common.Messaging;
using GameInterface.Services.Alleys.Interfaces;
using GameInterface.Services.Alleys.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Data;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Alleys.Commands;

/// <summary>
/// Debug commands for driving and inspecting alley sync. State-changing commands must be run on the
/// server (the host is authoritative and replicates to clients). Example worked against Danustica
/// (settlement town_ES1): <c>coop.debug.alley.list town_ES1</c>.
/// </summary>
public class AlleyDebugCommand
{
    private static bool TryGetAlley(string settlementId, string indexArg, out Alley alley, out Settlement settlement, out string error)
    {
        alley = null;
        settlement = Settlement.Find(settlementId);
        error = null;

        if (settlement == null)
        {
            error = $"Settlement with id '{settlementId}' not found";
            return false;
        }
        if (settlement.Alleys == null || settlement.Alleys.Count == 0)
        {
            error = $"Settlement '{settlementId}' has no alleys";
            return false;
        }
        if (!int.TryParse(indexArg, out int index) || index < 0 || index >= settlement.Alleys.Count)
        {
            error = $"Alley index must be 0..{settlement.Alleys.Count - 1}";
            return false;
        }

        alley = settlement.Alleys[index];
        return true;
    }

    [CommandLineArgumentFunction("list", "coop.debug.alley")]
    public static string List(List<string> args)
    {
        if (args.Count != 1) return "Usage: coop.debug.alley.list <settlementId>";

        Settlement settlement = Settlement.Find(args[0]);
        if (settlement == null) return $"Settlement with id '{args[0]}' not found";
        if (settlement.Alleys == null || settlement.Alleys.Count == 0) return $"'{settlement.Name}' has no alleys";

        var sb = new StringBuilder();
        sb.AppendLine($"{settlement.Name} alleys:");
        for (int i = 0; i < settlement.Alleys.Count; i++)
        {
            var alley = settlement.Alleys[i];
            string owner = alley.Owner != null ? $"{alley.Owner.Name} ({alley.Owner.StringId})" : "none";
            sb.AppendLine($"  [{i}] state={alley.State} owner={owner}");
        }
        return sb.ToString();
    }

    [CommandLineArgumentFunction("my_hero_id", "coop.debug.alley")]
    public static string MyHeroId(List<string> args)
    {
        var hero = Hero.MainHero;
        if (hero == null) return "No main hero on this instance (run this on a client, not the host)";
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return "Unable to resolve IObjectManager";
        if (!objectManager.TryGetId(hero, out var id)) return $"{hero.Name} is not registered";
        return $"{hero.Name} registry id: {id}  (pass this to the host's coop.debug.alley.set_owner)";
    }

    [CommandLineArgumentFunction("set_owner", "coop.debug.alley")]
    public static string SetOwner(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.alley.set_owner on the server (host) only";
        if (args.Count != 3) return "Usage: coop.debug.alley.set_owner <settlementId> <alleyIndex> <heroRegistryId>";

        if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return error;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return "Unable to resolve IObjectManager";

        // Resolve by the registry id (coop.debug.alley.my_hero_id on the owning client), which is the
        // same on every machine. Player-hero StringIds are generated per-machine, so a client's StringId
        // or name won't match the host's copy; fall back to those only for non-player heroes.
        if (!objectManager.TryGetObject<Hero>(args[2], out var hero))
            hero = Hero.FindFirst(h => h.StringId == args[2] || h.Name?.ToString() == args[2]);
        if (hero == null) return $"Hero '{args[2]}' not found (use the registry id from coop.debug.alley.my_hero_id on the owning client)";

        if (!objectManager.TryGetId(alley, out var alleyId)) return "Alley is not registered";
        if (!objectManager.TryGetId(hero, out var heroId)) return "Hero is not registered";

        // Drive the same authoritative take-over the in-game alley fight uses; for the cheat the granted
        // hero is both the owner and the overseer (single garrison member). AlleyManagementHandler applies it.
        TroopRosterElementData[] garrison = objectManager.TryGetId(hero.CharacterObject, out var heroCharId)
            ? new[] { new TroopRosterElementData(heroCharId, 1, 0, 0) }
            : new TroopRosterElementData[0];

        MessageBroker.Instance.Publish(alley, new RequestAcquireAlley(alleyId, heroId, heroId, garrison));

        return $"Set alley [{args[1]}] in {alley.Settlement.Name} to {hero.Name}";
    }

    [CommandLineArgumentFunction("abandon", "coop.debug.alley")]
    public static string Abandon(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.alley.abandon on the server (host) only";
        if (args.Count != 2) return "Usage: coop.debug.alley.abandon <settlementId> <alleyIndex>";

        if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return error;
        if (alley.Owner == null) return "Alley is not owned";

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return "Unable to resolve IObjectManager";
        if (!objectManager.TryGetId(alley, out var alleyId)) return "Alley is not registered";

        // Drive the authoritative abandon through the same handler the client requests use
        // (menu-style: returns the garrison to the owner's party).
        MessageBroker.Instance.Publish(alley, new RequestAbandonAlley(alleyId, fromClanScreen: false));
        return $"Abandoned alley [{args[1]}] in {alley.Settlement.Name}";
    }

    [CommandLineArgumentFunction("daily_tick", "coop.debug.alley")]
    public static string DailyTick(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.alley.daily_tick on the server (host) only";

        // Run one server daily alley pass now instead of waiting for a game day (troop decay, overseer XP,
        // dead-leader/timeout destroy, and the 1.5% attack roll over every player-owned alley).
        MessageBroker.Instance.Publish(null, new AlleyDailyTickTriggered());
        return "Ran the server alley daily tick once";
    }

    [CommandLineArgumentFunction("attack", "coop.debug.alley")]
    public static string Attack(List<string> args)
    {
        if (ModInformation.IsClient) return "Run coop.debug.alley.attack on the server (host) only";
        if (args.Count != 2) return "Usage: coop.debug.alley.attack <settlementId> <alleyIndex>";

        if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return error;
        if (alley.Owner == null || alley.Owner.IsGangLeader) return "Alley is not player-owned; only a player alley can be attacked";

        // Force an AI attack now (bypasses the daily roll). Needs a gang-occupied rival alley in the same
        // settlement to attack from; if there is none nothing happens (check coop.debug.alley.list).
        MessageBroker.Instance.Publish(alley, new ForceAlleyAttackRequested(alley));
        return $"Started an AI attack on alley [{args[1]}] in {alley.Settlement.Name}; the owner must go defend it";
    }

    [CommandLineArgumentFunction("info", "coop.debug.alley")]
    public static string Info(List<string> args)
    {
        if (args.Count != 2) return "Usage: coop.debug.alley.info <settlementId> <alleyIndex>";

        if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return error;

        var sb = new StringBuilder();
        sb.AppendLine($"Alley [{args[1]}] in {alley.Settlement.Name}");
        sb.AppendLine($"  state={alley.State}");
        sb.AppendLine($"  owner={(alley.Owner != null ? $"{alley.Owner.Name} ({alley.Owner.StringId})" : "none")}");

        if (ModInformation.IsServer &&
            ContainerProvider.TryResolve<IObjectManager>(out var objectManager) &&
            ContainerProvider.TryResolve<ISessionAlleyPlayerDataInterface>(out var sessionInterface) &&
            objectManager.TryGetId(alley, out var alleyId) &&
            sessionInterface.TryGetManagementData(alleyId, out var data))
        {
            sb.AppendLine($"  overseerId={data.OverseerId ?? "none"}");
            sb.AppendLine($"  garrison entries={data.Garrison?.Length ?? 0}");
        }

        return sb.ToString();
    }
}
