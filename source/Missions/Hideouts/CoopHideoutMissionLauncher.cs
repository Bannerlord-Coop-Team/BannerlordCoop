using Common.Messaging;
using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Services.Network;
using SandBox.Conversation.MissionLogics;
using SandBox.Missions;
using SandBox.Missions.MissionEvents;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Hideout;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Missions.MissionLogics;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace Missions.Hideouts;

internal sealed class CoopHideoutMissionLauncher : ICoopHideoutMissionLauncher
{
    private readonly IMessageBroker broker;
    private readonly IObjectManager objects;
    private readonly ICoopBattleBehaviorAttacher attacher;
    private readonly IBattleAgentBudget budget;
    private readonly IBattleNetwork network;
    private readonly IMissionContext missionContext;

    public CoopHideoutMissionLauncher(IMessageBroker broker, IObjectManager objects,
        ICoopBattleBehaviorAttacher attacher, IBattleAgentBudget budget,
        IBattleNetwork network, IMissionContext missionContext)
    {
        this.broker = broker;
        this.objects = objects;
        this.attacher = attacher;
        this.budget = budget;
        this.network = network;
        this.missionContext = missionContext;
    }

    public Mission OpenCoopHideoutMission(MissionInitializerRecord record, bool isDirectAssault)
    {
        var battle = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (battle?.MapEventSettlement?.IsHideout != true || !objects.TryGetId(battle, out var id))
            return null;

        var location = isDirectAssault ? null : Settlement.CurrentSettlement?.LocationComplex?.GetLocationWithId("hideout_center");
        var mission = MissionState.OpenNew(isDirectAssault ? "HideoutBattle" : "HideoutAmbushMission", record, delegate
        {
            var defender = new CoopTroopSupplier(id, BattleSideEnum.Defender, objects, budget);
            var attacker = new CoopTroopSupplier(id, BattleSideEnum.Attacker, objects, budget);
            CoopTroopSupplierRegistry.Register(defender);
            CoopTroopSupplierRegistry.Register(attacker);
            var logic = new CoopHideoutMissionLogic(network, broker, missionContext, defender, attacker, isDirectAssault);
            var suppliers = new IMissionTroopSupplier[] { logic.Defender, logic.Attacker };
            var behaviors = new List<MissionBehavior>
            {
                logic,
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new BattleEndLogic(),
                new MissionCombatantsLogic(battle.InvolvedParties, PartyBase.MainParty,
                    battle.GetLeaderParty(BattleSideEnum.Defender), battle.GetLeaderParty(BattleSideEnum.Attacker),
                    Mission.MissionTeamAITypeEnum.NoTeamAI, false),
                new AgentHumanAILogic(),
                new MissionConversationLogic(),
                new BattleObserverMissionLogic(),
                new BattleAgentLogic(),
                new MountAgentLogic(),
                new AgentVictoryLogic(),
                new MissionAgentPanicHandler(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(),
                new AgentMoraleInteractionLogic(),
                new HighlightsController(),
                new BattleHighlightsController(),
                new EquipmentControllerLeaveLogic(),
                new BattleSurgeonLogic(),
                new MissionObjectiveLogic(),
            };
            if (isDirectAssault)
            {
                behaviors.Add(new HideoutCinematicController());
                var controller = new CoopHideoutAssaultController(logic, suppliers);
                logic.Native = controller;
                behaviors.Add(controller);
            }
            else
            {
                behaviors.Add(new StealthPatrolPointMissionLogic());
                behaviors.Add(new HideoutAmbushBossFightCinematicController());
                behaviors.Add(new MissionAgentHandler());
                behaviors.Add(new MissionLocationLogic(location));
                behaviors.Add(new StealthFailCounterMissionLogic());
                behaviors.Add(new MissionAIActivationDeactivationEventListenerLogic());
                behaviors.Add(new CorpseDraggingMissionLogic());
                behaviors.Add(new ShowQuickInformationEventListenerLogic());
                behaviors.Add(new VisualTrackerMissionBehavior());
                behaviors.Add(new CoopHideoutStealthAreaLogic());
                var controller = new CoopHideoutAmbushController(logic, suppliers);
                logic.Native = controller;
                behaviors.Add(controller);
            }
            return behaviors.ToArray();
        });
        if (mission == null) return null;

        attacher.Attach(mission);
        broker.Publish(battle, new PlayerEnteredBattle(battle));
        return mission;
    }
}
