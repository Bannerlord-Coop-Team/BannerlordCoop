using HarmonyLib;
using Missions.Tournaments.Spectators;
using SandBox.Tournaments.MissionLogics;
using System.Linq;
using TaleWorlds.MountAndBlade;

namespace Missions.Tournaments.Patches;

internal static class TournamentCombatPatchInstaller
{
    private const string HarmonyId = "Coop.TournamentCombat";

    public static void Install()
    {
        var target = AccessTools.Method(typeof(Agent), nameof(Agent.RegisterBlow));
        if (target == null) return;
        if (Harmony.GetPatchInfo(target)?.Prefixes.Any(patch => patch.owner == HarmonyId) == true) return;

        var prefix = AccessTools.Method(typeof(TournamentBlowInterceptPatch), nameof(TournamentBlowInterceptPatch.Prefix));
        var harmony = new Harmony(HarmonyId);
        harmony.Patch(target, prefix: new HarmonyMethod(prefix));

        var reward = AccessTools.Method(typeof(TournamentFightMissionController), "EnemyHitReward");
        var rewardPrefix = AccessTools.Method(typeof(TournamentRewardSuppressionPatch), nameof(TournamentRewardSuppressionPatch.Prefix));
        if (reward != null)
            harmony.Patch(reward, prefix: new HarmonyMethod(rewardPrefix));

        var leave = AccessTools.Method(typeof(TournamentBehavior), "EndTournamentViaLeave");
        var leavePrefix = AccessTools.Method(typeof(TournamentLeaveGuardPatch), nameof(TournamentLeaveGuardPatch.Prefix));
        if (leave != null)
            harmony.Patch(leave, prefix: new HarmonyMethod(leavePrefix));

        var setKilled = AccessTools.Method(
            typeof(TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin),
            nameof(TaleWorlds.CampaignSystem.AgentOrigins.SimpleAgentOrigin.SetKilled));
        var campaignMutationPrefix = AccessTools.Method(
            typeof(TournamentCampaignOriginGuardPatch),
            nameof(TournamentCampaignOriginGuardPatch.Prefix));
        if (setKilled != null)
            harmony.Patch(setKilled, prefix: new HarmonyMethod(campaignMutationPrefix));

        var canPickUp = AccessTools.Method(typeof(Agent), nameof(Agent.CanInteractableWeaponBePickedUp));
        var pickUpPrefix = AccessTools.Method(
            typeof(TournamentSpectatorWeaponPickupPatch),
            nameof(TournamentSpectatorWeaponPickupPatch.Prefix));
        if (canPickUp != null)
            harmony.Patch(canPickUp, prefix: new HarmonyMethod(pickUpPrefix));

        var handleDrop = AccessTools.Method(typeof(Agent), nameof(Agent.HandleDropWeapon));
        var dropPrefix = AccessTools.Method(
            typeof(TournamentSpectatorWeaponDropPatch),
            nameof(TournamentSpectatorWeaponDropPatch.Prefix));
        if (handleDrop != null)
            harmony.Patch(handleDrop, prefix: new HarmonyMethod(dropPrefix));

        var missileCollision = AccessTools.Method(typeof(Mission), "HandleMissileCollisionReaction");
        var missileCollisionPrefix = AccessTools.Method(
            typeof(TournamentSpectatorOrangeCollisionPatch),
            nameof(TournamentSpectatorOrangeCollisionPatch.Prefix));
        if (missileCollision != null)
            harmony.Patch(missileCollision, prefix: new HarmonyMethod(missileCollisionPrefix));
    }
}

internal static class TournamentCampaignOriginGuardPatch
{
    public static bool Prefix()
        => Mission.Current?.GetMissionBehavior<CoopTournamentController>() == null;
}

internal static class TournamentLeaveGuardPatch
{
    public static bool Prefix(TournamentBehavior __instance)
    {
        if (__instance is not CoopTournamentBehavior) return true;
        return false;
    }
}

internal static class TournamentRewardSuppressionPatch
{
    public static bool Prefix()
        => Mission.Current?.GetMissionBehavior<CoopTournamentController>() == null;
}

internal static class TournamentBlowInterceptPatch
{
    public static bool Prefix(Agent __instance, Blow blow, ref AttackCollisionData collisionData)
    {
        CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
        if (controller == null) return true;
        return controller.InterceptBlow(__instance, blow, collisionData);
    }
}

internal static class TournamentSpectatorWeaponPickupPatch
{
    public static bool Prefix(Agent __instance, SpawnedItemEntity spawnedItem, ref bool __result)
    {
        CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
        if (controller == null) return true;

        bool isSpectator = controller.IsSpectatorAgent(__instance);
        bool isOrange = controller.IsSpectatorOrange(spawnedItem?.WeaponCopy.Item);
        if (!TournamentSpectatorOrange.ShouldBlockPickup(isSpectator, isOrange)) return true;

        __result = false;
        return false;
    }
}

internal static class TournamentSpectatorWeaponDropPatch
{
    public static bool Prefix(Agent __instance)
    {
        CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
        bool isSpectator = controller?.IsSpectatorAgent(__instance) == true;
        return !TournamentSpectatorOrange.ShouldBlockDrop(isSpectator);
    }
}

internal static class TournamentSpectatorOrangeCollisionPatch
{
    public static void Prefix(
        int missileIndex,
        Agent attackerAgent,
        ref Mission.MissileCollisionReaction collisionReaction)
    {
        CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
        if (controller == null) return;

        Mission.Missile missile = Mission.Current.MissilesList
            .FirstOrDefault(candidate => candidate.Index == missileIndex);
        bool isSpectator = controller.IsSpectatorAgent(attackerAgent);
        var item = missile?.Weapon.Item ?? attackerAgent?.WieldedWeapon.Item;
        bool isOrange = controller.IsSpectatorOrange(item);
        if (!TournamentSpectatorOrange.ShouldDisappearOnCollision(isSpectator, isOrange)) return;

        collisionReaction = Mission.MissileCollisionReaction.BecomeInvisible;
    }
}
