using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.Actions.ChangeOwnerOfSettlementAction;

namespace GameInterface.Services.Settlements.Patches
{
    /// <summary>
    /// Patches ownership changes of settlements
    /// </summary>
    [HarmonyPatch(typeof(ChangeOwnerOfSettlementAction), "ApplyInternal")]
    public class ChangeOwnerOfSettlementPatch
    {
        static readonly ILogger Logger = LogManager.GetLogger<ChangeOwnerOfSettlementPatch>();

        public static bool Prefix(Settlement settlement, Hero newOwner, Hero capturerHero, ChangeOwnerOfSettlementDetail detail)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client called unmanaged {name}", typeof(ChangeOwnerOfSettlementAction));
                return false;
            }

            MessageBroker.Instance.Publish(settlement,
                new SettlementOwnershipChanged(
                    settlement.StringId,
                    newOwner?.StringId,
                    capturerHero?.StringId,
                    Convert.ToInt32(detail)));

            // Run the original with patches live: each side effect (patrol culling, garrison
            // destruction and creation, governor removal) replicates through its own patch and
            // newly created objects get registered. Clients apply the owner change directly from
            // the message above instead of replaying the whole action.
            return true;
        }
    }
}
