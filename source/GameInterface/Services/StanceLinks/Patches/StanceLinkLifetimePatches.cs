using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Serilog;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Core.ViewModelCollection.CharacterViewModel;
using GameInterface.Policies;
using System;
using GameInterface.Services.Stances.Messages.Lifetime;

namespace GameInterface.Services.Stances.Patches;

/// <summary>
/// Patches required for creating a StanceLink
/// </summary>
[HarmonyPatch]
internal class StanceLinkPatches
{
    private static ILogger Logger = LogManager.GetLogger<Kingdom>();

    //[HarmonyPatch(typeof(StanceLink), MethodType.Constructor, typeof(StanceType), typeof(IFaction), typeof(IFaction))]
    //[HarmonyPrefix]
    //private static bool CreateStanceLinkPrefix(ref StanceLink __instance, StanceType stanceType, IFaction faction1, IFaction faction2)
    //{
    //    // Call original if we call this function
    //    if (CallOriginalPolicy.IsOriginalAllowed()) return true;

    //    if (ModInformation.IsClient)
    //    {
    //        Logger.Error("Client created managed {name}", typeof(StanceLink));
    //        return true;
    //    }


    //    var message = new StanceLinkCreated(__instance, stanceType, faction1, faction2);

    //    MessageBroker.Instance.Publish(__instance, message);


    //    return true;
    //}
}
