using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ArtisanProductQuestActionRequested : IEvent
{
    public readonly Hero Giver;
    public readonly ArtisanProductQuestAction Action;
    public readonly int ExpectedDelivered;

    public ArtisanProductQuestActionRequested(Hero giver, ArtisanProductQuestAction action, int expectedDelivered)
    {
        Giver = giver;
        Action = action;
        ExpectedDelivered = expectedDelivered;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestArtisanProductQuestAction : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly ArtisanProductQuestAction Action;
    [ProtoMember(4)] public readonly int ExpectedDelivered;

    public RequestArtisanProductQuestAction(string giverId, int generation, ArtisanProductQuestAction action, int expectedDelivered)
    {
        GiverId = giverId;
        Generation = generation;
        Action = action;
        ExpectedDelivered = expectedDelivered;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductActionRejected : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly bool LordStart;

    public NetworkArtisanProductActionRejected(string giverId, int generation, bool lordStart)
    {
        GiverId = giverId;
        Generation = generation;
        LordStart = lordStart;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductQuestProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly int Delivered;
    [ProtoMember(4)] public readonly bool MerchantRefused;
    [ProtoMember(5)] public readonly bool MerchantOfferGiven;

    public NetworkArtisanProductQuestProgress(string giverId, int generation, int delivered, bool merchantRefused,
        bool merchantOfferGiven)
    {
        GiverId = giverId;
        Generation = generation;
        Delivered = delivered;
        MerchantRefused = merchantRefused;
        MerchantOfferGiven = merchantOfferGiven;
    }
}
