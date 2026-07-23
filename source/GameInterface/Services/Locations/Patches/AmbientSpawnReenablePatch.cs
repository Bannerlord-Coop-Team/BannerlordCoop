//using Common;
//using HarmonyLib;
//using SandBox.CampaignBehaviors;
//using System.Collections.Generic;
//using System.Reflection;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.CampaignBehaviors;

//namespace GameInterface.Services.Locations.Patches;

///// <summary>
///// Re-enables the ambient crowds on clients. The host is a dedicated server that never runs a mission
///// scene, so these behaviors stay disabled on it. Each behavior's <c>RegisterEvents</c> is skipped and
///// re-routed through <see cref="AmbientSpawnReenable.SubscribeSpawnListenerOnly"/> so only the scene-spawn
///// listener is wired: some of these behaviors also wire session-launched, settlement-owner-change and
///// conversation-ended handlers that are not co-op safe. The re-subscribed crowd is scoped as ambient
///// (made static and non-interactable) and kept identical across clients by <see cref="AmbientSpawnSeedPatch"/>.
///// </summary>
//[HarmonyPatch]
//internal class AmbientSpawnReenablePatch
//{
//    private static IEnumerable<MethodBase> TargetMethods() => new MethodBase[]
//    {
//        AccessTools.Method(typeof(CommonTownsfolkCampaignBehavior), nameof(CommonTownsfolkCampaignBehavior.RegisterEvents)),
//        AccessTools.Method(typeof(CommonVillagersCampaignBehavior), nameof(CommonVillagersCampaignBehavior.RegisterEvents)),
//        AccessTools.Method(typeof(TownMerchantsCampaignBehavior), nameof(TownMerchantsCampaignBehavior.RegisterEvents)),
//        AccessTools.Method(typeof(WorkshopsCharactersCampaignBehavior), nameof(WorkshopsCharactersCampaignBehavior.RegisterEvents)),
//    };

//    static bool Prefix(CampaignBehaviorBase __instance)
//    {
//        if (ModInformation.IsClient)
//        {
//            AmbientSpawnReenable.SubscribeSpawnListenerOnly(__instance);
//        }

//        return false;
//    }
//}
