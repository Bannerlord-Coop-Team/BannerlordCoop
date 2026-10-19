using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Entity;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts;

internal interface IArtisanProductsProgress
{
    void Send(Quest quest);
    void SendIssue(IssueBase issue, IssueBase.IssueUpdateDetails detail);
}

internal class ArtisanProductsProgress : IArtisanProductsProgress
{
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;

    public ArtisanProductsProgress(INetwork network, IObjectManager objects, IIssueOwnerResolver owners)
    {
        this.network = network;
        this.objects = objects;
        this.owners = owners;
    }

    public void Send(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (!(issue is Issue artisan) || !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId)) return;
        network.SendAll(new NetworkArtisanProductsProgress(ownerId, issue.StringId, quest._deliveredRawGoods,
            quest._counterOfferRefused, quest.JournalEntries.Select(log => log.LogText.ToString()).ToArray(), controllerId,
            artisan._targetSettlement.Town.Prosperity));
    }

    public void SendIssue(IssueBase issue, IssueBase.IssueUpdateDetails detail)
    {
        if (!objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId)) return;
        var prosperity = issue is Issue artisan ? artisan._targetSettlement.Town.Prosperity : 0;
        network.SendAll(new NetworkArtisanProductsProgress(ownerId, issue.StringId, 0, false,
            issue.JournalEntries.Select(log => log.LogText.ToString()).ToArray(), controllerId, prosperity, detail));
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
    [ProtoMember(6)] public readonly string ControllerId;
    [ProtoMember(7)] public readonly IssueBase.IssueUpdateDetails Detail;
    [ProtoMember(8)] public readonly float Prosperity;

    public NetworkArtisanProductsProgress(string ownerId, string issueId, int delivered, bool merchantRefused, string[] logs,
        string controllerId = null, float prosperity = 0, IssueBase.IssueUpdateDetails detail = IssueBase.IssueUpdateDetails.None)
    {
        OwnerId = ownerId;
        IssueId = issueId;
        Delivered = delivered;
        MerchantRefused = merchantRefused;
        Logs = logs;
        ControllerId = controllerId;
        Prosperity = prosperity;
        Detail = detail;
    }
}

internal class ArtisanProductsProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver resolver;
    private readonly IControllerIdProvider controller;

    public ArtisanProductsProgressHandler(IMessageBroker broker, IIssueResolver resolver, IControllerIdProvider controller)
    {
        this.broker = broker;
        this.resolver = resolver;
        this.controller = controller;
        broker.Subscribe<NetworkArtisanProductsProgress>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkArtisanProductsProgress>(Handle);

    private void Handle(MessagePayload<NetworkArtisanProductsProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (string.IsNullOrEmpty(data.ControllerId) || data.ControllerId != controller.ControllerId) return;
            if (!resolver.TryResolve(data.OwnerId, data.IssueId, out _, out var issue, out _)) return;
            using (new AllowedThread())
            {
                issue._targetSettlement.Town.Prosperity = data.Prosperity;
                if (!(issue.IssueQuest is Quest quest))
                {
                    foreach (var text in data.Logs.Skip(issue.JournalEntries.Count)) issue.AddLog(new JournalLog(CampaignTime.Now, new TextObject(text)));
                    CampaignEventDispatcher.Instance.OnIssueUpdated(issue, data.Detail, Hero.MainHero);
                    return;
                }
                quest._deliveredRawGoods = data.Delivered;
                quest._counterOfferRefused = data.MerchantRefused;
                if (quest._playerStartsQuestLog != null) quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, data.Delivered);
                foreach (var text in data.Logs.Skip(quest.JournalEntries.Count)) quest.AddLog(new TextObject(text));
            }
        }, context: nameof(ArtisanProductsProgressHandler));
    }
}
