#if DEBUG
using Autofac;
using Common;
using Common.Commands;
using Common.Messaging;
using GameInterface.Services.Armies;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MapEvents.Commands;

internal static class PeaceBattleFixtureCommands
{
    private static Fixture fixture;

    private static CoopCommandResult Succeeded(string output) => new CoopCommandResult(true, output);
    private static CoopCommandResult Failed(string output) => new CoopCommandResult(false, output, "command_failed");

    public sealed class StartCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "peace_battle_fixture_start";
        public string Description => "Stages a defender-reinforcement or partial-peace army battle.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("mode", "Use partial for allied attackers; omit for defender reinforcement.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer) return Failed("Run this command on the server.");
            if (fixture != null) return Failed("Restore the existing peace-battle fixture first.");
            if (Campaign.Current == null) return Failed("No campaign is loaded.");
            if (args.Count > 1 || (args.Count == 1 && args[0] != "partial"))
                return Failed("Mode must be partial, or omit it for defender reinforcement.");
            var partialMode = args.Count == 1;
            if (!TryServices(out var objects, out var behavior, out var time, out _, out var error))
                return Failed(error);
            if (time.GetTimeControl() != TimeControlEnum.Pause)
                return Failed("Pause campaign time before staging the peace-battle fixture.");

            var battania = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == "battania");
            var vlandia = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == "vlandia");
            var wolfskins = Clan.All.FirstOrDefault(clan => clan.StringId == "wolfskins");
            var battanianTarget = Settlement.Find("town_B1");
            var vlandianTarget = Settlement.Find("town_V1");
            if (battania == null || vlandia == null || wolfskins == null ||
                battanianTarget == null || vlandianTarget == null)
                return Failed("The required Battania, Vlandia, Wolfskins, or army target was not found.");
            if (wolfskins.Kingdom != null)
                return Failed("Wolfskins must be an independent faction for this three-faction fixture.");
            if (!partialMode && FactionManager.IsAtWarAgainstFaction(vlandia, wolfskins))
                return Failed("Vlandia and Wolfskins must be at peace so the reinforcement can join the defender.");

            var battanians = SelectParties(battania, 2);
            var vlandians = SelectParties(vlandia, 2);
            var wolves = SelectParties(wolfskins, 1);
            if (battanians.Length != 2 || vlandians.Length != 2 || wolves.Length != 1)
                return Failed("Need two available Battanian lords, two Vlandian lords, and one Wolfskins party outside battles and armies.");

            var parties = new[] { battanians[0], battanians[1], wolves[0], vlandians[0], vlandians[1] };
            var snapshots = new List<PartySnapshot>();
            foreach (var party in parties)
            {
                if (!behavior.TryCreate(party, out var savedBehavior))
                    return Failed($"Could not capture movement state for {party.StringId}.");
                snapshots.Add(new PartySnapshot(party, savedBehavior));
            }

            var pending = new Fixture
            {
                Battania = battania,
                Vlandia = vlandia,
                Wolfskins = wolfskins,
                PartialMode = partialMode,
                Parties = snapshots.ToArray(),
                BattaniaWolfskinsAtWar = FactionManager.IsAtWarAgainstFaction(battania, wolfskins),
                BattaniaVlandiaAtWar = FactionManager.IsAtWarAgainstFaction(battania, vlandia),
                VlandiaWolfskinsAtWar = FactionManager.IsAtWarAgainstFaction(vlandia, wolfskins),
            };
            fixture = pending;
            try
            {
                if (!pending.BattaniaWolfskinsAtWar) DeclareWarAction.ApplyByDefault(battania, wolfskins);
                if (partialMode)
                {
                    if (pending.BattaniaVlandiaAtWar) MakePeaceAction.Apply(battania, vlandia);
                    if (!pending.VlandiaWolfskinsAtWar) DeclareWarAction.ApplyByDefault(vlandia, wolfskins);
                }
                else if (!pending.BattaniaVlandiaAtWar)
                    DeclareWarAction.ApplyByDefault(battania, vlandia);
                if (!FactionManager.IsAtWarAgainstFaction(battania, wolfskins) ||
                    FactionManager.IsAtWarAgainstFaction(battania, vlandia) != !partialMode ||
                    FactionManager.IsAtWarAgainstFaction(vlandia, wolfskins) != partialMode)
                    throw new InvalidOperationException("The required faction relationships could not be established.");

                var position = wolves[0].Position;
                foreach (var party in parties)
                {
                    party.Position = position;
                    MessageBroker.Instance.Publish(typeof(PeaceBattleFixtureCommands),
                        new PartyBehaviorChangeAttempted(party, forcePosition: true, isCurrentlyAtSea: false));
                }

                battania.CreateArmy(battanians[0].LeaderHero, battanianTarget, Army.ArmyTypes.Raider);
                pending.BattanianArmy = battanians[0].Army;
                vlandia.CreateArmy(vlandians[0].LeaderHero, vlandianTarget, Army.ArmyTypes.Raider);
                pending.VlandianArmy = vlandians[0].Army;
                if (pending.BattanianArmy == null || pending.VlandianArmy == null)
                    throw new InvalidOperationException("Both fixture armies must be created.");

                battanians[1].Army = pending.BattanianArmy;
                pending.BattanianArmy.AddPartyToMergedParties(battanians[1]);
                vlandians[1].Army = pending.VlandianArmy;
                pending.VlandianArmy.AddPartyToMergedParties(vlandians[1]);

                StartBattleAction.Apply(battanians[0].Party, wolves[0].Party);
                pending.MapEvent = battanians[0].MapEvent;
                if (pending.MapEvent?.IsFieldBattle != true || wolves[0].MapEvent != pending.MapEvent)
                    throw new InvalidOperationException("The Battanian attack did not create a field battle with Wolfskins.");

                StartBattleAction.Apply(vlandians[0].Party, partialMode ? wolves[0].Party : battanians[0].Party);
                if (!HasExactBattleParties(pending.MapEvent, parties) || !HasFixtureSides(pending, parties) ||
                    !objects.TryGetId(pending.MapEvent, out pending.MapEventId) ||
                    !objects.TryGetId(pending.BattanianArmy, out _) ||
                    !objects.TryGetId(pending.VlandianArmy, out _))
                    throw new InvalidOperationException("All five registered parties must join the expected sides of one field battle.");

                return Succeeded("Peace-battle fixture staged. Call peace_battle_fixture_state on server and client, then peace_battle_fixture_peace on server.\n" +
                    StateJson(objects, pending.MapEventId, parties, partialMode: partialMode));
            }
            catch (Exception setupError)
            {
                try
                {
                    Restore(pending, behavior, time);
                    fixture = null;
                    return Failed($"Fixture setup failed: {setupError.Message}. The baseline was restored.");
                }
                catch (Exception restoreError)
                {
                    return Failed($"Fixture setup failed: {setupError.Message}. Restore failed: {restoreError.Message}. Run peace_battle_fixture_restore.");
                }
            }
        }
    }

    public sealed class StateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "peace_battle_fixture_state";
        public string Description => "Reports the exact peace-battle parties on this machine.";
        public CoopCommandSide Side => CoopCommandSide.Both;
        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("map_event_id", "The fixture map event id.", true),
            new ExpectedArgs("battania_leader_id", "The Battanian leader party StringId.", true),
            new ExpectedArgs("battania_member_id", "The Battanian member party StringId.", true),
            new ExpectedArgs("wolfskins_party_id", "The Wolfskins party StringId.", true),
            new ExpectedArgs("vlandia_leader_id", "The Vlandian leader party StringId.", true),
            new ExpectedArgs("vlandia_member_id", "The Vlandian member party StringId.", true),
            new ExpectedArgs("mode", "Use partial for the allied-attacker fixture; omit for defender reinforcement.", false),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (Campaign.Current == null) return Failed("No campaign is loaded.");
            if (args.Count > 7 || (args.Count == 7 && args[6] != "partial"))
                return Failed("Mode must be partial, or omit it for defender reinforcement.");
            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects))
                return Failed("Unable to resolve ObjectManager.");
            var parties = Enumerable.Range(1, 5)
                .Select(index => Campaign.Current.CampaignObjectManager.Find<MobileParty>(args[index]))
                .ToArray();
            return Succeeded(StateJson(objects, args[0], parties, partialMode: args.Count == 7));
        }
    }

    public sealed class PeaceCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "peace_battle_fixture_peace";
        public string Description => "Makes peace between the selected fixture factions during battle.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer) return Failed("Run this command on the server.");
            var current = fixture;
            if (current?.MapEvent == null || current.MapEvent.IsFinalized)
                return Failed("No active peace-battle fixture is available.");
            if (current.PeaceApplied) return Failed("Peace was already applied to this fixture.");
            IFaction peaceFirst = current.PartialMode ? (IFaction)current.Vlandia : current.Battania;
            IFaction peaceSecond = current.PartialMode ? (IFaction)current.Wolfskins : current.Vlandia;
            if (!FactionManager.IsAtWarAgainstFaction(peaceFirst, peaceSecond))
                return Failed("The selected fixture factions are no longer at war.");
            var parties = current.Parties.Select(snapshot => snapshot.Party).ToArray();
            if (!HasExactBattleParties(current.MapEvent, parties) || !HasFixtureSides(current, parties) ||
                current.Parties[0].Party.Army != current.BattanianArmy ||
                current.Parties[1].Party.Army != current.BattanianArmy ||
                current.Parties[3].Party.Army != current.VlandianArmy ||
                current.Parties[4].Party.Army != current.VlandianArmy)
                return Failed("The fixture parties are no longer all in the same battle.");

            if (!ContainerProvider.TryResolve<IObjectManager>(out var objects))
                return Failed("ObjectManager was unavailable for the peace state report.");
            var before = StateJson(objects, current.MapEventId, parties, current.MapEvent, "before_peace", current.PartialMode);
            try
            {
                MakePeaceAction.Apply(peaceFirst, peaceSecond);
            }
            catch (Exception ex)
            {
                return Failed($"MakePeaceAction failed: {ex.Message}.\n{before}\n" +
                    StateJson(objects, current.MapEventId, parties, current.MapEvent, "after_failed_peace", current.PartialMode));
            }
            current.PeaceApplied = true;
            var after = StateJson(objects, current.MapEventId, parties, current.MapEvent, "after_peace", current.PartialMode);
            if (FactionManager.IsAtWarAgainstFaction(peaceFirst, peaceSecond))
                return Failed("MakePeaceAction did not end the selected war.\n" + before + "\n" + after);
            if (current.PartialMode &&
                (!FactionManager.IsAtWarAgainstFaction(current.Battania, current.Wolfskins) ||
                 parties.Skip(3).Any(party => party.MapEvent != null || party.Party.MapEventSide != null) ||
                 !HasExactBattleParties(current.MapEvent, parties.Take(3).ToArray()) ||
                 !HasExactSides(current.MapEvent, parties.Take(2).ToArray(), new[] { parties[2] })))
                return Failed("The partial-peace battle did not retain only Wolfskins and the Battanian army.\n" + before + "\n" + after);
            return Succeeded("Production peace applied with patches live.\n" + before + "\n" +
                after);
        }
    }

    public sealed class RestoreCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_event";
        public string Name => "peace_battle_fixture_restore";
        public string Description => "Restores the peace-battle fixture parties, armies, and war state.";
        public CoopCommandSide Side => CoopCommandSide.Server;
        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (!ModInformation.IsServer) return Failed("Run this command on the server.");
            var current = fixture;
            if (current == null) return Failed("No peace-battle fixture is active.");
            if (!TryServices(out _, out var behavior, out var time, out _, out var error))
                return Failed(error);
            try
            {
                Restore(current, behavior, time);
                fixture = null;
                return Succeeded("Peace-battle fixture restored. Do not save the fixture campaign.");
            }
            catch (Exception ex)
            {
                return Failed($"Fixture restore failed: {ex.Message}. Retry peace_battle_fixture_restore.");
            }
        }
    }

    private static bool TryServices(
        out IObjectManager objects,
        out IMobilePartyBehaviorSnapshot behavior,
        out ITimeControlInterface time,
        out IArmyDisbander armies,
        out string error)
    {
        objects = null;
        behavior = null;
        time = null;
        armies = null;
        error = null;
        if (!ContainerProvider.TryGetContainer(out var container) ||
            !container.TryResolve(out objects) ||
            !container.TryResolve(out behavior) ||
            !container.TryResolve(out time) ||
            !container.TryResolve(out armies))
        {
            error = "Unable to resolve peace-battle fixture services.";
            return false;
        }
        return true;
    }

    private static MobileParty[] SelectParties(IFaction faction, int count) => MobileParty.All
        .Where(party => party.IsActive && !party.IsPlayerParty() && party.Party != null &&
                        party.MapFaction == faction && party.LeaderHero != null &&
                        party.MemberRoster.TotalHealthyCount > 0 && party.Army == null &&
                        party.AttachedTo == null && party.AttachedParties.Count == 0 &&
                        party.MapEvent == null && party.CurrentSettlement == null &&
                        party.BesiegerCamp == null && !party.IsTransitionInProgress &&
                        !party.IsCurrentlyAtSea && party.Position.IsOnLand)
        .OrderBy(party => party.StringId)
        .Take(count)
        .ToArray();

    private static bool HasExactBattleParties(MapEvent mapEvent, MobileParty[] parties) =>
        mapEvent != null && !mapEvent.IsFinalized && mapEvent.IsFieldBattle &&
        new HashSet<PartyBase>(mapEvent.InvolvedParties).SetEquals(parties.Select(party => party.Party)) &&
        parties.All(party => party.MapEvent == mapEvent);

    private static bool HasFixtureSides(Fixture current, MobileParty[] parties) =>
        HasExactSides(current.MapEvent,
            current.PartialMode ? new[] { parties[0], parties[1], parties[3], parties[4] } : parties.Take(2).ToArray(),
            current.PartialMode ? new[] { parties[2] } : parties.Skip(2).ToArray());

    private static bool HasExactSides(MapEvent mapEvent, MobileParty[] attackers, MobileParty[] defenders) =>
        mapEvent != null &&
        new HashSet<PartyBase>(mapEvent.AttackerSide.Parties.Select(entry => entry.Party))
            .SetEquals(attackers.Select(party => party.Party)) &&
        new HashSet<PartyBase>(mapEvent.DefenderSide.Parties.Select(entry => entry.Party))
            .SetEquals(defenders.Select(party => party.Party)) &&
        attackers.All(party => party.Party.MapEventSide == mapEvent.AttackerSide) &&
        defenders.All(party => party.Party.MapEventSide == mapEvent.DefenderSide);

    private static string StateJson(IObjectManager objects, string expectedMapEventId, MobileParty[] parties,
        MapEvent knownEvent = null, string phase = "observation", bool partialMode = false)
    {
        var battania = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == "battania");
        var vlandia = Kingdom.All.FirstOrDefault(kingdom => kingdom.StringId == "vlandia");
        var wolfskins = Clan.All.FirstOrDefault(clan => clan.StringId == "wolfskins");
        var mapEvent = knownEvent;
        if (mapEvent == null && !string.IsNullOrEmpty(expectedMapEventId))
            objects.TryGetObject(expectedMapEventId, out mapEvent);
        if (mapEvent == null)
            mapEvent = parties.FirstOrDefault(party => party?.MapEvent != null)?.MapEvent;
        var roles = new[] { "battaniaLeader", "battaniaMember", "wolfskins", "vlandiaLeader", "vlandiaMember" };
        var records = parties.Select((party, index) => new
        {
            role = roles[index],
            partyStringId = party?.StringId,
            partyId = Id(objects, party),
            factionId = party?.MapFaction?.StringId,
            armyId = Id(objects, party?.Army),
            armyLeaderPartyId = party?.Army?.LeaderParty?.StringId,
            attachedToPartyId = party?.AttachedTo?.StringId,
            mapEventId = Id(objects, party?.MapEvent),
            mapEventSide = party?.Party?.MapEventSide?.MissionSide.ToString() ?? "none",
            onAttackerSide = party != null && mapEvent?.AttackerSide?.Parties.Any(item => item.Party == party.Party) == true,
            onDefenderSide = party != null && mapEvent?.DefenderSide?.Parties.Any(item => item.Party == party.Party) == true,
            active = party?.IsActive == true,
            behavior = party == null ? "none" : party.DefaultBehavior.ToString(),
            moveMode = party == null ? "none" : party.PartyMoveMode.ToString(),
            position = party == null ? null : new { x = party.Position.X, y = party.Position.Y, onLand = party.Position.IsOnLand },
            targetPosition = party == null ? null : new { x = party.TargetPosition.X, y = party.TargetPosition.Y },
            healthyTroops = party?.MemberRoster.TotalHealthyCount ?? 0,
        }).ToArray();
        return "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            machine = ModInformation.IsServer ? "server" : "client",
            phase,
            mode = partialMode ? "partial" : "defender_reinforcement",
            peaceFactionIds = partialMode ? new[] { "vlandia", "wolfskins" } : new[] { "battania", "vlandia" },
            expectedMapEventId,
            mapEventId = Id(objects, mapEvent),
            mapEventExists = mapEvent != null,
            mapEventFinalized = mapEvent?.IsFinalized,
            isFieldBattle = mapEvent?.IsFieldBattle,
            attackerPartyStringIds = mapEvent?.AttackerSide?.Parties.Select(item => item.Party?.MobileParty?.StringId ?? item.Party?.Settlement?.StringId).ToArray(),
            defenderPartyStringIds = mapEvent?.DefenderSide?.Parties.Select(item => item.Party?.MobileParty?.StringId ?? item.Party?.Settlement?.StringId).ToArray(),
            involvedPartyStringIds = mapEvent?.InvolvedParties.Select(party => party.MobileParty?.StringId ?? party.Settlement?.StringId).ToArray(),
            campaignTicks = CampaignTime.Now.NumTicks,
            battaniaVlandiaAtWar = battania != null && vlandia != null && FactionManager.IsAtWarAgainstFaction(battania, vlandia),
            battaniaWolfskinsAtWar = battania != null && wolfskins != null && FactionManager.IsAtWarAgainstFaction(battania, wolfskins),
            vlandiaWolfskinsAtWar = vlandia != null && wolfskins != null && FactionManager.IsAtWarAgainstFaction(vlandia, wolfskins),
            parties = records,
        });
    }

    private static string Id(IObjectManager objects, object value) =>
        value == null ? "none" : objects.TryGetId(value, out var id) ? id : "unregistered";

    private static void Restore(Fixture current, IMobilePartyBehaviorSnapshot behavior, ITimeControlInterface time)
    {
        time.ServerSetTimeControlForLiveTest(TimeControlEnum.Pause);
        if (current.MapEvent != null && !current.MapEvent.IsFinalized)
            current.MapEvent.FinalizeEvent();

        if (!ContainerProvider.TryResolve<IArmyDisbander>(out var armies))
            throw new InvalidOperationException("Unable to resolve army cleanup service.");
        if (current.BattanianArmy?.Parties.Count > 0)
            armies.Disband(current.BattanianArmy, Army.ArmyDispersionReason.ObjectiveFinished);
        if (current.VlandianArmy?.Parties.Count > 0)
            armies.Disband(current.VlandianArmy, Army.ArmyDispersionReason.ObjectiveFinished);

        RestoreWar(current.Battania, current.Vlandia, current.BattaniaVlandiaAtWar);
        RestoreWar(current.Battania, current.Wolfskins, current.BattaniaWolfskinsAtWar);
        RestoreWar(current.Vlandia, current.Wolfskins, current.VlandiaWolfskinsAtWar);
        foreach (var snapshot in current.Parties)
        {
            snapshot.RestoreRosters();
            snapshot.Party.Position = snapshot.Behavior.PartyPosition;
            if (!behavior.TryApply(snapshot.Party, snapshot.Behavior, out _))
                throw new InvalidOperationException($"Unable to restore movement state for {snapshot.Party.StringId}.");
            MessageBroker.Instance.Publish(typeof(PeaceBattleFixtureCommands),
                new PartyBehaviorChangeAttempted(snapshot.Party, forcePosition: true,
                    isCurrentlyAtSea: snapshot.Behavior.IsCurrentlyAtSea));
        }
    }

    private static void RestoreWar(IFaction first, IFaction second, bool wasAtWar)
    {
        var atWar = FactionManager.IsAtWarAgainstFaction(first, second);
        if (wasAtWar && !atWar) DeclareWarAction.ApplyByDefault(first, second);
        if (!wasAtWar && atWar) MakePeaceAction.Apply(first, second);
        if (FactionManager.IsAtWarAgainstFaction(first, second) != wasAtWar)
            throw new InvalidOperationException($"Unable to restore the {first.StringId}-{second.StringId} war state.");
    }

    private sealed class PartySnapshot
    {
        public MobileParty Party { get; }
        public PartyBehaviorUpdateData Behavior { get; }
        private readonly TroopRosterElement[] members;
        private readonly TroopRosterElement[] prisoners;

        public PartySnapshot(MobileParty party, PartyBehaviorUpdateData behavior)
        {
            Party = party;
            Behavior = behavior;
            members = party.MemberRoster.GetTroopRoster().ToArray();
            prisoners = party.PrisonRoster.GetTroopRoster().ToArray();
        }

        public void RestoreRosters()
        {
            RestoreRoster(Party.MemberRoster, members);
            RestoreRoster(Party.PrisonRoster, prisoners);
        }

        private static void RestoreRoster(TroopRoster roster, TroopRosterElement[] baseline)
        {
            for (var index = roster.Count - 1; index >= 0; index--)
            {
                var element = roster.GetElementCopyAtIndex(index);
                roster.AddToCountsAtIndex(index, -element.Number, -element.WoundedNumber, 0, false);
            }
            roster.RemoveZeroCounts();
            foreach (var element in baseline)
                roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, true);
        }
    }

    private sealed class Fixture
    {
        public Kingdom Battania;
        public Kingdom Vlandia;
        public Clan Wolfskins;
        public bool PartialMode;
        public PartySnapshot[] Parties;
        public Army BattanianArmy;
        public Army VlandianArmy;
        public MapEvent MapEvent;
        public string MapEventId;
        public bool BattaniaWolfskinsAtWar;
        public bool BattaniaVlandiaAtWar;
        public bool VlandiaWolfskinsAtWar;
        public bool PeaceApplied;
    }
}
#endif
