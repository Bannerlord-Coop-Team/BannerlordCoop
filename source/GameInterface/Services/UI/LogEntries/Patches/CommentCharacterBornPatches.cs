using Common;
using Common.Messaging;
using GameInterface.Services.UI.LogEntries.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;

namespace GameInterface.Services.UI.LogEntries.Patches;

[HarmonyPatch(typeof(CommentCharacterBornBehavior))]
internal class CommentCharacterBornPatches
{
    [HarmonyPatch(nameof(CommentCharacterBornBehavior.HeroCreated))]
    [HarmonyPrefix]
    public static void HeroCreatedPrefix(CommentCharacterBornBehavior __instance, Hero hero, bool isBornNaturally)
    {
        if (ModInformation.IsClient) return;

        // vanilla only logs born heroes, so every other hero creation is not sent
        if (!isBornNaturally) return;

        var message = new CommentCharacterBorn(hero);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
