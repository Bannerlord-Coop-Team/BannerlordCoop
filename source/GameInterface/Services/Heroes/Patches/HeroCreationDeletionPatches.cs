using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroCreator), "CreateNewHero")]
internal class HeroCreationDeletionPatches
{
    private static bool Prefix() {
        
        if (AllowedThread.IsThisThreadAllowed()) return true;
        if (PolicyProvider.AllowOriginalCalls) return true;

        // Only run on server
        return ModInformation.IsServer;
    }
    

    private static void Postfix(ref Hero __result, ref CharacterObject template, ref int age)
    {
        // Skip if we called it
        if (AllowedThread.IsThisThreadAllowed()) return;

        var birthday = __result.BirthDay;
        var bornSettlement = __result.BornSettlement;
        var message = new HeroCreated(
            template,
            age,
            birthday,
            bornSettlement);

        MessageBroker.Instance.Publish(__result, message);
    }

    internal static Func<CharacterObject, int, Hero> CreateNewHero = typeof(HeroCreator)
        .GetMethod("CreateNewHero", BindingFlags.Static | BindingFlags.NonPublic)
        .BuildDelegate<Func<CharacterObject, int, Hero>>();

    public static void OverrideCreateNewHero(CharacterObject template, int age, CampaignTime birthDay, Settlement bornSettlement)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using(new AllowedThread())
            {
                Hero newHero = CreateNewHero(template, age);
                newHero.SetBirthDay(birthDay);
                newHero.BornSettlement = bornSettlement;
            }
        });
    }
}
