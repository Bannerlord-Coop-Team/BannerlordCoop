using System;
using System.Reflection;

namespace GameInterface.Services.Issues.Generic.CreationCapture;

/// <summary>
/// Reflection-based, single-field instantiation of <see cref="ICreationCaptureStrategy{TIssue,TFields}"/> -
/// covers the dominant real shape: one rolled reference-typed field, force-written via
/// <see cref="FieldInfo.SetValue"/>. <typeparamref name="TValue"/> is typically a reference type (e.g.
/// <see cref="TaleWorlds.Core.ItemObject"/>) captured off one <c>readonly</c> field.
/// </summary>
public sealed class FieldForceCreationCapture<TIssue, TValue> : ICreationCaptureStrategy<TIssue, TValue>
{
    private readonly FieldInfo _field;
    private readonly Func<TaleWorlds.CampaignSystem.Hero, TIssue> _constructUnforced;

    /// <param name="field">The reflected field to read at capture time and force-write at replication time.</param>
    /// <param name="constructUnforced">Builds a new <typeparamref name="TIssue"/> the normal way (which
    /// independently rolls its own value for <paramref name="field"/> as an unavoidable side effect of the real
    /// constructor).</param>
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
