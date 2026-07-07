using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Mid-battle reinforcements: the host fields a NEW AI party that joins the live battle through the local
/// spawn path — each troop spawns AI-controlled at the side's default reinforcement frame, flows through the
/// owner-side capture pipeline (registered under the host, broadcast to peers as puppets, casualty attributed
/// from the origin) exactly like the initial troops, and the formations it lands in are put on a charge.
/// </summary>
public interface IReinforcementFielder : IDisposable
{
}

/// <inheritdoc cref="IReinforcementFielder"/>
public class ReinforcementFielder : IReinforcementFielder
{
    private static readonly ILogger Logger = LogManager.GetLogger<ReinforcementFielder>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IBattleSession session;
    private readonly IBattleDeploymentCoordinator deployment;
    private readonly IAgentFormationAssigner formationAssigner;

    // [Host] Map-event party ids we have already fielded as mid-battle reinforcements, so a repeated involved-
    // parties broadcast for the same party doesn't double-spawn it.
    private readonly HashSet<string> reinforcedParties = new HashSet<string>();

    public ReinforcementFielder(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IBattleSession session,
        IBattleDeploymentCoordinator deployment,
        IAgentFormationAssigner formationAssigner)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.session = session;
        this.deployment = deployment;
        this.formationAssigner = formationAssigner;

        // [Host] A new AI party joining the live battle is fielded through our own spawn path (reinforcements).
        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_ReinforcementPartiesAdded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_ReinforcementPartiesAdded);
    }

    // [Host] A party was added to the live battle. If it is a new AI party we field — not a player's own party,
    // and not one of the initial parties the troop supplier already spawns — field it now by spawning its troops
    // at the side's default reinforcement frame. Gated on the battle being activated so the INITIAL involved-
    // parties broadcast (pre-activation) is ignored: those parties spawn through the supplier, not here.
    private void Handle_ReinforcementPartiesAdded(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        if (!session.IsLocalHost) return;
        if (!deployment.IsActivated) return;

        var partyIds = payload.What.MapEventPartyIds;
        if (partyIds == null || partyIds.Length == 0) return;

        GameThread.RunSafe(() =>
        {
            if (Mission.Current == null) return;

            foreach (var partyId in partyIds)
            {
                if (reinforcedParties.Contains(partyId)) continue;      // already fielded
                if (IsSupplierParty(partyId)) continue;                 // an initial party — the supplier spawns it
                if (!objectManager.TryGetObject<MapEventParty>(partyId, out var mapEventParty)) continue;

                var party = mapEventParty?.Party;
                if (party == null) continue;

                // The broadcast is server -> all clients (every battle), so only field this battle's parties.
                var mapEvent = party.MapEventSide?.MapEvent;
                if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId) || mapEventId != session.InstanceId)
                    continue;

                // A player's own party is fielded by that player (Phase E), not us — we only field AI parties.
                if (party.LeaderHero?.IsPlayerHero() == true) continue;

                reinforcedParties.Add(partyId);
                SpawnReinforcementParty(party, partyId);
            }
        });
    }

    // Whether a party is one of the initial reserves the troop supplier already provides, so the native spawn
    // logic spawns it and we must not also spawn it here.
    private bool IsSupplierParty(string mapEventPartyId)
    {
        foreach (var supplier in CoopTroopSupplierRegistry.GetSuppliers(session.InstanceId))
            foreach (var (partyId, _) in supplier.GetSuppliedByParty())
                if (partyId == mapEventPartyId) return true;
        return false;
    }

    // [Host, game thread] Field a newly-joined AI party: spawn each of its able troops AI-controlled at the
    // side's default reinforcement frame, then put the formations they land in on a charge. Capture is NOT
    // suppressed, so each spawn flows through the owner-side capture pipeline (registered under us, broadcast
    // to peers as puppets, casualty attributed from the origin) — the same pipeline the initial troops use.
    private void SpawnReinforcementParty(PartyBase party, string mapEventPartyId)
    {
        var mission = Mission.Current;
        var team = BattleTeams.Resolve(party.Side);
        if (team == null)
        {
            Logger.Warning("[BattleSync] No team for side {Side}; cannot field reinforcement party {Party}", party.Side, mapEventPartyId);
            return;
        }

        var formations = new HashSet<Formation>();
        int spawned = 0;
        foreach (var element in party.MemberRoster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null) continue;

            int able = element.Number - element.WoundedNumber;
            for (int i = 0; i < able; i++)
            {
                var agent = SpawnReinforcementTroop(mission, team, character, party);
                if (agent?.Formation != null) formations.Add(agent.Formation);
                spawned++;
            }
        }

        // A coop battle has no general commanding formations, so order each formation the reinforcements joined
        // to engage — SetControlledByAI alone leaves them idle without an active behavior.
        foreach (var formation in formations)
        {
            formation.SetControlledByAI(true);
            formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
        }

        Logger.Information("[BattleSync] Fielded reinforcement party {Party}: spawned {Count} troop(s)", mapEventPartyId, spawned);

        if (spawned > 0)
        {
            var troopText = spawned > 1 ? $"{spawned} troops" : $"{spawned} troop";
            InformationManager.DisplayMessage(new InformationMessage($"Reinforcements have arrived: {party.Name} ({troopText})"));
        }
    }

    // [Host, game thread] Spawn one reinforcement troop AI-controlled. With no InitialPosition set, the engine
    // positions it at the side's reinforcement spawn frame; we then drop it into its troop-class formation.
    private Agent SpawnReinforcementTroop(Mission mission, Team team, CharacterObject character, PartyBase party)
    {
        var origin = new CoopAgentOrigin(character, party, -1, null, new UniqueTroopDescriptor(MBRandom.RandomInt(int.MaxValue)));
        var equipment = character.IsHero ? character.HeroObject.BattleEquipment : character.Equipment;

        var buildData = new AgentBuildData(character);
        buildData.Team(team);
        buildData.TroopOrigin(origin);
        buildData.Equipment(equipment);
        buildData.BodyProperties(character.GetBodyPropertiesMax());
        buildData.Controller(AgentControllerType.AI);
        buildData.IsReinforcement(true);
        buildData.ClothingColor1(origin.FactionColor);
        buildData.ClothingColor2(origin.FactionColor2);

        var agent = mission.SpawnAgent(buildData);
        agent.FadeIn();

        formationAssigner.Assign(agent);

        // Wake the AI exactly as the adopt and NPC-release paths do. Without this the reinforcement is
        // AI-controlled but NOT alarmed and holds stale enemy caches, so it ignores its formation's Charge order
        // (set in SpawnReinforcementParty) and stands idle — the "reinforcements spawn but don't move" bug. In a
        // coop battle no general drives the formation, so nothing else alarms them.
        AgentAiWaker.Wake(agent);

        return agent;
    }
}
