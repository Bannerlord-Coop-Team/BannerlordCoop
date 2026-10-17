using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
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

    public ArtisanProductsProgress(INetwork network, IObjectManager objects)
    {
        this.network = network;
        this.objects = objects;
    }

    public void Send(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;
        network.SendAll(new NetworkArtisanProductsProgress(ownerId, issue.StringId, quest._deliveredRawGoods,
            quest._counterOfferRefused, quest.JournalEntries.Select(log => log.LogText.ToString()).ToArray()));
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanProductsProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly int Delivered;
    [ProtoMember(4)] public readonly bool MerchantRefused;
    [ProtoMember(5)] public readonly string[] Logs;

    public NetworkArtisanProductsProgress(string ownerId, string issueId, int delivered, bool merchantRefused, string[] logs)
    {
        OwnerId = ownerId;
        IssueId = issueId;
        Delivered = delivered;
        MerchantRefused = merchantRefused;
        Logs = logs;
    }
}

internal class ArtisanProductsProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver resolver;

    public ArtisanProductsProgressHandler(IMessageBroker broker, IIssueResolver resolver)
    {
        this.broker = broker;
        this.resolver = resolver;
        broker.Subscribe<NetworkArtisanProductsProgress>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkArtisanProductsProgress>(Handle);

    private void Handle(MessagePayload<NetworkArtisanProductsProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.OwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;
            quest._deliveredRawGoods = data.Delivered;
            quest._counterOfferRefused = data.MerchantRefused;
            if (quest._playerStartsQuestLog != null) quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, data.Delivered);
            foreach (var text in data.Logs.Skip(quest.JournalEntries.Count)) quest.AddLog(new TextObject(text));
        }, context: nameof(ArtisanProductsProgressHandler));
    }
}
