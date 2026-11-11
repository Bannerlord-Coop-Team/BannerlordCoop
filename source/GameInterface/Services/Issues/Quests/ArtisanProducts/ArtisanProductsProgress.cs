using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal interface IArtisanProductsProgress
{
    void Send(Quest quest);
}

internal class ArtisanProductsProgress : IArtisanProductsProgress
{
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;

    public ArtisanProductsProgress(INetwork network, IObjectManager objects, IIssueOwnerResolver owners, IPlayerManager players)
    {
        this.network = network;
        this.objects = objects;
        this.owners = owners;
        this.players = players;
    }

    public void Send(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer)) return;
        network.Send(peer, new NetworkArtisanProductsProgress(giverId, issue.StringId, quest._deliveredRawGoods,
            quest._counterOfferRefused, controllerId));
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductsProgress : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly int Delivered;
    [ProtoMember(4)] public readonly bool MerchantRefused;
    [ProtoMember(5)] public readonly string ControllerId;

    public NetworkArtisanProductsProgress(string giverId, string issueId, int delivered, bool merchantRefused, string controllerId)
    {
        GiverId = giverId;
        IssueId = issueId;
        Delivered = delivered;
        MerchantRefused = merchantRefused;
        ControllerId = controllerId;
    }
}

internal class ArtisanProductsProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver resolver;
    private readonly IControllerIdProvider controller;
    private readonly IIssueConversationGuard conversationGuard;

    public ArtisanProductsProgressHandler(IMessageBroker broker, IIssueResolver resolver, IControllerIdProvider controller,
        IIssueConversationGuard conversationGuard)
    {
        this.broker = broker;
        this.resolver = resolver;
        this.controller = controller;
        this.conversationGuard = conversationGuard;
        broker.Subscribe<NetworkArtisanProductsProgress>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkArtisanProductsProgress>(Handle);

    private void Handle(MessagePayload<NetworkArtisanProductsProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() => conversationGuard.RunInOrder(() =>
        {
            if (data.ControllerId != controller.ControllerId ||
                !resolver.TryResolve(data.GiverId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;
            using (new AllowedThread())
            using (new QuestJournalReplay())
            {
                quest._deliveredRawGoods = data.Delivered;
                quest._counterOfferRefused = data.MerchantRefused;
                if (quest._playerStartsQuestLog != null) quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, data.Delivered);
            }
        }, data.GiverId), context: nameof(ArtisanProductsProgressHandler));
    }
}
