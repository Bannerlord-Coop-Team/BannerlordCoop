using Common.Util;
using GameInterface;
using HarmonyLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Agents.Patches;
using Missions.Missiles.Patches;
using Missions.Tournaments;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace E2E.Tests.Services.Missions.Tournaments;

[Collection(nameof(TournamentCombatInfrastructureCollection))]
public class TournamentCombatInfrastructureTests
{
    private static int shieldDamageCalls;
    private static bool shieldDamageRanInsideAllowedThread;

    [Fact]
    public void TournamentMissileScope_RecognizesSharedTournamentController()
    {
        Mission mission = ObjectHelper.SkipConstructor<Mission>();
        CoopTournamentController controller = ObjectHelper.SkipConstructor<CoopTournamentController>();
        FieldInfo behaviorsField = typeof(Mission).GetField(
            "<MissionBehaviors>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(behaviorsField);
        behaviorsField.SetValue(mission, new List<MissionBehavior> { controller });

        Assert.True(BlockMissileIfNative.IsCoopMission(mission));
    }

    [Fact]
    public void MissionModule_RegistersShieldDamagePatchCategory()
    {
        HarmonyPatchCategoryRegistration registration = Assert.Single(
            MissionModule.CreatePatchCategoryRegistrations(),
            candidate => candidate.Category == MissionModule.ShieldDamagePatchCategory);
        var harmony = new Harmony($"{nameof(MissionModule_RegistersShieldDamagePatchCategory)}.{Guid.NewGuid()}");
        MethodInfo target = AccessTools.Method(typeof(Agent), "OnShieldDamaged");

        try
        {
            registration.Apply(harmony);

            Patches patches = Harmony.GetPatchInfo(target);
            Assert.Contains(patches.Postfixes, patch => patch.owner == harmony.Id);
        }
        finally
        {
            harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
        }
    }

    [Fact]
    public void ShieldDamageReplay_RunsExactlyOnceInsideAllowedThread()
    {
        var harmony = new Harmony($"{nameof(ShieldDamageReplay_RunsExactlyOnceInsideAllowedThread)}.{Guid.NewGuid()}");
        MethodInfo target = AccessTools.Method(typeof(Agent), "OnShieldDamaged");
        MethodInfo prefix = AccessTools.Method(
            typeof(TournamentCombatInfrastructureTests),
            nameof(CaptureShieldDamageReplay));
        shieldDamageCalls = 0;
        shieldDamageRanInsideAllowedThread = false;

        try
        {
            harmony.Patch(target, prefix: new HarmonyMethod(prefix) { priority = Priority.First });
            Agent agent = ObjectHelper.SkipConstructor<Agent>();

            ShieldDamageHandler.ApplyShieldDamage(agent, EquipmentIndex.Weapon0, 10);

            Assert.Equal(1, shieldDamageCalls);
            Assert.True(shieldDamageRanInsideAllowedThread);
        }
        finally
        {
            harmony.Unpatch(target, HarmonyPatchType.All, harmony.Id);
        }
    }

    private static bool CaptureShieldDamageReplay()
    {
        shieldDamageCalls++;
        shieldDamageRanInsideAllowedThread = AllowedThread.IsThisThreadAllowed();
        return false;
    }
}

[CollectionDefinition(nameof(TournamentCombatInfrastructureCollection), DisableParallelization = true)]
public class TournamentCombatInfrastructureCollection
{
}
