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
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Diamond;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Map;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch]
internal class HeroCreationDeletionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCreationDeletionPatches>();
    private static IEnumerable<MethodBase> TargetMethods => typeof(Hero).GetConstructors(BindingFlags.NonPublic | BindingFlags.Public);

    [HarmonyPatch(typeof(Hero), MethodType.Constructor, typeof(string))]
    private static bool Prefix(ref Hero __instance, ref string stringID)
    {
        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created unmanaged {name}", typeof(Hero));
            return true;
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to reslove {name}", nameof(IObjectManager));
            return true;
        }

        if (objectManager.AddNewObject(__instance, out stringID) == false)
        {
            Logger.Error("Unable to register {name} with {objectManager}", __instance.Name, nameof(IObjectManager));
            return true;
        }

        var data = new HeroCreationData(stringID);
        var message = new HeroCreated(data);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    [HarmonyPatch(typeof(Hero), MethodType.Constructor)]
    private static bool Prefix(ref Hero __instance)
    {
        throw new NotImplementedException();
    }

    private static ConstructorInfo ctor_Hero = typeof(Hero).GetConstructor(new Type[] { typeof(string) });
    public static void OverrideCreateNewHero(string heroId)
    {
        Hero newHero = ObjectHelper.SkipConstructor<Hero>();

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to reslove {name}", nameof(IObjectManager));
            return;
        }

        if (objectManager.AddExisting(heroId, newHero) == false)
        {
            Logger.Error("Unable to register {name} with {objectManager}", newHero.Name, nameof(IObjectManager));
            return;
        }

        using (new AllowedThread())
        {
            ctor_Hero.Invoke(newHero, new object[] { heroId });
        }
    }
}