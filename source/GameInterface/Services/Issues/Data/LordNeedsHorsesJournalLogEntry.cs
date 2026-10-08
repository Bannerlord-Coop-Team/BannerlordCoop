using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.Issues.Data;

internal sealed class LordNeedsHorsesJournalLogEntry : JournalLogEntry
{
    [SaveableField(100)]
    public readonly string OwnerControllerId;

    public LordNeedsHorsesJournalLogEntry(string ownerControllerId, TextObject title, Hero giver,
        params MBObjectBase[] relatedObjects) : base(title, giver, null, false, relatedObjects)
    {
        OwnerControllerId = ownerControllerId;
    }
}

public sealed class LordNeedsHorsesJournalSaveableTypeDefiner : SaveableTypeDefiner
{
    public LordNeedsHorsesJournalSaveableTypeDefiner() : base(44_192_000)
    {
    }

    public override void DefineClassTypes() => AddClassDefinition(typeof(LordNeedsHorsesJournalLogEntry), 1);
}
