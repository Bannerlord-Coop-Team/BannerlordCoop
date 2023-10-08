using Common;
using Common.Extensions;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches
{
    /// <summary>
    /// Patches adding hero to parties
    /// </summary>
    [HarmonyPatch(typeof(AddHeroToPartyAction), "ApplyInternal")]
    public class AddHeroToPartyPatch
    {
        private static readonly AllowedInstance<MobileParty> AllowedInstance = new AllowedInstance<MobileParty>();

        private static readonly Action<Hero, MobileParty, bool> ApplyInternal = 
        typeof(ChangeOwnerOfSettlementAction)
        .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
        .BuildDelegate<Action<Hero, MobileParty, bool>>();
    
        public static bool Prefix(Hero hero, MobileParty newParty, bool showNotification = true)
        {
            if (AllowedInstance.IsAllowed(newParty)) return true;

            MessageBroker.Instance.Publish(newParty, 
                new AddHeroToParty(hero.StringId, newParty.StringId, showNotification));

            return false;
        }
    
        public static void RunOriginalApplyInternal(Hero hero, MobileParty newParty, bool showNotification = true)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = newParty;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(hero, newParty, showNotification);
                }, true);
            }
        }
    
    }
}
