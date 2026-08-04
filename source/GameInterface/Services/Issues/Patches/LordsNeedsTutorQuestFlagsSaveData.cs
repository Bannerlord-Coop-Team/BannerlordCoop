using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Save-system representation of one Lord Needs a Tutor quest's recorded
/// <c>_doNotForceYoungHeroOutFromClan</c>/<c>_showQuestFailedConversation</c> pair (see
/// <see cref="Interfaces.LordsNeedsTutorQuestFlags"/>). Must survive save/reload since this quest can run for
/// real days/weeks (up to 200) of in-game time.
/// </summary>
internal sealed class LordsNeedsTutorQuestFlagsSaveData
{
    [SaveableField(1)]
    internal Hero QuestGiverHero;

    [SaveableField(2)]
    internal bool DoNotForceYoungHeroOutFromClan;

    [SaveableField(3)]
    internal bool ShowQuestFailedConversation;

    private LordsNeedsTutorQuestFlagsSaveData()
    {
    }

    internal LordsNeedsTutorQuestFlagsSaveData(Hero questGiverHero, bool doNotForceYoungHeroOutFromClan, bool showQuestFailedConversation)
    {
        QuestGiverHero = questGiverHero;
        DoNotForceYoungHeroOutFromClan = doNotForceYoungHeroOutFromClan;
        ShowQuestFailedConversation = showQuestFailedConversation;
    }
}

/// <summary>
/// Registers the Lord Needs a Tutor quest-flags save records with Bannerlord's save system. Base id picked to
/// not collide with the three other registered definers in this project (<c>SiegeAftermathSaveableTypeDefiner</c>
/// = 44_177_000, <c>PlayerCaptivityAttackProtectionSaveableTypeDefiner</c> = 44_182_000,
/// <c>VillageNeedsToolsIssueOwnershipSaveableTypeDefiner</c> = 44_183_000) - confirmed no other
/// <c>SaveableTypeDefiner</c> in the repo uses 44_184_000 before picking it.
/// </summary>
public sealed class LordsNeedsTutorQuestFlagsSaveableTypeDefiner : SaveableTypeDefiner
{
    private const int SaveBaseId = 44_184_000;

    public LordsNeedsTutorQuestFlagsSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(LordsNeedsTutorQuestFlagsSaveData), 1);
    }

    public override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<LordsNeedsTutorQuestFlagsSaveData>));
    }
}
