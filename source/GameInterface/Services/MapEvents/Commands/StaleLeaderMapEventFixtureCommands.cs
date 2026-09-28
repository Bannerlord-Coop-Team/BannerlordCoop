#if DEBUG
using Common;
using Common.Commands;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.Commands;

internal class StaleLeaderMapEventFixtureCommands
{
    private static readonly ILogger Logger = LogManager.GetLogger<StaleLeaderMapEventFixtureCommands>();
    private static Fixture fixture;

    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    public sealed class PrepareCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "stale_leader_fixture_prepare";
        public string Description => "Stages an AI party outside Danustica for the empty-side update fixture.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run this command on the server.");
            if (fixture != null) return Failed("The stale-leader fixture is already active.");
            if (Campaign.Current == null) return Failed("No campaign is loaded.");
            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot) ||
                !ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve fixture services.");

            var danustica = Settlement.Find("town_ES1");
            if (danustica?.MapFaction == null) return Failed("Danustica (town_ES1) is unavailable.");

            MobileParty survivor = null;
            MobileParty banditTemplate = null;
            foreach (var lord in MobileParty.All.Where(party =>
                         party.IsActive && party.IsLordParty && !party.IsPlayerParty() &&
                         party.LeaderHero != null && party.Ai != null && party.Army == null &&
                         party.MapEvent == null && party.CurrentSettlement == null &&
                         party.BesiegedSettlement == null && !party.IsTransitionInProgress &&
                         !party.IsCurrentlyAtSea && !party.IsCurrentlyUsedByAQuest &&
                         party.MemberRoster.TotalHealthyCount > 0 &&
                         party.MapFaction != null &&
                         !FactionManager.IsAtWarAgainstFaction(party.MapFaction, danustica.MapFaction))
                     .OrderBy(party => party.Position.ToVec2().DistanceSquared(danustica.GatePosition.ToVec2())))
            {
                var bandit = MobileParty.All.FirstOrDefault(party =>
                    party.IsActive && party.IsBandit && party.ActualClan != null &&
                    party.PartyComponent is BanditPartyComponent && party.MapFaction != null &&
                    FactionManager.IsAtWarAgainstFaction(lord.MapFaction, party.MapFaction) &&
                    party.MemberRoster.GetTroopRoster().Any(troop => !troop.Character.IsHero && troop.Number > 0));
                if (bandit == null || !behaviorSnapshot.TryCreate(lord, out _)) continue;
                survivor = lord;
                banditTemplate = bandit;
                break;
            }

            if (survivor == null)
                return Failed("No available AI lord and hostile bandit pair can enter Danustica.");
            if (!behaviorSnapshot.TryCreate(survivor, out var originalBehavior) ||
                !objectManager.TryGetId(survivor, out var survivorId))
                return Failed("Unable to capture the AI party's registered movement state.");

            fixture = new Fixture(survivor, banditTemplate, danustica, originalBehavior);
            try
            {
                survivor.Position = new CampaignVec2(
                    new Vec2(danustica.GatePosition.X - 1.5f, danustica.GatePosition.Y), true);
                survivor.SetMoveModeHold();
                survivor.ResetNavigationToHold();
                PublishPosition(survivor);
                return Succeeded($"Staged AI party outside Danustica|party={survivor.StringId}|" +
                    $"partyId={survivorId}|settlement=town_ES1|" +
                    $"position={survivor.Position.X:R},{survivor.Position.Y:R}.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to stage stale-leader fixture");
                try
                {
                    RestoreFixture(fixture, behaviorSnapshot);
                    fixture = null;
                }
                catch (Exception restoreError)
                {
                    return Failed($"Fixture staging failed: {e.Message}. Restore failed: {restoreError.Message}.");
                }
                return Failed($"Fixture staging failed: {e.Message}. Baseline restored.");
            }
        }
    }

    public sealed class RunCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "stale_leader_fixture_run";
        public string Description => "Runs the production map-event update against an empty defender side.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run this command on the server.");
            if (fixture == null) return Failed("Prepare the stale-leader fixture first.");
            if (fixture.MapEvent != null) return Failed("The stale-leader fixture already ran.");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve the object manager.");
            if (!fixture.Survivor.IsActive || fixture.Survivor.MapEvent != null ||
                fixture.Survivor.CurrentSettlement != null || !fixture.BanditTemplate.IsActive)
                return Failed("The prepared parties are no longer available; restore the fixture.");

            try
            {
                var banditComponent = (BanditPartyComponent)fixture.BanditTemplate.PartyComponent;
                var troop = fixture.BanditTemplate.MemberRoster.GetTroopRoster()
                    .First(element => !element.Character.IsHero && element.Number > 0).Character;
                fixture.Bandit = BanditPartyComponent.CreateBanditParty(
                    $"coop_debug_3641_bandit_{Guid.NewGuid():N}",
                    fixture.BanditTemplate.ActualClan,
                    banditComponent.Hideout,
                    isBossParty: false,
                    pt: null,
                    new CampaignVec2(new Vec2(
                        fixture.Survivor.Position.X - 0.4f,
                        fixture.Survivor.Position.Y), true));
                fixture.Bandit.MemberRoster.AddToCounts(troop, 20);
                fixture.Bandit.SetMoveModeHold();

                fixture.MapEvent = MapEventBattleFactory.CreateMapEvent(
                    fixture.Survivor.Party, fixture.Bandit.Party, default);
                if (fixture.MapEvent == null || !fixture.MapEvent.IsFieldBattle ||
                    fixture.MapEvent.AttackerSide.Parties.Count != 1 ||
                    fixture.MapEvent.DefenderSide.Parties.Count != 1 ||
                    fixture.MapEvent.DefenderSide.Parties[0].Party != fixture.Bandit.Party ||
                    fixture.MapEvent.IsPlayerMapEvent)
                    throw new InvalidOperationException("The AI-only field battle did not have one party per side.");

                var defender = fixture.MapEvent.DefenderSide;
                var removedParty = defender._battleParties[0];
                defender.InvalidateSimulationSetup();
                defender._battleParties.Clear();
                fixture.Bandit.Party._mapEventSide = null;
                // An unregistered shell reproduces the stale reference without creating live roster objects.
                defender.LeaderParty = ObjectHelper.SkipConstructor<PartyBase>();
                if (defender.LeaderParty.MapFaction != null)
                    throw new InvalidOperationException("The stale defender leader still has a faction.");
                if (objectManager.Contains(defender.LeaderParty))
                    throw new InvalidOperationException("The stale defender leader unexpectedly has a registry id.");
                if (!objectManager.TryGetId(fixture.MapEvent, out var eventId))
                    throw new InvalidOperationException("The map event has no registry id before Update.");
                fixture.EventId = eventId;
                MessageBroker.Instance.Publish(defender, new MapEventPartyRemoved(defender, removedParty));
                Logger.Information("Stale-leader fixture before Update: event={EventId}, registryId={RegistryId}, defenderParties={PartyCount}, " +
                    "leaderFaction={LeaderFaction}, survivor={Survivor}", fixture.MapEvent.StringId, eventId,
                    defender._battleParties.Count, defender.LeaderParty.MapFaction, fixture.Survivor.StringId);

                // Keep patches active: this is the real server update entry point.
                fixture.MapEvent.Update();
                if (!fixture.MapEvent.IsFinalized || fixture.Survivor.MapEvent != null)
                    throw new InvalidOperationException("The update did not finalize the event and release the AI party.");

                if (fixture.Bandit.IsActive)
                    DestroyPartyAction.Apply(null, fixture.Bandit);
                fixture.Survivor.SetMoveGoToSettlement(
                    fixture.Settlement, MobileParty.NavigationType.Default, isTargetingThePort: false);
                MessageBroker.Instance.Publish(typeof(StaleLeaderMapEventFixtureCommands),
                    new PartyBehaviorChangeAttempted(fixture.Survivor));
                return Succeeded($"Empty-defender update completed|party={fixture.Survivor.StringId}|" +
                    $"event={fixture.MapEvent.StringId}|eventId={fixture.EventId}|" +
                    $"defenderPartiesBefore=0|leaderFactionBefore=none|leaderRegisteredBefore=false|" +
                    $"finalized={fixture.MapEvent.IsFinalized}|partyMapEvent=none|target=town_ES1.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Stale-leader fixture update failed");
                return Failed($"Stale-leader fixture failed: {e.GetType().Name}: {e.Message}. " +
                    $"event={fixture.MapEvent?.StringId ?? "none"}. Run fixture_state, then fixture_restore.");
            }
        }
    }

    public sealed class StateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "stale_leader_fixture_state";
        public string Description => "Reports the party and event on the server or a client.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("party_string_id", "The AI party StringId.", true),
            new ExpectedArgs("event_string_id", "The map event StringId.", true),
            new ExpectedArgs("event_registry_id", "The event registry id captured before Update.", true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (Campaign.Current == null) return Failed("No campaign is loaded.");
            var party = Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[0]);
            if (party == null) return Failed($"AI party {args[0]} was not found.");
            var mapEvent = Campaign.Current.MapEventManager.MapEvents
                .FirstOrDefault(candidate => candidate.StringId == args[1]);
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
                return Failed("Unable to resolve the object manager.");
            var eventRegistered = objectManager.TryGetObject<MapEvent>(args[2], out var registeredEvent);
            return Succeeded($"party={party.StringId}|active={party.IsActive}|" +
                $"mapEvent={party.MapEvent?.StringId ?? "none"}|" +
                $"eventPresent={mapEvent != null}|eventRegistered={eventRegistered}|" +
                $"eventIdentityMatches={eventRegistered && ReferenceEquals(mapEvent, registeredEvent)}|" +
                $"eventFinalized={mapEvent?.IsFinalized.ToString() ?? "absent"}|" +
                $"defenderParties={(mapEvent?.DefenderSide?.Parties.Count.ToString() ?? "absent")}|" +
                $"settlement={party.CurrentSettlement?.StringId ?? "none"}|" +
                $"target={party.TargetSettlement?.StringId ?? "none"}|" +
                $"moveMode={party.PartyMoveMode}|" +
                $"position={party.Position.X:R},{party.Position.Y:R}|" +
                $"campaignTicks={CampaignTime.Now.NumTicks}");
        }
    }

    public sealed class RestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "stale_leader_fixture_restore";
        public string Description => "Restores the AI party and removes fixture objects.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsClient) return Failed("Run this command on the server.");
            if (fixture == null) return Failed("The stale-leader fixture is not active.");
            if (!ContainerProvider.TryResolve<IMobilePartyBehaviorSnapshot>(out var behaviorSnapshot))
                return Failed("Unable to resolve movement restore service.");
            try
            {
                var restored = fixture;
                RestoreFixture(restored, behaviorSnapshot);
                fixture = null;
                return Succeeded($"Stale-leader fixture restored|party={restored.Survivor.StringId}|" +
                    $"mapEvent={restored.Survivor.MapEvent?.StringId ?? "none"}|" +
                    $"settlement={restored.Survivor.CurrentSettlement?.StringId ?? "none"}.");
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to restore stale-leader fixture");
                return Failed($"Fixture restore failed: {e.Message}. Retry the restore command.");
            }
        }
    }

    private static void RestoreFixture(Fixture current, IMobilePartyBehaviorSnapshot behaviorSnapshot)
    {
        if (current.MapEvent != null && !current.MapEvent.IsFinalized)
            current.MapEvent.FinalizeEvent();
        if (current.Survivor.MapEvent != null)
            throw new InvalidOperationException("The AI party is still in a map event.");
        if (current.Bandit?.IsActive == true)
            DestroyPartyAction.Apply(null, current.Bandit);
        if (current.Survivor.CurrentSettlement != null)
            LeaveSettlementAction.ApplyForParty(current.Survivor);
        if (current.Survivor.CurrentSettlement != null)
            throw new InvalidOperationException("The AI party did not leave its fixture settlement.");
        current.Survivor.Position = current.OriginalBehavior.PartyPosition;
        if (!behaviorSnapshot.TryApply(current.Survivor, current.OriginalBehavior, out _))
            throw new InvalidOperationException("Unable to restore the AI party's movement state.");
        if (current.Survivor.Position.ToVec2().DistanceSquared(
                current.OriginalBehavior.PartyPosition.ToVec2()) > 0.0001f)
            throw new InvalidOperationException("The AI party did not return to its original position.");
        PublishPosition(current.Survivor);
    }

    private static void PublishPosition(MobileParty party) =>
        MessageBroker.Instance.Publish(typeof(StaleLeaderMapEventFixtureCommands),
            new PartyBehaviorChangeAttempted(party, forcePosition: true,
                isCurrentlyAtSea: party.IsCurrentlyAtSea));

    private sealed class Fixture
    {
        public readonly MobileParty Survivor;
        public readonly MobileParty BanditTemplate;
        public readonly Settlement Settlement;
        public readonly PartyBehaviorUpdateData OriginalBehavior;
        public MobileParty Bandit;
        public MapEvent MapEvent;
        public string EventId;

        public Fixture(MobileParty survivor, MobileParty banditTemplate, Settlement settlement,
            PartyBehaviorUpdateData originalBehavior)
        {
            Survivor = survivor;
            BanditTemplate = banditTemplate;
            Settlement = settlement;
            OriginalBehavior = originalBehavior;
        }
    }
}
#endif
