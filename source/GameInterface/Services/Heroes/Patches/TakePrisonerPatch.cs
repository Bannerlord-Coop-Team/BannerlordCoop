using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Patches
{
    [HarmonyPatch(typeof(TakePrisonerAction))]
    public class TakePrisonerPatch
    {
        private static readonly Action<PartyBase, Hero, bool> ApplyInternal =
            typeof(TakePrisonerAction)
            .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
            .BuildDelegate<Action<PartyBase, Hero, bool>>();

        [HarmonyPrefix]
        [HarmonyPatch("ApplyInternal")]
        private static bool Prefix(PartyBase capturerParty, Hero prisonerCharacter, bool isEventCalled = true)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient && prisonerCharacter != Hero.MainHero) return false;

            MessageBroker.Instance.Publish(capturerParty, new PrisonerTaken(
                capturerParty.MobileParty.StringId,
                prisonerCharacter.StringId,
                isEventCalled));

            return ModInformation.IsServer;
        }

        public static void RunOriginalApplyInternal(PartyBase capturerParty, Hero prisonerCharacter, bool isEventCalled)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                using (new AllowedThread())
                {
                    ApplyInternal.Invoke(capturerParty, prisonerCharacter, isEventCalled);
                }
            });
        }
    }
}