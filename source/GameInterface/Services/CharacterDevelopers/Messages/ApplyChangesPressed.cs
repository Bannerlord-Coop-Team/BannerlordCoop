using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper.PerkSelection;
using TaleWorlds.Library;

namespace GameInterface.Services.CharacterDevelopers.Messages;

public record ApplyChangesPressed : IEvent
{
    public HeroDeveloper HeroDeveloper;
    public PerkSelectionVM Perks;
    public MBBindingList<CharacterAttributeItemVM> Attributes;
    public MBBindingList<SkillVM> Skills;

    public ApplyChangesPressed(HeroDeveloper heroDeveloper, PerkSelectionVM perks, MBBindingList<CharacterAttributeItemVM> attributes, MBBindingList<SkillVM> skills)
    {
        HeroDeveloper = heroDeveloper;
        Perks = perks;
        Attributes = attributes;
        Skills = skills;
    }
}