using Common.Util;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic.CreationCapture;

public sealed class CreationCaptureRunner<TIssue, TFields> where TIssue : IssueBase
{
    private readonly ICreationCaptureStrategy<TIssue, TFields> _strategy;
    private readonly IssueBase.IssueFrequency _frequency;

    public CreationCaptureRunner(ICreationCaptureStrategy<TIssue, TFields> strategy, IssueBase.IssueFrequency frequency = IssueBase.IssueFrequency.Rare)
    {
        _strategy = strategy;
        _frequency = frequency;
    }

    public bool TryCapture(TIssue issue, out TFields fields) => _strategy.TryCaptureFields(issue, out fields);

    public TIssue ConstructAndRegisterReplicated(Hero owner, TFields fields, Action<TIssue, TFields> afterRegistered = null)
    {
        var issue = _strategy.ConstructReplicated(owner, fields);
        RegisterReplicated(owner, issue);
        afterRegistered?.Invoke(issue, fields);
        return issue;
    }

    private void RegisterReplicated(Hero owner, TIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(TIssue), _frequency);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
