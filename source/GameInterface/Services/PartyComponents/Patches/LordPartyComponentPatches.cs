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

    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var ctor in AccessTools.GetDeclaredConstructors(typeof(LordPartyComponent)))
            yield return ctor;

        // ChangePartyOwner also assigns Owner (runtime ownership/leader changes).
        yield return AccessTools.Method(typeof(LordPartyComponent), nameof(LordPartyComponent.ChangePartyOwner));
    }

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var initArgsField = AccessTools.Field(typeof(LordPartyComponent), nameof(LordPartyComponent._initializationArgs));
        var initArgsIntercept = AccessTools.Method(typeof(LordPartyComponentTranspilers), nameof(InitializationArgsIntercept));

        // Owner is a { get; private set; } auto-property; its 8-byte setter gets JIT-inlined into the
        // ctor, so a prefix on set_Owner never fires. Intercept the call site here instead so the change
        // is reliably replicated. (Field-syncing the backing store wouldn't help — the stfld lives in
        // set_Owner, which is the method that gets inlined away.)
        var ownerSetter = AccessTools.PropertySetter(typeof(LordPartyComponent), nameof(LordPartyComponent.Owner));
        var ownerIntercept = AccessTools.Method(typeof(LordPartyComponentTranspilers), nameof(OwnerSetIntercept));

        foreach (var instruction in instructions)
        {
            if (instruction.StoresField(initArgsField))
            {
                yield return new CodeInstruction(OpCodes.Call, initArgsIntercept);
            }
            else if (instruction.Calls(ownerSetter))
            {
                yield return new CodeInstruction(OpCodes.Call, ownerIntercept);
            }
            else
            {
                yield return instruction;
            }
        }
    }

    public static void OwnerSetIntercept(LordPartyComponent instance, Hero owner)
    {
        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            instance.Owner = owner;
            return;
        }

        if (ModInformation.IsClient)
        {
            Logger.Error("Client updated managed {type}", "LordPartyComponent.Owner");
            instance.Owner = owner;
            return;
        }

        MessageBroker.Instance.Publish(instance, new LordPartyOwnerChanged(instance, owner));

        instance.Owner = owner;
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
            instance._initializationArgs = initArgs;
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
