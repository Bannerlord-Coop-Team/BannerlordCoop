using GameInterface.Services.HeroDevelopers.Messages;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Patches;

/// <summary>
/// Captures the ordered inner mutations produced synchronously by one <see cref="HeroDeveloper.AddSkillXp"/> call.
/// </summary>
internal sealed class HeroDeveloperBatchScope
{
    [ThreadStatic]
    private static HeroDeveloperBatchScope current;

    private readonly HeroDeveloperBatchScope parent;
    private readonly HeroDeveloper heroDeveloper;
    private readonly List<HeroDeveloperOperation> operations = new();
    private bool active = true;

    private HeroDeveloperBatchScope(HeroDeveloper heroDeveloper)
    {
        this.heroDeveloper = heroDeveloper;
        parent = current;
        current = this;
    }

    public static HeroDeveloperBatchScope Begin(HeroDeveloper heroDeveloper) => new(heroDeveloper);

    public static bool TryEnqueue(RawXpGain message) =>
        TryEnqueue(message.HeroDeveloper, HeroDeveloperOperation.RawXpGain(message.RawXp, message.ShouldNotify));

    public static bool TryEnqueue(SkillXpSet message) =>
        TryEnqueue(message.HeroDeveloper, HeroDeveloperOperation.SkillXpSet(message.SkillObject, message.Value));

    public static bool TryEnqueue(SkillLevelChange message) =>
        TryEnqueue(
            message.HeroDeveloper,
            HeroDeveloperOperation.SkillLevelChange(message.SkillObject, message.ChangeAmount, message.ShouldNotify));

    private static bool TryEnqueue(HeroDeveloper developer, HeroDeveloperOperation operation)
    {
        if (current == null || !ReferenceEquals(current.heroDeveloper, developer)) return false;

        current.operations.Add(operation);
        return true;
    }

    public HeroDeveloperBatch Complete()
    {
        if (!TryClose()) return null;
        if (operations.Count == 0) return null;

        if (parent != null && ReferenceEquals(parent.heroDeveloper, heroDeveloper))
        {
            parent.operations.AddRange(operations);
            return null;
        }

        return new HeroDeveloperBatch(heroDeveloper, operations);
    }

    public void Abort()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!active) return false;

        active = false;
        if (ReferenceEquals(current, this)) current = parent;
        return true;
    }
}
