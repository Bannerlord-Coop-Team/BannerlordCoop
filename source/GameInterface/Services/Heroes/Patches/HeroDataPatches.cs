using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(Hero))]
internal class HeroDataPatches
{
    [HarmonyPatch(nameof(Hero.SetName))]
    [HarmonyPrefix]
    private static bool Prefix(ref Hero __instance, ref TextObject fullName, ref TextObject firstName)
    {
        // Allows original method call when called by OverrideTemplateFn 
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Skip method if called from client and allow origin
        if (ModInformation.IsClient) return false;

        var data = new HeroChangeNameData(__instance, fullName, firstName);
        var message = new HeroNameChanged(data);
        MessageBroker.Instance.Publish(__instance, message);

        // Returning true allows original on the server to run
        return true;
    }

    public static void SetNameOverride(Hero instance, TextObject fullName,  TextObject firstName)
    {
        using (new AllowedThread())
        {
            instance.SetName(fullName, firstName);
        }
    }
}
