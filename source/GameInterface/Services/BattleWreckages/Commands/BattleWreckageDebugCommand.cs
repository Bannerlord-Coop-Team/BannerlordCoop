using Common.Commands;
using Common.Util;
using GameInterface.Services.BattleWreckages.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Utils.Commands;
using HarmonyLib;
using System;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.BattleWreckages.Commands;

/// <summary>
/// Debug commands for live testing battle wreckages. State-changing commands run on the server, which is authoritative
/// and replicates to clients. For example, <c>coop.debug.wreckage.create "Player Name" Normal</c> on the server puts a
/// Normal wreckage on that player's party, then <c>coop.debug.wreckage.client_state</c> on that client shows what it holds.
/// </summary>
public class BattleWreckageDebugCommand
{
    private const string WreckagePrefix = "coop.debug.wreckage";

    private static readonly string WreckageTypes = string.Join(", ",
        Enum.GetNames(typeof(BattleWreckage.WreckageType)).Where(name => name != nameof(BattleWreckage.WreckageType.Invalid)));

    private static readonly string LeaveReasons = string.Join(", ",
        Enum.GetNames(typeof(BattleWreckageCampaignBehavior.WreckageLeaveReason)));

    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    private static string GetWreckageId(IObjectManager objectManager, BattleWreckage battleWreckage)
    {
        if (battleWreckage == null) return "none";

        return objectManager.TryGetId(battleWreckage, out var id) ? id : "unregistered";
    }

    public sealed class ListWreckagesCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;

