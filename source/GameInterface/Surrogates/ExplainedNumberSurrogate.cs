using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.ExplainedNumber.StatExplainer;


namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ExplainedNumberSurrogate
{
    [ProtoMember(1)]
    public float BaseNumber { get; set; }

    [ProtoMember(2)]
    public float LimitMinValue { get; set; }

    [ProtoMember(3)]
    public float LimitMaxValue { get; set; }

    [ProtoMember(4)]
    public float SumOfFactors { get; set; }

    [ProtoMember(5)]
    public ExplainedNumber.StatExplainer StatExplainer { get; set; }

    public ExplainedNumberSurrogate(ExplainedNumber explainedNumber)
    {
        BaseNumber = explainedNumber.BaseNumber;
        LimitMaxValue = explainedNumber.LimitMaxValue;
        LimitMinValue = explainedNumber.LimitMinValue;
        SumOfFactors = explainedNumber.SumOfFactors;
        StatExplainer = explainedNumber._explainer;

    }
    public static implicit operator ExplainedNumberSurrogate(ExplainedNumber explainedNumber)
    {
        return new ExplainedNumberSurrogate(explainedNumber);
    }

    public static implicit operator ExplainedNumber(ExplainedNumberSurrogate surrogate)
    {
        return new ExplainedNumber
        {
            BaseNumber = surrogate.BaseNumber,
            _limitMinValue = surrogate.LimitMinValue,
            _limitMaxValue = surrogate.LimitMaxValue,
            SumOfFactors = surrogate.SumOfFactors,
            _explainer = surrogate.StatExplainer
        };
    }
}

[ProtoContract]
public sealed class StatExplainerSurrogate
{
    [ProtoMember(1)]
    public List<ExplanationLine> Lines { get; set; } = new();

    [ProtoMember(2)]
    public ExplanationLine? BaseLine { get; set; }

    [ProtoMember(3)]
    public ExplanationLine? LimitMinLine { get; set; }

    [ProtoMember(4)]
    public ExplanationLine? LimitMaxLine { get; set; }

    public static implicit operator StatExplainerSurrogate(ExplainedNumber.StatExplainer value)
    {
        if (value is null)
            return null!;

        var surrogate = new StatExplainerSurrogate();

        foreach (var line in value.Lines)
        {
            surrogate.Lines.Add(line);
        }

        if (value.BaseLine.HasValue)
            surrogate.BaseLine = value.BaseLine.Value;

        if (value.LimitMinLine.HasValue)
            surrogate.LimitMinLine = value.LimitMinLine.Value;

        if (value.LimitMaxLine.HasValue)
            surrogate.LimitMaxLine = value.LimitMaxLine.Value;

        return surrogate;
    }

    public static implicit operator ExplainedNumber.StatExplainer(StatExplainerSurrogate surrogate)
    {
        if (surrogate is null)
            return null!;

        var value = new ExplainedNumber.StatExplainer
        {
            Lines = surrogate.Lines,
            BaseLine = surrogate.BaseLine,
            LimitMinLine = surrogate.LimitMinLine,
            LimitMaxLine = surrogate.LimitMaxLine
        };

        return value;
    }
}

[ProtoContract]
public sealed class ExplanationLineSurrogate
{
    [ProtoMember(1)]
    public string Name { get; set; } = string.Empty;

    [ProtoMember(2)]
    public float Number { get; set; }

    [ProtoMember(3)]
    public ExplainedNumber.StatExplainer.OperationType OperationType { get; set; }

    public static implicit operator ExplanationLineSurrogate(
        ExplainedNumber.StatExplainer.ExplanationLine value)
    {
        return new ExplanationLineSurrogate
        {
            Name = value.Name,
            Number = value.Number,
            OperationType = value.OperationType
        };
    }

    public static implicit operator ExplainedNumber.StatExplainer.ExplanationLine(
        ExplanationLineSurrogate surrogate)
    {
        return new ExplainedNumber.StatExplainer.ExplanationLine(
            surrogate.Name,
            surrogate.Number,
            surrogate.OperationType);
    }
}