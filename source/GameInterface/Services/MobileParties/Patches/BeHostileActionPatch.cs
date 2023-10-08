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
    [HarmonyPatch(typeof(BeHostileAction), "ApplyInternal")]
    public class BeHostileActionPatch
    {
        private static readonly AllowedInstance<PartyBase> AllowedInstance = new AllowedInstance<PartyBase>();

        private static readonly Action<PartyBase, PartyBase, float> ApplyInternal = 
        typeof(ChangeOwnerOfSettlementAction)
        .GetMethod("ApplyInternal", BindingFlags.NonPublic | BindingFlags.Static)
        .BuildDelegate<Action<PartyBase, PartyBase, float>>();
    
        public static bool Prefix(PartyBase attackerParty, PartyBase defenderParty, float value)
        {
            if (AllowedInstance.IsAllowed(attackerParty)) return true;

            MessageBroker.Instance.Publish(attackerParty, 
                new LocalBecomeHostile(attackerParty.MobileParty.StringId, defenderParty.MobileParty.StringId, value));

            return false;
        }
    
        public static void RunOriginalApplyInternal(PartyBase attackerParty, PartyBase defenderParty, float value)
        {
            using (AllowedInstance)
            {
                AllowedInstance.Instance = attackerParty;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    ApplyInternal.Invoke(attackerParty, defenderParty, value);
                }, true);
            }
        }
    
    }
}
