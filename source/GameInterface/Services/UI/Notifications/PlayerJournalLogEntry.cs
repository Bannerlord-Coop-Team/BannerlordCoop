using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace GameInterface.Services.UI.Notifications;

/// <summary>
/// Retains the player who owns a journal archive when the server save is loaded by another client.
/// </summary>
internal sealed class PlayerJournalLogEntry : JournalLogEntry
{
    [SaveableField(100)]
    internal readonly string OwnerControllerId;

    internal PlayerJournalLogEntry(JournalLogEntry entry, string ownerControllerId, params MBObjectBase[] relatedObjects)
        : base(entry.Title, entry.RelatedHero, entry.Antagonist, entry.IsSpecial, relatedObjects)
    {
        OwnerControllerId = ownerControllerId;
        Id = entry.Id;
        GameTime = entry.GameTime;
        _entries = entry._entries;
        _lastIssueStatus = entry._lastIssueStatus;
        _questCompletionDetail = entry._questCompletionDetail;
    }
}

/// <summary>
/// Registers player-owned journal entries with the campaign save system.
/// </summary>
public sealed class PlayerJournalSaveableTypeDefiner : SaveableTypeDefiner
{
    public PlayerJournalSaveableTypeDefiner() : base(44_186_004)
    {
    }

    public override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PlayerJournalLogEntry), 1);
    }
}
