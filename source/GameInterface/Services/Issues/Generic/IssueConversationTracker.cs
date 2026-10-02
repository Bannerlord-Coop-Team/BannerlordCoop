using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

public interface IIssueConversationTracker
{
    void Register(string issueGiverId, string controllerId, int generation);
    bool TryGetTrackedRequester(string issueGiverId, string controllerId, out int generation);
    void Clear(string issueGiverId);
    IssueBase AlternativePickerIssue { get; }
    int AlternativePickerGeneration { get; }
    void TrackAlternativePicker(IssueBase issue, int generation);
}

internal sealed class IssueConversationTracker : IIssueConversationTracker
{
    private readonly Dictionary<(string IssueGiverId, string ControllerId), int> tracked = new();

    public IssueBase AlternativePickerIssue { get; private set; }
    public int AlternativePickerGeneration { get; private set; }

    public void TrackAlternativePicker(IssueBase issue, int generation)
    {
        AlternativePickerIssue = issue;
        AlternativePickerGeneration = generation;
    }

    public void Register(string issueGiverId, string controllerId, int generation)
    {
        if (issueGiverId == null || controllerId == null) return;

        tracked[(issueGiverId, controllerId)] = generation;
    }

    public bool TryGetTrackedRequester(string issueGiverId, string controllerId, out int generation)
    {
        if (issueGiverId != null && controllerId != null && tracked.TryGetValue((issueGiverId, controllerId), out generation))
        {
            return true;
        }

        generation = 0;
        return false;
    }

    public void Clear(string issueGiverId)
    {
        if (issueGiverId == null) return;

        var stale = new List<(string IssueGiverId, string ControllerId)>();
        foreach (var key in tracked.Keys)
        {
            if (key.IssueGiverId == issueGiverId) stale.Add(key);
        }

        foreach (var key in stale)
        {
            tracked.Remove(key);
        }
    }
}
