using Common.Commands;
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

namespace GameInterface.Services.Alleys.Commands;

/// <summary>
/// Debug commands for driving and inspecting alley sync. State-changing commands must be run on the
/// server (the host is authoritative and replicates to clients). Example worked against Danustica
/// (settlement town_ES1): <c>coop.debug.alley.list town_ES1</c>.
/// </summary>
public class AlleyDebugCommand
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

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

    public sealed class AlleyListCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "list";

        public string Description => "Lists alleys in a settlement.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            Settlement settlement = Settlement.Find(args[0]);
            if (settlement == null) return Failed($"Settlement with id '{args[0]}' not found");
            if (settlement.Alleys == null || settlement.Alleys.Count == 0) return Failed($"'{settlement.Name}' has no alleys");

            var sb = new StringBuilder();
            sb.AppendLine($"{settlement.Name} alleys:");
            for (int i = 0; i < settlement.Alleys.Count; i++)
            {
                var alley = settlement.Alleys[i];
                string owner = alley.Owner != null ? $"{alley.Owner.Name} ({alley.Owner.StringId})" : "none";
                sb.AppendLine($"  [{i}] state={alley.State} owner={owner}");
            }
            return Succeeded(sb.ToString());
        }
    }

    public sealed class AlleyMyHeroIdCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "my_hero_id";

        public string Description => "Reports the local main hero registry id.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var hero = Hero.MainHero;
            if (hero == null) return Failed("No main hero on this instance (run this on a client, not the host)");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return Failed("Unable to resolve IObjectManager");
            if (!objectManager.TryGetId(hero, out var id)) return Failed($"{hero.Name} is not registered");
            return Succeeded($"{hero.Name} registry id: {id}  (pass this to the host's coop.debug.alley.set_owner)");
        }
    }

    public sealed class AlleySetOwnerCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "set_owner";

        public string Description => "Sets an alley owner on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
            new ExpectedArgs("hero_registry_id", "The registered owner hero id."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.alley.set_owner on the server (host) only");

            if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return Failed(error);

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return Failed("Unable to resolve IObjectManager");

            // Resolve by the registry id (coop.debug.alley.my_hero_id on the owning client), which is the
            // same on every machine. Player-hero StringIds are generated per-machine, so a client's StringId
            // or name won't match the host's copy; fall back to those only for non-player heroes.
            if (!objectManager.TryGetObject<Hero>(args[2], out var hero))
                hero = Hero.FindFirst(h => h.StringId == args[2] || h.Name?.ToString() == args[2]);
            if (hero == null) return Failed($"Hero '{args[2]}' not found (use the registry id from coop.debug.alley.my_hero_id on the owning client)");

            if (!objectManager.TryGetId(alley, out var alleyId)) return Failed("Alley is not registered");
            if (!objectManager.TryGetId(hero, out var heroId)) return Failed("Hero is not registered");

            // Drive the same authoritative take-over the in-game alley fight uses; for the cheat the granted
            // hero is both the owner and the overseer (single garrison member). AlleyManagementHandler applies it.
            TroopRosterElementData[] garrison = objectManager.TryGetId(hero.CharacterObject, out var heroCharId)
                ? new[] { new TroopRosterElementData(heroCharId, 1, 0, 0) }
                : new TroopRosterElementData[0];

            MessageBroker.Instance.Publish(alley, new RequestAcquireAlley(alleyId, heroId, heroId, garrison));

            return Succeeded($"Set alley [{args[1]}] in {alley.Settlement.Name} to {hero.Name}");
        }
    }

    public sealed class AlleyAbandonCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "abandon";

        public string Description => "Abandons a player-owned alley on the server.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.alley.abandon on the server (host) only");

            if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return Failed(error);
            if (alley.Owner == null) return Failed("Alley is not owned");

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return Failed("Unable to resolve IObjectManager");
            if (!objectManager.TryGetId(alley, out var alleyId)) return Failed("Alley is not registered");

            // Drive the authoritative abandon through the same handler the client requests use
            // (menu-style: returns the garrison to the owner's party).
            MessageBroker.Instance.Publish(alley, new RequestAbandonAlley(alleyId, fromClanScreen: false));
            return Succeeded($"Abandoned alley [{args[1]}] in {alley.Settlement.Name}");
        }
    }

    public sealed class AlleyDailyTickCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "daily_tick";

        public string Description => "Runs one authoritative alley daily tick.";

        public IExpectedArgs[] ExpectedArgs { get; } = System.Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.alley.daily_tick on the server (host) only");

            // Run one server daily alley pass now instead of waiting for a game day (troop decay, overseer XP,
            // dead-leader/timeout destroy, and the 1.5% attack roll over every player-owned alley).
            MessageBroker.Instance.Publish(null, new AlleyDailyTickTriggered());
            return Succeeded("Ran the server alley daily tick once");
        }
    }

    public sealed class AlleyAttackCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "attack";

        public string Description => "Starts an AI attack against a player-owned alley.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run coop.debug.alley.attack on the server (host) only");

            if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return Failed(error);
            if (alley.Owner == null || alley.Owner.IsGangLeader) return Failed("Alley is not player-owned; only a player alley can be attacked");

            // Force an AI attack now (bypasses the daily roll). Needs a gang-occupied rival alley in the same
            // settlement to attack from; if there is none nothing happens (check coop.debug.alley.list).
            MessageBroker.Instance.Publish(alley, new ForceAlleyAttackRequested(alley));
            return Succeeded($"Started an AI attack on alley [{args[1]}] in {alley.Settlement.Name}; the owner must go defend it");
        }
    }

    public sealed class AlleyInfoCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.alley";

        public string Name => "info";

        public string Description => "Reports state for an alley.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("settlement_id", "The settlement StringId."),
            new ExpectedArgs("alley_index", "The zero-based alley index."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!TryGetAlley(args[0], args[1], out var alley, out _, out var error)) return Failed(error);

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
                var lastRecruitTime = new CampaignTime(data.LastRecruitTimeTicks);
                sb.AppendLine($"  lastRecruitTimeTicks={data.LastRecruitTimeTicks}");
                sb.AppendLine($"  recruitCooldownElapsedDays={lastRecruitTime.ElapsedDaysUntilNow:R}");
            }

            return Succeeded(sb.ToString());
        }
    }
}
