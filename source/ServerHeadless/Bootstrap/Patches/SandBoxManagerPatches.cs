using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// The SandBox data (Settlements, NPCCharacters, WorkshopTypes, …) is loaded by
    /// <c>SandBoxSubModule.RegisterSubModuleObjects/AfterRegisterSubModuleObjects</c>, which the
    /// engine reaches by iterating loaded submodule instances. Headless we have no submodule
    /// instances, so <see cref="SandBoxManager.InitializeSandboxXMLs"/> never runs and the world has
    /// no settlements. Drive it directly off SandBoxManager.OnCampaignStart (same point in the load).
    /// </summary>
    [HarmonyPatch(typeof(SandBoxManager))]
    internal class SandBoxManagerPatches
    {
        [HarmonyPatch(nameof(SandBoxManager.OnCampaignStart))]
        [HarmonyPostfix]
        static void OnCampaignStartPostfix(SandBoxManager __instance, bool isSavedCampaign)
        {
            __instance.InitializeSandboxXMLs(isSavedCampaign);
            __instance.InitializeCharactersAfterLoad(isSavedCampaign);
        }
    }
}
