using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using HarmonyLib;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Messages;
using Missions.Tournaments;
using Missions.Tournaments.Messages;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Tournaments;

public class TournamentWorldItemOrderingTests : MissionTestEnvironment
{
    public TournamentWorldItemOrderingTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RuntimeWorldItemBeforeDirectDrop_SkipsSecondDropAndPreservesRuntimeItem()
    {
        var harmony = new Harmony("e2e.tournament-world-item-ordering");
        PatchAgentEquipment(harmony);

        try
        {
            var observer = Clients.First();
            SetControllerId(observer, "observer");

            observer.Call(() =>
            {
                Agent agent = ObjectHelper.SkipConstructor<Agent>();
                MissionEquipment equipment = CreateEquipmentWithWeapon();
                AgentEquipmentShim.Track(agent, equipment);

                Guid agentId = Guid.NewGuid();
                Guid worldItemId = Guid.NewGuid();
                var agentRegistry = observer.Resolve<INetworkAgentRegistry>();
                var worldItemRegistry = observer.Resolve<INetworkWorldItemRegistry>();
                Assert.True(agentRegistry.TryRegisterAgent("fighter", agentId, agent));

                // A runtime packet containing the dropped world item also describes the agent after the
                // drop. Exercise the production equipment reconciliation used before world-item spawning.
                var controller = observer.Resolve<CoopTournamentController>();
                InvokeReconcileRuntimeEquipment(controller, agent);
                Assert.True(equipment[EquipmentIndex.Weapon0].IsEmpty);

                // SpawnRuntimeWorldItem ends by registering the new scene item under the direct-drop id.
                // A shell is sufficient here because the registry treats the scene entity as an identity.
                SpawnedItemEntity runtimeItem = ObjectHelper.SkipConstructor<SpawnedItemEntity>();
                worldItemRegistry.Register(worldItemId, runtimeItem);

                using var messageBroker = new MessageBroker();
                var handler = new WeaponDropHandler(
                    agentRegistry,
                    worldItemRegistry,
                    messageBroker,
                    observer.Resolve<IBattleNetwork>());
                try
                {
                    messageBroker.Publish(
                        this,
                        new NetworkWeaponDropped(agentId, EquipmentIndex.Weapon0, worldItemId));

                    Assert.Equal(0, AgentEquipmentShim.GetDropCount(agent));
                    Assert.True(worldItemRegistry.TryGet(worldItemId, out SpawnedItemEntity registeredItem));
                    Assert.Same(runtimeItem, registeredItem);
                    Assert.Single(worldItemRegistry.GetAll());
                }
                finally
                {
                    handler.Dispose();
                }
            });
        }
        finally
        {
            AgentEquipmentShim.Clear();
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static MissionEquipment CreateEquipmentWithWeapon()
    {
        var equipment = new MissionEquipment();
        var weapons = new MissionWeapon[(int)EquipmentIndex.NumAllWeaponSlots];
        weapons[(int)EquipmentIndex.Weapon0] = new MissionWeapon(
            ObjectHelper.SkipConstructor<ItemObject>(),
            null,
            null);
        typeof(MissionEquipment)
            .GetField("_weaponSlots", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .SetValue(equipment, weapons);
        return equipment;
    }

    private static void InvokeReconcileRuntimeEquipment(
        CoopTournamentController controller,
        Agent agent)
    {
        MethodInfo reconcile = typeof(CoopTournamentController).GetMethod(
            "ReconcileRuntimeEquipment",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(reconcile);
        reconcile.Invoke(
            controller,
            new object[] { agent, Array.Empty<TournamentMissionWeaponData>() });
    }

    private static void PatchAgentEquipment(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
            prefix: Prefix(nameof(AgentEquipmentShim.SkipScriptComponentCache)));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.Current)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetCurrentMission)));
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Equipment)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetEquipment)));
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.RemoveEquippedWeapon)),
            prefix: Prefix(nameof(AgentEquipmentShim.RemoveEquippedWeapon)));
        harmony.Patch(
            AccessTools.Method(typeof(Agent), nameof(Agent.GetWeaponEntityFromEquipmentSlot)),
            prefix: Prefix(nameof(AgentEquipmentShim.GetWeaponEntity)));
        harmony.Patch(
            AccessTools.Method(
                typeof(Agent),
                nameof(Agent.DropItem),
                new[] { typeof(EquipmentIndex), typeof(WeaponClass) }),
            prefix: Prefix(nameof(AgentEquipmentShim.DropItem)));
    }

    private static HarmonyMethod Prefix(string methodName) =>
        new(AccessTools.Method(typeof(AgentEquipmentShim), methodName));

    private static class AgentEquipmentShim
    {
        private sealed class State
        {
            public MissionEquipment Equipment { get; }
            public int DropCount { get; set; }

            public State(MissionEquipment equipment)
            {
                Equipment = equipment;
            }
        }

        private static readonly Dictionary<Agent, State> States = new();

        public static void Track(Agent agent, MissionEquipment equipment) =>
            States.Add(agent, new State(equipment));

        public static int GetDropCount(Agent agent) => States[agent].DropCount;

        public static void Clear() => States.Clear();

        public static bool SkipScriptComponentCache() => false;

        public static bool GetCurrentMission(ref Mission __result)
        {
            __result = null;
            return false;
        }

        public static bool GetEquipment(Agent __instance, ref MissionEquipment __result)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            __result = state.Equipment;
            return false;
        }

        public static bool RemoveEquippedWeapon(Agent __instance, EquipmentIndex slotIndex)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.Equipment[slotIndex] = default;
            return false;
        }

        public static bool GetWeaponEntity(
            Agent __instance,
            EquipmentIndex slotIndex,
            ref WeakGameEntity __result)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            if (!state.Equipment[slotIndex].IsEmpty) return true;
            __result = default;
            return false;
        }

        public static bool DropItem(Agent __instance, EquipmentIndex itemIndex)
        {
            if (!States.TryGetValue(__instance, out State state)) return true;
            state.DropCount++;
            state.Equipment[itemIndex] = default;
            return false;
        }
    }
}
