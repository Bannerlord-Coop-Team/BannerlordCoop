//using Common;
//using Common.Logging;
//using Common.Messaging;
//using GameInterface.Policies;
//using GameInterface.Services.MapEventParties.Messages;
//using GameInterface.Services.MobileParties.Messages.Lifetime;
//using HarmonyLib;
//using Serilog;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using TaleWorlds.CampaignSystem.Actions;

//namespace GameInterface.Services.MobileParties.Patches;

//[HarmonyPatch(typeof(DestroyPartyAction))]
//internal class DestroyPartyActionPatches
//{
//    private static readonly ILogger Logger = LogManager.GetLogger<DestroyPartyActionPatches>();

//    [HarmonyPatch(nameof(DestroyPartyAction.Apply))]
//    [HarmonyPrefix]
//    private static void PrefixApply()
//    {
//        if (CallOriginalPolicy.IsOriginalAllowed()) return;

//        if (ModInformation.IsClient)
//        {
//            Logger.Error("Client called managed method {methodName}", $"{nameof(TakePrisonerAction)}.{nameof(TakePrisonerAction.Apply)}");
//            return;
//        }

//        var message = new PartyDestroyed(capturerParty, prisonerCharacter);
//        MessageBroker.Instance.Publish(null, message);
//    }
//}
