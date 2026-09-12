using Common.Messaging;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Library;

namespace GameInterface.Services.CharacterDevelopers.Messages;

public readonly struct ApplyChanges : IEvent
{
    public readonly HeroDeveloper HeroDeveloper;
    public readonly PerkSelectionVM Perks;
    public readonly MBBindingList<CharacterAttributeItemVM> Attributes;
    public readonly MBBindingList<SkillVM> Skills;

    public ApplyChanges(
        HeroDeveloper heroDeveloper,
        PerkSelectionVM perks,
        MBBindingList<CharacterAttributeItemVM> attributes,
        MBBindingList<SkillVM> skills)
    {
        HeroDeveloper = heroDeveloper;
        Perks = perks;
        Attributes = attributes;
        Skills = skills;
    }
}