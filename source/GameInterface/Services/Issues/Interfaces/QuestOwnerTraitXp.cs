using GameInterface.Services.Issues.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

public interface IQuestOwnerTraitXp
{
    void Apply(Hero owner, TraitObject trait, int xpValue);
}

internal sealed class QuestOwnerTraitXp : IQuestOwnerTraitXp
{
    private readonly PendingRegistry<PropertyOwner<PropertyObject>> progressByOwner;

    public QuestOwnerTraitXp(PendingRegistry<PropertyOwner<PropertyObject>> progressByOwner)
    {
        this.progressByOwner = progressByOwner;
    }

    public void Apply(Hero owner, TraitObject trait, int xpValue)
    {
        if (owner == null) return;
        if (!progressByOwner.TryGet(owner, out var progress))
            progress = new PropertyOwner<PropertyObject>();

        var previousLevel = owner.GetTraitLevel(trait);
        if (progress.GetPropertyValue(trait) == 0)
            progress.SetPropertyValue(trait, Campaign.Current.Models.CharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel(trait, previousLevel));
        Campaign.Current.Models.CharacterDevelopmentModel.GetTraitLevelForTraitXp(
            owner, trait, xpValue + progress.GetPropertyValue(trait), out var level, out var remainingXp);
        progress.SetPropertyValue(trait, remainingXp);
        progressByOwner.Set(owner, progress);
        if (level != previousLevel)
            owner.SetTraitLevel(trait, level);
    }
}
