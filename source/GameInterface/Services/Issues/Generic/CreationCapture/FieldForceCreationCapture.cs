using System;
using System.Reflection;

namespace GameInterface.Services.Issues.Generic.CreationCapture;

public sealed class FieldForceCreationCapture<TIssue, TValue> : ICreationCaptureStrategy<TIssue, TValue>
{
    private readonly FieldInfo _field;
    private readonly Func<TaleWorlds.CampaignSystem.Hero, TIssue> _constructUnforced;

    public FieldForceCreationCapture(FieldInfo field, Func<TaleWorlds.CampaignSystem.Hero, TIssue> constructUnforced)
    {
        _field = field ?? throw new ArgumentNullException(nameof(field));
        _constructUnforced = constructUnforced ?? throw new ArgumentNullException(nameof(constructUnforced));
    }

    public bool TryCaptureFields(TIssue issue, out TValue fields)
    {
        fields = default;
        if (issue == null) return false;

        var value = _field.GetValue(issue);
        if (value == null) return false;

        fields = (TValue)value;
        return true;
    }

    public TIssue ConstructReplicated(TaleWorlds.CampaignSystem.Hero owner, TValue fields)
    {
        var issue = _constructUnforced(owner);
        _field.SetValue(issue, fields);
        return issue;
    }
}
