using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

public interface IRaceArbitratedAcceptMirrorStrategy<TFields>
{
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out TFields fields);

    void MirrorQuestAccepted(Hero owner, TFields fields);

    void RejectAcceptance(Hero owner);
}

public sealed class RaceArbitratedAcceptMirrorHandler<TFields>
{
    private readonly IRaceArbitratedAcceptMirrorStrategy<TFields> _strategy;

    public RaceArbitratedAcceptMirrorHandler(IRaceArbitratedAcceptMirrorStrategy<TFields> strategy)
    {
        _strategy = strategy;
    }

    public bool TryArbitrate(Hero owner, System.Func<Hero, bool> canAccept, out TFields fields)
    {
        fields = default;
        if (owner == null || canAccept == null || !canAccept(owner)) return false;

        _strategy.ReplayQuestAccepted(owner);
        return _strategy.TryCaptureQuestFields(owner, out fields);
    }

    public bool TryCaptureQuestFields(Hero owner, out TFields fields) => _strategy.TryCaptureQuestFields(owner, out fields);

    public void Mirror(Hero owner, TFields fields) => _strategy.MirrorQuestAccepted(owner, fields);

    public void Reject(Hero owner) => _strategy.RejectAcceptance(owner);
}
