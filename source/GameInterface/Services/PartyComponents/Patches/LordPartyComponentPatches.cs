using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;

namespace GameInterface.Services.PartyComponents.Patches;

[HarmonyPatch(typeof(LordPartyComponent))]
internal class LordPartyComponentPatches
{
}

[HarmonyPatch(typeof(LordPartyComponent))]
public class LordPartyComponentTranspilers
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordPartyComponentTranspilers>();

    public static IEnumerable<MethodBase> TargetMethods() => AccessTools.GetDeclaredConstructors(typeof(LordPartyComponent));


    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> InitializationArgsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var field = AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._initializationArgs));
        var fieldIntercept = AccessTools.Method(typeof(LordPartyComponentTranspilers), nameof(InitializationArgsIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(field))
            {
                yield return new CodeInstruction(OpCodes.Call, fieldIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void InitializationArgsIntercept(LordPartyComponent instance, LordPartyComponent.InitializationArgs initArgs)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance._initializationArgs = initArgs;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {type}", nameof(instance._initializationArgs));
            return;
        }

        var message = new LordPartyComponentInitArgsUpdated(instance, initArgs);
        MessageBroker.Instance.Publish(instance, message);

        instance._initializationArgs = initArgs;
    }
}

[HarmonyPatch(typeof(LordPartyComponent.InitializationArgs))]
internal class LordPartyComponentInitializationArgsPatches
{
    [HarmonyPatch(nameof(LordPartyComponent.InitializationArgs.InitializeLordPartyProperties))]
    [HarmonyPrefix]
    public static bool InitializeLordPartyPropertiesPrefix(ref LordPartyComponent.InitializationArgs __instance, MobileParty mobileParty, Hero owner)
    {
        mobileParty.AddElementToMemberRoster(owner.CharacterObject, 1, true);
        if (mobileParty.IsPlayerParty() || owner.Clan.IsPlayerClan())
        {
            mobileParty.InitializeMobilePartyAtPosition(__instance.Position);
        }
        else
        {
            PartyTemplateObject pt = owner.Clan.IsRebelClan ? owner.Clan.Culture.RebelsPartyTemplate : owner.Clan.DefaultPartyTemplate;
            mobileParty.InitializeMobilePartyAroundPosition(pt, __instance.Position, __instance.SpawnRadius, 0f);
        }
        mobileParty.ItemRoster.Add(new ItemRosterElement(DefaultItems.Grain, MBRandom.RandomInt(15, 30), null));
        if (__instance.SpawnSettlement != null)
        {
            MobileParty.NavigationType navigationType = mobileParty.IsCurrentlyAtSea ? MobileParty.NavigationType.Naval : MobileParty.NavigationType.Default;
            mobileParty.SetMoveGoToSettlement(__instance.SpawnSettlement, navigationType, mobileParty.IsCurrentlyAtSea);
        }

        return false;
    }
}
