using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Data;
using GameInterface.Services.Heroes.Messages.Lifetime;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches;

/// <summary>
/// Patches for the lifetime of <see cref="Hero"/> objects.
/// </summary>
[HarmonyPatch]
internal class HeroLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroLifetimePatches>();

    [HarmonyPatch(typeof(Hero), MethodType.Constructor, typeof(string))]
    private static bool Prefix(ref Hero __instance, ref string stringID)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);
            return false;
        }

        // Allow method if container is not setup
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return true;
        if (ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker) == false) return true;
        if (ContainerProvider.TryResolve<INetworkConfiguration>(out var configuration) == false) return true;

        // Allow method if registration failed
        if (objectManager.AddNewObject(__instance, out stringID) == false) return true;

        var data = new HeroCreationData(stringID);
        var message = new HeroCreated(data);

        using(new MessageTransaction<NewHeroSynced>(messageBroker, configuration.ObjectCreationTimeout))
        {
            MessageBroker.Instance.Publish(__instance, message);
        }

        return true;
    }

    [HarmonyPatch(typeof(Hero), MethodType.Constructor)]
    private static bool Prefix(ref Hero __instance)
    {
        // Allow method if it was determined to be allowed
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        Logger.Error("Client created unmanaged {name}\n"
                + "Callstack: {callstack}", typeof(Hero), Environment.StackTrace);

        return true;
    }

    private static ConstructorInfo ctor_Hero = typeof(Hero).GetConstructor(new Type[] { typeof(string) });
    public static void OverrideCreateNewHero(string heroId)
    {
        Hero newHero = ObjectHelper.SkipConstructor<Hero>();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return;

        if (objectManager.AddExisting(heroId, newHero) == false) return;

        using (new AllowedThread())
        {
            ctor_Hero.Invoke(newHero, new object[] { heroId });
        }
    }
}