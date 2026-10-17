using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordsNeedsTutor;

internal sealed class TutorSuccessionHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly IOutcomeDispatcher dispatcher;

    public TutorSuccessionHandler(IMessageBroker broker, IOutcomeDispatcher dispatcher)
    {
        this.broker = broker;
        this.dispatcher = dispatcher;
        broker.Subscribe<PlayerHeirSelectionCompleted>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<PlayerHeirSelectionCompleted>(Handle);

    private void Handle(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() => dispatcher.RunAsOwners<Quest>(quest =>
        {
            if (Hero.MainHero == payload.What.PlayerHero)
                quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
        }), context: nameof(TutorSuccessionHandler));
    }
}
