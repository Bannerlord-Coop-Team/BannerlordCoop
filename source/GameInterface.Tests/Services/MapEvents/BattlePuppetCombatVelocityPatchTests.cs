using GameInterface.Services.MapEvents.Patches;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattlePuppetCombatVelocityPatchTests
{
    [Fact]
    public void Patch_HooksNativeAttackCollisionResults()
    {
        var harmony = new Harmony("gameinterface.tests.battle-combat-velocity");
        try
        {
            var patched = harmony
                .CreateClassProcessor(typeof(BattlePuppetCombatVelocityPatch))
                .Patch();

            Assert.Contains(
                patched,
                method => method.Name.Contains(
                    nameof(MissionCombatMechanicsHelper.GetAttackCollisionResults)));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ApplyVictimGlobalVelocity_OnFoot_UsesWorldVelocityWithoutRotation()
    {
        var attackInformation = new AttackInformation
        {
            DoesVictimHaveMountAgent = false,
            VictimAgentMovementVelocity = new Vec2(20f, 30f),
            VictimMovementDirectionAsAngle = 1.5f,
        };

        BattlePuppetCombatVelocityPatch.ApplyVictimGlobalVelocity(
            ref attackInformation,
            new Vec2(3f, 4f));

        Assert.Equal(new Vec2(3f, 4f), attackInformation.VictimAgentMovementVelocity);
        Assert.Equal(0f, attackInformation.VictimMovementDirectionAsAngle);
    }

    [Fact]
    public void ApplyVictimGlobalVelocity_Mounted_ReconstructsNativeSpeedAndDirection()
    {
        var attackInformation = new AttackInformation
        {
            DoesVictimHaveMountAgent = true,
            VictimAgentMovementVelocity = new Vec2(20f, 30f),
            VictimAgentMountMovementDirection = new Vec2(-1f, 0f),
        };

        BattlePuppetCombatVelocityPatch.ApplyVictimGlobalVelocity(
            ref attackInformation,
            new Vec2(3f, 4f));

        Assert.Equal(new Vec2(0f, 5f), attackInformation.VictimAgentMovementVelocity);
        Assert.Equal(0.6f, attackInformation.VictimAgentMountMovementDirection.X, precision: 3);
        Assert.Equal(0.8f, attackInformation.VictimAgentMountMovementDirection.Y, precision: 3);
    }
}
