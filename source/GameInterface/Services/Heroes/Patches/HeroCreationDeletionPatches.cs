using Common;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroCreator), "CreateNewHero")]
internal class HeroCreationDeletionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCreationDeletionPatches>();
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // Only run on server
        return ModInformation.IsServer;
    }
    

    private static void Postfix(ref Hero __result, ref CharacterObject template, ref int age)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to reslove {name}", nameof(IObjectManager));
            return;
        }

        if (objectManager.AddNewObject(__result, out var newId) == false)
        {
            Logger.Error("Unable to register {name} with {objectManager}", __result.Name, nameof(IObjectManager));
            return;
        }

        var birthday = __result.BirthDay;
        var bornSettlement = __result.BornSettlement;
        var data = new HeroCreationData(template, age, birthday, bornSettlement, newId);
        var message = new HeroCreated(data);

        MessageBroker.Instance.Publish(__result, message);
    }

    internal static Func<CharacterObject, int, Hero> CreateNewHero = typeof(HeroCreator)
        .GetMethod("CreateNewHero", BindingFlags.Static | BindingFlags.NonPublic)
        .BuildDelegate<Func<CharacterObject, int, Hero>>();

    public static Hero OverrideCreateNewHero(CharacterObject template, int age, CampaignTime birthDay, Settlement bornSettlement)
    {
        using(new AllowedThread())
        {
            Hero newHero = CreateNewHero(template, age);
            newHero.SetBirthDay(birthDay);
            newHero.BornSettlement = bornSettlement;
            return newHero;
        }
    }
}
