using Common;
using Common.Messaging;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.UI.LogEntries.Messages;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.CommentBehaviors;

namespace GameInterface.Services.UI.LogEntries.Patches;

[HarmonyPatch(typeof(CommentChildbirthBehavior))]
internal class CommentChildbirthBehaviorPatches
{
    [HarmonyPatch(nameof(CommentChildbirthBehavior.OnGivenBirthEvent))]
    [HarmonyPrefix]
    public static bool OnGivenBirthEventPrefix(CommentChildbirthBehavior __instance, Hero mother, List<Hero> aliveChildren, int stillbornCount)
    {
        if (ModInformation.IsClient) return true;

        if (!mother.IsPlayerHero() && mother.Clan?.IsPlayerClan() == false) return false;

        var message = new CommentGivenBirth(mother, aliveChildren, stillbornCount);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }
}
