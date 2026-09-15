using System;
using GameInterface.Services.Inventory.Data;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Villages.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Villages.Interfaces;

public interface IVillageHostileActionInterface : IGameAbstraction
{
    void RequestHostileAction(VillageHostileAction action);
    void BeginHostileActionPresentation(VillageHostileAction action);
    bool CanStartHostileAction(MobileParty mobileParty, Settlement settlement, VillageHostileAction action, out VillageHostileActionDeniedReason reason);
    void ApplyHostileAction(MobileParty mobileParty, Settlement settlement, VillageHostileAction action);
    void ApplyForceActionOutcome(MapEvent mapEvent, VillageHostileAction action);
    void ApproveMapEventStart(PartyBase attacker, Settlement settlement, VillageHostileAction action);
    bool TryConsumeApprovedMapEventStart(PartyBase attacker, PartyBase defender, BattleCreationFlags flags, out VillageHostileActionDeniedReason reason);
    void CancelMapEventStartApprovals(PartyBase attacker);
    bool TryGetForceActionCooldown(Settlement settlement, out CampaignTime cooldownUntil);
    VillageHostileActionCooldownData[] GetActiveCooldowns();
    void ApplyCooldowns(VillageHostileActionCooldownData[] cooldowns);
    ForceTransferPoolData AuthorizeForceTransfer(
        VillageHostileAction action,
        string partyId,
        string settlementId,
        ItemRosterElementData[] suppliesItems,
        string troopId,
        int troopCount,
        DateTime? utcNow = null);
    bool TryConsumeForceTransfer(string requestId, string partyId, out ForceTransferPoolData pool);
    bool TryPeekForceTransfer(string requestId, string partyId, out ForceTransferPoolData pool);
    bool HasPendingForceTransferForParty(string partyId);
    void GrantForceTransferPool(MobileParty attacker, ForceTransferPoolData pool);
    bool IsForceTransferPoolValid(ForceTransferPoolData pool);
    void OpenForceTransferLootScreen(ForceTransferPoolData pool);
}
