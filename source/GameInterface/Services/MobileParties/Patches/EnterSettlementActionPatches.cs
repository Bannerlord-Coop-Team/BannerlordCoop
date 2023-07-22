using Common.Messaging;
using Common.Util;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using HarmonyLib;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Patches
{
    [HarmonyPatch(typeof(EnterSettlementAction))]
    internal class EnterSettlementActionPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(nameof(EnterSettlementAction.ApplyForParty))]
        private static bool ApplyForPartyPrefix(ref MobileParty mobileParty, ref Settlement settlement)
        {
            if (mobileParty.IsAllowed()) return true;

            if (mobileParty.StringId == null)
            {
                ;
            }

            var message = new PartyEnterSettlementAttempted(settlement.StringId, mobileParty.StringId);
            MessageBroker.Instance.Publish(mobileParty, message);

            return false;
        }

        public static void OverrideApplyForParty(MobileParty mobileParty, Settlement settlement)
        {
            using var allowedInstance = mobileParty.GetAllowInstance();
            allowedInstance.IsAllowed = true;
            EnterSettlementAction.ApplyForParty(mobileParty, settlement);
        }
    }

    internal static class MobilePartyExtensions
    {
        private static ConditionalWeakTable<MobileParty, AllowInstance> PartyAllowInstanceExtension = new();

        public static AllowInstance GetAllowInstance(this MobileParty mobileParty)
        {
            return PartyAllowInstanceExtension.GetOrCreateValue(mobileParty);
        }

        public static bool IsAllowed(this MobileParty mobileParty)
        {
            var allowedInstance = PartyAllowInstanceExtension.GetOrCreateValue(mobileParty);

            return allowedInstance.IsAllowed;
        }
    }


    internal class AllowInstance : IDisposable
    {
        private readonly SemaphoreSlim _sem = new SemaphoreSlim(1);
        public bool IsAllowed
        {
            get => _isAllowed;
            set
            {
                _sem.Wait();
                _isAllowed = value;
            }
        }

        private bool _isAllowed = false;

        public void Dispose()
        {
            _isAllowed = false;
            _sem.Release();
        }
    }
}