        public ListWreckagesCoopCommand(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string Prefix => WreckagePrefix;

        public string Name => "list";

        public string Description => "Lists battle wreckages with their id, type, state and time left.";

        public CoopCommandSide Side => CoopCommandSide.Both;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var wreckages = Campaign.Current.Wreckages;
            if (wreckages.Count == 0) return Succeeded("No battle wreckages");

            var sb = new StringBuilder();
            foreach (var battleWreckage in wreckages)
            {
                var hoursLeft = (battleWreckage.DestroyTime - CampaignTime.Now).ToHours;
                sb.AppendLine($"{GetWreckageId(objectManager, battleWreckage)} '{battleWreckage.Name}' type={battleWreckage.WreckageTypeCategory} " +
                    $"land={battleWreckage.Position.IsOnLand} investigated={battleWreckage.IsInvestigated} visible={battleWreckage.IsVisible} " +
                    $"destroyInHours={hoursLeft:0.#} wounded={battleWreckage.TotalNumberOfWoundedInBattle} died={battleWreckage.TotalNumberOfDiedInBattle}");
            }
            return Succeeded(sb.ToString());
        }
    }

    public sealed class ListEncountersCoopCommand : ICoopCommand
    {
        private readonly IBattleWreckageEncounterTracker encounterTracker;
        private readonly IObjectManager objectManager;

        public ListEncountersCoopCommand(IBattleWreckageEncounterTracker encounterTracker, IObjectManager objectManager)
        {
            this.encounterTracker = encounterTracker;
            this.objectManager = objectManager;
        }

        public string Prefix => WreckagePrefix;

        public string Name => "encounters";

        public string Description => "Lists the server's tracked wreckage encounters per player.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var encounters = encounterTracker.Encounters;
            if (encounters.Count == 0) return Succeeded("No wreckage encounters");

            var sb = new StringBuilder();
            foreach (var encounter in encounters)
            {
                sb.AppendLine($"{encounter.Hero.Name} ({encounter.Hero.StringId}) wreckage={GetWreckageId(objectManager, encounter.BattleWreckage)} " +
                    $"started={encounter.InvestigationStartTime} " +
                    $"durationHours={encounter.RequiredInvestigationDuration.ToHours:0.#} lastInteraction={encounter.LastInteractionTime} " +
                    $"override='{encounter.OverriddenConsequenceId}' accepted={encounter.IsOverriddenConsequenceAccepted} " +
                    $"recoverCount={encounter.RecoverTroopCountsToAdd} investigating={encounter.IsInvestigating}");
            }
            return Succeeded(sb.ToString());
        }
    }

    public sealed class CreateWreckageCoopCommand : ICoopCommand
    {
        private const int DefaultCasualties = 10;

        private readonly ICommandObjectResolver resolver;
        private readonly IObjectManager objectManager;

        public CreateWreckageCoopCommand(ICommandObjectResolver resolver, IObjectManager objectManager)
        {
            this.resolver = resolver;
            this.objectManager = objectManager;
        }

        public string Prefix => WreckagePrefix;

        public string Name => "create";

        public string Description => "Creates a wreckage on a hero's party, with casualties copied from that party's troops.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero", "Hero name or id whose party the wreckage is placed on; quote names with spaces."),
            new ExpectedArgs("type", WreckageTypes + "."),
            new ExpectedArgs("casualties", $"Wounded and died troops to add, taken from the party's regular troops. Defaults to {DefaultCasualties}.", isRequired: false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var error)) return Failed(error);
            if (hero.PartyBelongedTo == null) return Failed($"{hero.Name} has no party");

            if (!Enum.TryParse(args[1], ignoreCase: true, out BattleWreckage.WreckageType wreckageType) ||
                wreckageType == BattleWreckage.WreckageType.Invalid)
            {
                return Failed($"'{args[1]}' is not one of {WreckageTypes}");
            }

            var casualties = DefaultCasualties;
            if (args.Count > 2)
            {
                if (!int.TryParse(args[2], out casualties) || casualties < 0) return Failed($"'{args[2]}' is not a casualty count");
            }

            var behavior = Campaign.Current.GetCampaignBehavior<BattleWreckageCampaignBehavior>();
            if (behavior == null) return Failed("BattleWreckageCampaignBehavior is unavailable");

            // Vanilla SpawnWreckage sets the destroy time for the type and appends the wreckage to Campaign.Current.Wreckages
            var party = hero.PartyBelongedTo;
            behavior.SpawnWreckage(CreateStandInMapEvent(party), wreckageType);
            var battleWreckage = Campaign.Current.Wreckages.Last();

            // The attacker won, so its roster takes the wounded and the defender's the dead. Wounded troops let recover
            // troops roll and both rosters give trade goods something to loot
            var remaining = casualties;
            foreach (var element in party.MemberRoster.GetTroopRoster().Where(element => !element.Character.IsHero))
            {
                if (remaining == 0) break;

                var count = Math.Min(remaining, element.Number);
                battleWreckage.AttackerWoundedInBattle.AddToCounts(element.Character, count, woundedCount: count);
                battleWreckage.DefenderDiedInBattle.AddToCounts(element.Character, count);
                remaining -= count;
            }

            return Succeeded($"Created {wreckageType} wreckage {GetWreckageId(objectManager, battleWreckage)} '{battleWreckage.Name}' " +
                $"on {party.Name} with {casualties - remaining} casualties");
        }

        // The wreckage constructor only reads the map event's position, sides, start time and winner, so a stand in is
        // enough. Skipping the constructors keeps it and its sides out of the registries, which would otherwise send them
        // to every client. The party fights itself and the attacker wins
        private static MapEvent CreateStandInMapEvent(MobileParty party)
        {
            var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
            mapEvent._sides = new[]
            {
                CreateSide(mapEvent, BattleSideEnum.Defender, party.Party),
                CreateSide(mapEvent, BattleSideEnum.Attacker, party.Party),
            };
            mapEvent.Position = party.Position;
            mapEvent._mapEventStartTime = CampaignTime.Now;
            mapEvent._battleState = BattleState.AttackerVictory;
            return mapEvent;
        }

        // The fields vanilla's MapEventSide constructor sets, the readonly ones the same way MapEventSideRegistry does
        private static MapEventSide CreateSide(MapEvent mapEvent, BattleSideEnum missionSide, PartyBase leaderParty)
        {
            var side = ObjectHelper.SkipConstructor<MapEventSide>();
            AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent)).SetValue(side, mapEvent);
            AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._battleParties)).SetValue(side, new MBList<MapEventParty>());
            side.LeaderParty = leaderParty;
            side.MissionSide = missionSide;
            side._mapFaction = leaderParty.MapFaction;
            return side;
        }
    }

    public sealed class DestroyWreckageCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;
        private readonly IBattleWreckageEncounterTracker encounterTracker;

        public DestroyWreckageCoopCommand(IObjectManager objectManager, IBattleWreckageEncounterTracker encounterTracker)
        {
            this.objectManager = objectManager;
            this.encounterTracker = encounterTracker;
        }

        public string Prefix => WreckagePrefix;

        public string Name => "destroy";

        public string Description => "Destroys a wreckage by id, which the registry replicates to clients.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("wreckage_id", "Wreckage id from coop.debug.wreckage.list."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!objectManager.TryGetObject(args[0], out BattleWreckage battleWreckage)) return Failed($"No wreckage with id '{args[0]}'");

            // Vanilla never destroys a wreckage that's being investigated, the hourly tick skips them too
            var encounter = encounterTracker.Encounters.FirstOrDefault(tracked => tracked.BattleWreckage == battleWreckage);
            if (encounter != null)
            {
                return Failed($"{encounter.Hero.Name} is investigating {args[0]}, run {WreckagePrefix}.leave first");
            }

            battleWreckage.DestroyWreckage();
            return Succeeded($"Destroyed wreckage {args[0]}");
        }
    }

    public sealed class LeaveWreckageEncounterCoopCommand : ICoopCommand
    {
        private readonly ICommandObjectResolver resolver;
        private readonly IBattleWreckageEncounterTracker encounterTracker;
        private readonly IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface;

        public LeaveWreckageEncounterCoopCommand(
            ICommandObjectResolver resolver,
            IBattleWreckageEncounterTracker encounterTracker,
            IBattleWreckageCampaignBehaviorInterface battleWreckageCampaignBehaviorInterface)
        {
            this.resolver = resolver;
            this.encounterTracker = encounterTracker;
            this.battleWreckageCampaignBehaviorInterface = battleWreckageCampaignBehaviorInterface;
        }

        public string Prefix => WreckagePrefix;

        public string Name => "leave";

        public string Description => "Makes a player leave their wreckage encounter, e.g. PlayerInterrupted or WreckageExpired.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("hero", "Player hero name or id; quote names with spaces."),
            new ExpectedArgs("reason", LeaveReasons + "."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!resolver.TryResolveHero(args[0], out var hero, out var error)) return Failed(error);
            if (!Enum.TryParse(args[1], ignoreCase: true, out BattleWreckageCampaignBehavior.WreckageLeaveReason reason))
            {
                return Failed($"'{args[1]}' is not one of {LeaveReasons}");
            }

            if (!encounterTracker.TryGetEncounter(hero, out _)) return Failed($"{hero.Name} has no wreckage encounter");

            battleWreckageCampaignBehaviorInterface.LeaveWreckageEncounter(hero, reason);
            return Succeeded($"{hero.Name} left their wreckage encounter with {reason}");
        }
    }

    public sealed class ClientStateCoopCommand : ICoopCommand
    {
        private readonly IObjectManager objectManager;

        public ClientStateCoopCommand(IObjectManager objectManager)
        {
            this.objectManager = objectManager;
        }

        public string Prefix => WreckagePrefix;

        public string Name => "client_state";

        public string Description => "Shows this client's vanilla wreckage encounter state.";

        public CoopCommandSide Side => CoopCommandSide.Client;

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            var behavior = Campaign.Current.GetCampaignBehavior<BattleWreckageCampaignBehavior>();
            if (behavior == null) return Failed("BattleWreckageCampaignBehavior is unavailable");

            return Succeeded($"wreckage={GetWreckageId(objectManager, behavior._encounteredBattleWreckage)} " +
                $"interrupted={behavior._isEncounteredWreckageInterrupted} started={behavior._playerInvestigationStartTime} " +
                $"durationHours={behavior._requiredInvestigationDuration.ToHours:0.#} override='{behavior._currentOverriddenConsequenceId}' " +
                $"accepted={behavior._isOverriddenConsequenceAccepted} recoverCount={behavior._recoverTroopCountsToAdd} " +
                $"lootedTroops={behavior._lootedTroops.TotalManCount} lootedItems={behavior._lootedItems.Count} " +
                $"gold={behavior._lootedGoldAmount} explanations={behavior._consequenceExplanations.Count}");
        }
    }
}
