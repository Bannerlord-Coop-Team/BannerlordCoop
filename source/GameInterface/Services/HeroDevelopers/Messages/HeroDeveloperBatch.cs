using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.HeroDevelopers.Messages;

/// <summary>
/// Identifies an operation captured from one <see cref="HeroDeveloper.AddSkillXp"/> call.
/// </summary>
internal enum HeroDeveloperOperationType
{
    RawXpGain,
    SkillXpSet,
    SkillLevelChange,
}

/// <summary>
/// Stores one ordered hero-developer operation before object references are converted to network ids.
/// </summary>
internal sealed class HeroDeveloperOperation
{
    public HeroDeveloperOperationType Type { get; }
    public SkillObject SkillObject { get; }
    public float Value { get; }
    public int ChangeAmount { get; }
    public bool ShouldNotify { get; }

    private HeroDeveloperOperation(
        HeroDeveloperOperationType type,
        SkillObject skillObject,
        float value,
        int changeAmount,
        bool shouldNotify)
    {
        Type = type;
        SkillObject = skillObject;
        Value = value;
        ChangeAmount = changeAmount;
        ShouldNotify = shouldNotify;
    }

    public static HeroDeveloperOperation RawXpGain(float rawXp, bool shouldNotify) =>
        new(HeroDeveloperOperationType.RawXpGain, null, rawXp, 0, shouldNotify);

    public static HeroDeveloperOperation SkillXpSet(SkillObject skillObject, float value) =>
        new(HeroDeveloperOperationType.SkillXpSet, skillObject, value, 0, false);

    public static HeroDeveloperOperation SkillLevelChange(
        SkillObject skillObject,
        int changeAmount,
        bool shouldNotify) =>
        new(HeroDeveloperOperationType.SkillLevelChange, skillObject, 0f, changeAmount, shouldNotify);
}

/// <summary>
/// Carries the ordered mutations produced by one local <see cref="HeroDeveloper.AddSkillXp"/> call.
/// </summary>
internal sealed class HeroDeveloperBatch : IEvent
{
    public HeroDeveloper HeroDeveloper { get; }
    public IReadOnlyList<HeroDeveloperOperation> Operations { get; }

    public HeroDeveloperBatch(
        HeroDeveloper heroDeveloper,
        IReadOnlyList<HeroDeveloperOperation> operations)
    {
        HeroDeveloper = heroDeveloper;
        Operations = operations;
    }
}
