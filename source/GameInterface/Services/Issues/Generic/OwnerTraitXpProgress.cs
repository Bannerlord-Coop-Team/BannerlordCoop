using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic;

internal interface IOwnerTraitXpProgress
{
    void Apply(Hero owner, TraitObject trait, int xpValue);
}

internal sealed class OwnerTraitXpProgress : IOwnerTraitXpProgress
{
    private readonly PendingRegistry<PropertyOwner<PropertyObject>> registry;

    public OwnerTraitXpProgress(PendingRegistry<PropertyOwner<PropertyObject>> registry)
    {
        this.registry = registry;
    }

    public void Apply(Hero owner, TraitObject trait, int xpValue)
    {
        if (owner == null) return;

        if (!registry.TryGet(owner, out var progress))
        {
            progress = new PropertyOwner<PropertyObject>();
        }
        var traitLevelBefore = owner.GetTraitLevel(trait);
        if (progress.GetPropertyValue(trait) == 0)
        {
            progress.SetPropertyValue(trait, Campaign.Current.Models.CharacterDevelopmentModel.GetTraitXpRequiredForTraitLevel(trait, traitLevelBefore));
        }
        Campaign.Current.Models.CharacterDevelopmentModel.GetTraitLevelForTraitXp(
            owner, trait, xpValue + progress.GetPropertyValue(trait), out var traitLevel, out var traitXp);
        progress.SetPropertyValue(trait, traitXp);
        registry.Set(owner, progress);
        if (traitLevel != traitLevelBefore)
        {
            owner.SetTraitLevel(trait, traitLevel);
        }
    }
}
