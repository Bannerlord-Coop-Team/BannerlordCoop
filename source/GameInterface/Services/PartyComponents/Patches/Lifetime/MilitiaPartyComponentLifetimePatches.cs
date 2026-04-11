using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.PartyComponents.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.PartyComponents.Patches.Lifetime;


/// <summary>
/// Harmony patches for the lifetime of a <see cref="MilitiaPartyComponent"/> object
/// </summary>
[HarmonyPatch]
internal class MilitiaPartyComponentLifetimePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MilitiaPartyComponentLifetimePatches>();

    [HarmonyPatch(typeof(MilitiaPartyComponent), MethodType.Constructor, typeof(Settlement), typeof(MilitiaPartyComponent.InitializationArgs))]
    [HarmonyPrefix]
    private static bool Prefix(MilitiaPartyComponent __instance, Settlement settlement, MilitiaPartyComponent.InitializationArgs args)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (ModInformation.IsClient)
        {
            Logger.Error("Client created managed {name}", typeof(MilitiaPartyComponent));
            return true;
        }

        var message = new PartyComponentCreated(__instance, settlement.StringId);

        MessageBroker.Instance.Publish(__instance, message);

        return true;
    }

    /// <summary>
    /// Logs changes to <see cref="MilitiaPartyComponent.Settlement"/> so we can trace
    /// when/why it becomes null during multiplayer sync.
    /// </summary>
    [HarmonyPatch(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.Settlement), MethodType.Setter)]
    [HarmonyPrefix]
    private static void SettlementSetterPrefix(MilitiaPartyComponent __instance, Settlement value)
    {
        var current = __instance.Settlement;
        if (value == null)
        {
            Logger.Warning("MilitiaPartyComponent.Settlement being set to NULL (was: {PreviousSettlement}, MobileParty: {Party}, IsClient: {IsClient})",
                current?.StringId ?? "null",
                __instance.MobileParty?.StringId ?? "null",
                ModInformation.IsClient);
        }
        //else if (current != value)
        //{
        //    Logger.Debug("MilitiaPartyComponent.Settlement changed: {Previous} -> {Next} (MobileParty: {Party}, IsClient: {IsClient})",
        //        current?.StringId ?? "null",
        //        value.StringId,
        //        __instance.MobileParty?.StringId ?? "null",
        //        ModInformation.IsClient);
        //}
    }

    /// <summary>
    /// Guards against NullReferenceException in <see cref="MilitiaPartyComponent.PartyOwner"/>
    /// when <see cref="MilitiaPartyComponent.Settlement"/> or its <see cref="Settlement.OwnerClan"/>
    /// is null during a multiplayer sync transition.
    /// </summary>
    [HarmonyPatch(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.PartyOwner), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool PartyOwnerPrefix(MilitiaPartyComponent __instance, ref Hero __result)
    {
        if (__instance.Settlement == null || __instance.Settlement.OwnerClan == null)
        {
            Logger.Warning("MilitiaPartyComponent.PartyOwner accessed with null Settlement/OwnerClan (MobileParty: {Party}, Settlement: {Settlement}, IsClient: {IsClient})",
                __instance.MobileParty?.StringId ?? "null",
                __instance.Settlement?.StringId ?? "null",
                ModInformation.IsClient);
            __result = null;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Guards against NullReferenceException in <see cref="MilitiaPartyComponent.Name"/>
    /// when <see cref="MilitiaPartyComponent.Settlement"/> is null during a multiplayer sync transition.
    /// </summary>
    [HarmonyPatch(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.Name), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool NamePrefix(MilitiaPartyComponent __instance, ref TextObject __result)
    {
        if (__instance.Settlement == null)
        {
            Logger.Warning("MilitiaPartyComponent.Name accessed with null Settlement (MobileParty: {Party}, IsClient: {IsClient})",
                __instance.MobileParty?.StringId ?? "null",
                ModInformation.IsClient);
            __result = new TextObject("{=!}Militia");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Guards against NullReferenceException in <see cref="MilitiaPartyComponent.GetDefaultComponentBanner"/>
    /// when Settlement is null during sync.
    /// </summary>
    [HarmonyPatch(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.GetDefaultComponentBanner))]
    [HarmonyPrefix]
    private static bool GetDefaultComponentBannerPrefix(MilitiaPartyComponent __instance, ref Banner __result)
    {
        if (__instance.Settlement == null)
        {
            Logger.Warning("MilitiaPartyComponent.GetDefaultComponentBanner accessed with null Settlement (MobileParty: {Party}, IsClient: {IsClient})",
                __instance.MobileParty?.StringId ?? "null",
                ModInformation.IsClient);
            __result = null;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Guards against NullReferenceException in <see cref="MilitiaPartyComponent.OnFinalize"/>.
    /// The base game does <c>this.Settlement.MilitiaPartyComponent = null</c>, but on clients
    /// <see cref="MilitiaPartyComponent.Settlement"/> may already be null when the party is
    /// destroyed, causing the DynamicSync field intercept to crash on a null instance.
    /// </summary>
    [HarmonyPatch(typeof(MilitiaPartyComponent), "OnFinalize")]
    [HarmonyPrefix]
    private static bool OnFinalizePrefix(MilitiaPartyComponent __instance)
    {
        if (__instance.Settlement == null)
        {
            Logger.Warning("MilitiaPartyComponent.OnFinalize called with null Settlement (MobileParty: {Party}, IsClient: {IsClient}) — skipping Settlement.MilitiaPartyComponent = null",
                __instance.MobileParty?.StringId ?? "null",
                ModInformation.IsClient);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Guards against NullReferenceException in <see cref="MilitiaPartyComponent.OnInitialize"/>
    /// which does <c>this.Settlement.MilitiaPartyComponent = this</c>.
    /// Settlement may be null on clients if the sync hasn't arrived yet.
    /// </summary>
    [HarmonyPatch(typeof(MilitiaPartyComponent), "OnInitialize")]
    [HarmonyPrefix]
    private static bool OnInitializePrefix(MilitiaPartyComponent __instance)
    {
        if (__instance.Settlement == null)
        {
            Logger.Warning("MilitiaPartyComponent.OnInitialize called with null Settlement (MobileParty: {Party}, IsClient: {IsClient}) — skipping Settlement.MilitiaPartyComponent = this",
                __instance.MobileParty?.StringId ?? "null",
                ModInformation.IsClient);
            return false;
        }
        return true;
    }
}