using Common;
using Common.Messaging;
using GameInterface.Services.BattleWreckages.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Patches;

[HarmonyPatch(typeof(CampaignObjectManager))]
internal class BattleWreckageCampaignObjectManagerPatches
{
    /// <summary>
    /// Runs after the constructor, so clients already have the wreckage's field values when this arrives.
    /// The client's map visual reads the position and type once, when the wreckage is added
    /// </summary>
    [HarmonyPatch(nameof(CampaignObjectManager.AddWreckage))]
    [HarmonyPostfix]
    public static void AddWreckagePostfix(BattleWreckage battleWreckage)
    {
        // Clients add wreckages in BattleWreckageCampaignBehaviorInterface.AddWreckageToMap
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(battleWreckage, new BattleWreckageAdded(battleWreckage));
    }

    /// <summary>
    /// Runs before the registry's DestroyWreckage postfix sends the destroy, so clients can still resolve the wreckage when this arrives
    /// </summary>
    [HarmonyPatch(nameof(CampaignObjectManager.RemoveWreckage))]
    [HarmonyPostfix]
    public static void RemoveWreckagePostfix(BattleWreckage battleWreckage)
    {
        // Clients remove wreckages in BattleWreckageCampaignBehaviorInterface.RemoveWreckageFromMap
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(battleWreckage, new BattleWreckageRemoved(battleWreckage));
    }
}
