using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods;

internal class ArtisanDeliveryProgressHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IIssueResolver resolver;

    public ArtisanDeliveryProgressHandler(IMessageBroker broker, IIssueResolver resolver)
    {
        this.broker = broker;
        this.resolver = resolver;
        broker.Subscribe<NetworkArtisanDeliveryProgress>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<NetworkArtisanDeliveryProgress>(Handle);

    private void Handle(MessagePayload<NetworkArtisanDeliveryProgress> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!resolver.TryResolve(data.IssueOwnerId, data.IssueId, out _, out var issue, out _) ||
                !(issue.IssueQuest is Quest quest)) return;

            using var replay = new AllowedThread();
            using var journalReplay = new QuestJournalReplay();
            quest._givenTradeGoods = data.Delivered;
            if (quest._playerStartsQuestLog != null)
            {
                quest.UpdateQuestTaskStage(quest._playerStartsQuestLog, data.Delivered);
            }
        }, context: nameof(ArtisanDeliveryProgressHandler));
    }
}
