//using Common;
//using Common.Logging;
//using Common.Messaging;
//using GameInterface.Policies;
//using GameInterface.Services.MapEventParties.Messages;
//using HarmonyLib;
//using Serilog;
//using System;
//using TaleWorlds.CampaignSystem;
//using TaleWorlds.CampaignSystem.Actions;
//using TaleWorlds.CampaignSystem.Party;

//namespace GameInterface.Services.MapEventParties.Patches;

//[HarmonyPatch(typeof(TakePrisonerAction))]
//internal class TakePrisonerActionPatches
//{
//    private static readonly ILogger Logger = LogManager.GetLogger<TakePrisonerActionPatches>();

//    [HarmonyPatch(nameof(TakePrisonerAction.ApplyInternal))]
//    [HarmonyPrefix]
//    private static void PrefixApplyInternal(PartyBase capturerParty, Hero prisonerCharacter)
//    {
//        if (CallOriginalPolicy.IsOriginalAllowed()) return;

//        if (ModInformation.IsClient)
//        {
//            Logger.Error("Client attempted to take prisoner, {CallStack}", Environment.StackTrace);
//            return;
//        }

//        var message = new PrisonerTaken(capturerParty, prisonerCharacter);
//        MessageBroker.Instance.Publish(null, message);
//    }
//}
