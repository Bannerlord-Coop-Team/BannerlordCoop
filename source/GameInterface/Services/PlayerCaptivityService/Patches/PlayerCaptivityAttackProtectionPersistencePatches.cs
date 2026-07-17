using Common;
using GameInterface.Services.MobilePartyAIs.Patches;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PlayerCaptivityService.Patches;

/// <summary>
/// Persists server-owned former-captor attack protections in the existing player-captivity behavior save record.
/// </summary>
[HarmonyPatch(typeof(PlayerCaptivityCampaignBehavior))]
internal class PlayerCaptivityAttackProtectionPersistencePatches
{
    private const string SaveKey = "_coop_player_captivity_attack_protections";

    [HarmonyPatch(nameof(PlayerCaptivityCampaignBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix()
    {
        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
    }

    [HarmonyPatch(nameof(PlayerCaptivityCampaignBehavior.SyncData))]
    [HarmonyPostfix]
    private static void SyncDataPostfix(IDataStore dataStore)
    {
        SyncAttackProtections(dataStore, ModInformation.IsClient);
    }

    internal static void SyncAttackProtections(IDataStore dataStore, bool isClient, CampaignTime? currentTime = null)
    {
        List<PlayerCaptivityAttackProtectionSaveData> saveData = null;
        if (dataStore.IsSaving)
        {
            if (!isClient)
                DefaultMobilePartyAIModelPatches.PrunePersistedAttackProtections(currentTime ?? CampaignTime.Now);

            saveData = isClient
                ? new List<PlayerCaptivityAttackProtectionSaveData>()
                : DefaultMobilePartyAIModelPatches.GetPersistedAttackProtections()
                    .Select(protection => new PlayerCaptivityAttackProtectionSaveData(
                        protection.AttackerParty,
                        protection.TargetParty,
                        protection.DisabledUntil))
                    .ToList();
        }

        dataStore.SyncData(SaveKey, ref saveData);
        if (!dataStore.IsLoading) return;

        DefaultMobilePartyAIModelPatches.ResetPersistedAttackProtections();
        if (isClient || saveData == null) return;

        var now = currentTime ?? CampaignTime.Now;
        foreach (var protection in saveData)
        {
            if (protection?.AttackerParty?.Ai == null
                || protection.AttackerParty.IsActive != true
                || protection.TargetParty?.IsActive != true
                || now > protection.DisabledUntil)
            {
                continue;
            }

            DefaultMobilePartyAIModelPatches.PreventAttacksUntil(
                protection.AttackerParty,
                protection.TargetParty,
                protection.DisabledUntil);
        }
    }
}

[HarmonyPatch(typeof(DestroyPartyAction), nameof(DestroyPartyAction.ApplyInternal))]
internal class PlayerCaptivityAttackProtectionPartyDestructionPatches
{
    [HarmonyPostfix]
    private static void ApplyInternalPostfix(MobileParty destroyedParty)
    {
        if (ModInformation.IsServer)
            DefaultMobilePartyAIModelPatches.RemoveAttackProtectionsForParty(destroyedParty);
    }
}
