using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Services.Issues.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

internal interface IArtisanProductJournal
{
    ArtisanProductLogEntry[] Pack(IEnumerable<JournalLog> entries);
    JournalLog[] Unpack(ArtisanProductLogEntry[] entries);
    JournalLog[] Merge(IReadOnlyList<JournalLog> previous, ArtisanProductLogEntry[] entries);
}

internal sealed class ArtisanProductJournal : IArtisanProductJournal
{
    private readonly IBinaryPackageFactory packages;

    public ArtisanProductJournal(IBinaryPackageFactory packages) => this.packages = packages;

    public ArtisanProductLogEntry[] Pack(IEnumerable<JournalLog> entries)
        => entries.Select(log => new ArtisanProductLogEntry(log.LogTime, PackText(log.LogText),
            PackText(log.TaskName), log.CurrentProgress, log.Range, (int)log.Type)).ToArray();

    public JournalLog[] Unpack(ArtisanProductLogEntry[] entries)
        => (entries ?? Array.Empty<ArtisanProductLogEntry>()).Select(log => new JournalLog(log.Time,
            UnpackText(log.Text), UnpackText(log.TaskName), log.Progress, log.Range, (LogType)log.Type)).ToArray();

    public JournalLog[] Merge(IReadOnlyList<JournalLog> previous, ArtisanProductLogEntry[] entries)
    {
        var result = Unpack(entries);
        for (var i = 0; i < Math.Min(previous.Count, result.Length); i++)
        {
            var old = previous[i];
            var incoming = entries[i];
            if (old.LogTime != incoming.Time || old.Range != incoming.Range || (int)old.Type != incoming.Type ||
                !(PackText(old.LogText) ?? Array.Empty<byte>()).SequenceEqual(incoming.Text ?? Array.Empty<byte>()) ||
                !(PackText(old.TaskName) ?? Array.Empty<byte>()).SequenceEqual(incoming.TaskName ?? Array.Empty<byte>())) continue;
            // The journal's unread list and task pointers refer to these objects.
            old.UpdateCurrentProgress(incoming.Progress);
            result[i] = old;
        }
        return result;
    }

    private byte[] PackText(TextObject text)
        => text == null ? null : BinaryPackageSerializer.Serialize(packages.GetBinaryPackage<TextObjectBinaryPackage>(text));

    private TextObject UnpackText(byte[] bytes)
        => bytes == null || bytes.Length == 0 ? null : BinaryPackageSerializer.Deserialize<TextObjectBinaryPackage>(bytes).Unpack<TextObject>(packages);
}
