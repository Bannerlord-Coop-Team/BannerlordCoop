using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace GameInterface.Services.Crime;

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestCrimeRatingChange : ICommand
{
    [ProtoMember(1)] public string FactionId { get; }
    [ProtoMember(2)] public float Delta { get; }
    [ProtoMember(3)] public bool ShowNotification { get; }

    public RequestCrimeRatingChange(string factionId, float delta, bool showNotification)
    {
        FactionId = factionId;
        Delta = delta;
        ShowNotification = showNotification;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCrimeRatingChanged : IEvent
{
    [ProtoMember(1)] public string ControllerId { get; }
    [ProtoMember(2)] public string HeroId { get; }
    [ProtoMember(3)] public string FactionId { get; }
    [ProtoMember(4)] public float Rating { get; }

    public NetworkCrimeRatingChanged(string controllerId, string heroId, string factionId, float rating)
    {
        ControllerId = controllerId;
        HeroId = heroId;
        FactionId = factionId;
        Rating = rating;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCrimeRatingNotification : IEvent
{
    [ProtoMember(1)] public string ControllerId { get; }
    [ProtoMember(2)] public string HeroId { get; }
    [ProtoMember(3)] public string FactionId { get; }
    [ProtoMember(4)] public float Rating { get; }
    [ProtoMember(5)] public float Delta { get; }

    public NetworkCrimeRatingNotification(string controllerId, string heroId, string factionId, float rating, float delta)
    {
        ControllerId = controllerId;
        HeroId = heroId;
        FactionId = factionId;
        Rating = rating;
        Delta = delta;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct RequestCrimePayment : ICommand
{
    [ProtoMember(1)] public string SettlementId { get; }
    [ProtoMember(2)] public CrimeModel.PaymentMethod Method { get; }

    [ProtoMember(3)] public string FactionId { get; }

    public RequestCrimePayment(string settlementId, string factionId, CrimeModel.PaymentMethod method)
    {
        SettlementId = settlementId;
        FactionId = factionId;
        Method = method;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCrimePaymentResult : IEvent
{
    [ProtoMember(1)] public string HeroId { get; }
    [ProtoMember(2)] public string SettlementId { get; }
    [ProtoMember(3)] public bool Accepted { get; }
    [ProtoMember(4)] public bool LeaveMenu { get; }

    public NetworkCrimePaymentResult(string heroId, string settlementId, bool accepted, bool leaveMenu)
    {
        HeroId = heroId;
        SettlementId = settlementId;
        Accepted = accepted;
        LeaveMenu = leaveMenu;
    }
}
