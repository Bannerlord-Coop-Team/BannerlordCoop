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
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Map;

namespace GameInterface.Services.Heroes.Patches;

[HarmonyPatch(typeof(HeroCreator), "CreateNewHero")]
internal class HeroCreationDeletionPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeroCreationDeletionPatches>();
    private static bool Prefix() => ModInformation.IsServer;

    private static ConstructorInfo ctor_Hero => typeof(Hero).GetConstructors().First();
    private static MethodInfo intercept_Method => typeof(HeroCreationDeletionPatches)
        .GetMethod(nameof(CreateHeroIntecept), BindingFlags.NonPublic | BindingFlags.Static);
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            if (instruction.opcode == OpCodes.Newobj && instruction.operand as ConstructorInfo == ctor_Hero)
            {
                yield return new CodeInstruction(OpCodes.Call, intercept_Method);
                continue;
            }

            yield return instruction;
        }
    }

    private static Hero CreateHeroIntecept(string stringId)
    {
        Hero newHero;

        // Skip if we called it
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            if (SharedThreadData.TryGetValue(AllowedThread.CurrentThreadId, out newHero) == false)
            {
                Logger.Error("Data was not shared between threads");
                newHero = ObjectHelper.SkipConstructor<Hero>();
            }
            SharedThreadData.Remove(AllowedThread.CurrentThreadId);
            return newHero;
        }
        else
        {
            if (ModInformation.IsClient)
            {
                Logger.Fatal("Client created unregistered {name}", typeof(Hero));
            }

            newHero = ObjectHelper.SkipConstructor<Hero>();
        }

        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
        {
            Logger.Error("Unable to reslove {name}", nameof(IObjectManager));
            return newHero;
        }

        if (objectManager.AddNewObject(newHero, out var newId) == false)
        {
            Logger.Error("Unable to register {name} with {objectManager}", newHero.Name, nameof(IObjectManager));
            return newHero;
        }

        var data = new HeroCreationData(newId);
        var message = new HeroCreated(data);

        MessageBroker.Instance.Publish(newHero, message);

        return newHero;
    }

    internal static Func<CharacterObject, int, Hero> CreateNewHero = typeof(HeroCreator)
        .GetMethod("CreateNewHero", BindingFlags.Static | BindingFlags.NonPublic)
        .BuildDelegate<Func<CharacterObject, int, Hero>>();

    private static Dictionary<int, Hero> SharedThreadData = new Dictionary<int, Hero>();
    public static Hero OverrideCreateNewHero(string heroId)
    {
        using(new AllowedThread())
        {
            Hero newHero = ObjectHelper.SkipConstructor<Hero>();

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false)
            {
                Logger.Error("Unable to reslove {name}", nameof(IObjectManager));
                return newHero;
            }

            if (objectManager.AddExisting(heroId, newHero) == false)
            {
                Logger.Error("Unable to register {name} with {objectManager}", newHero.Name, nameof(IObjectManager));
                return newHero;
            }

            SharedThreadData.Add(AllowedThread.CurrentThreadId, newHero);

            return newHero;
        }
    }
}