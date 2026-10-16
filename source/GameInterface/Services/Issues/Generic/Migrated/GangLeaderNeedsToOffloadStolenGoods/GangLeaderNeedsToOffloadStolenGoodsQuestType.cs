using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;

internal static class GangLeaderNeedsToOffloadStolenGoodsQuestType
{
    public static readonly PendingRegistry<PropertyOwner<PropertyObject>> OwnerTraitXpProgress = new();
}
