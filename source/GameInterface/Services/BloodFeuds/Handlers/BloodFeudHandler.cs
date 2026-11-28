using Common;
using Common.Messaging;
using GameInterface.Services.BloodFeuds.Interfaces;
using GameInterface.Services.BloodFeuds.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Helpers;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.BloodFeuds.Handlers;

/// <summary>
/// Mirrors the server's per player clan blood feuds into this client's clans and shows the vanilla <see cref="TaleWorlds.CampaignSystem.CampaignBehaviors.ExecutionCampaignBehavior"/> notices
/// </summary>
internal class BloodFeudHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IExecutionCampaignBehaviorInterface executionInterface;

    public BloodFeudHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IExecutionCampaignBehaviorInterface executionInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.executionInterface = executionInterface;

        messageBroker.Subscribe<NetworkUpdateBloodFeuds>(Handle_NetworkUpdateBloodFeuds);
        messageBroker.Subscribe<NetworkNotifyBloodFeudStateChanged>(Handle_NetworkNotifyBloodFeudStateChanged);
        messageBroker.Subscribe<NetworkNotifyBloodFeudClanMemberExecuted>(Handle_NetworkNotifyBloodFeudClanMemberExecuted);
        messageBroker.Subscribe<NetworkNotifyBloodFeudLordExecuted>(Handle_NetworkNotifyBloodFeudLordExecuted);
        messageBroker.Subscribe<NetworkNotifyBloodFeudClanMemberCaptured>(Handle_NetworkNotifyBloodFeudClanMemberCaptured);
        messageBroker.Subscribe<NetworkNotifyBloodFeudSettlementExecutionStarted>(Handle_NetworkNotifyBloodFeudSettlementExecutionStarted);
        messageBroker.Subscribe<NetworkNotifyBloodFeudSettlementExecutionCancelled>(Handle_NetworkNotifyBloodFeudSettlementExecutionCancelled);
        messageBroker.Subscribe<NetworkSettleBloodFeudByRansomPayment>(Handle_NetworkSettleBloodFeudByRansomPayment);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkUpdateBloodFeuds>(Handle_NetworkUpdateBloodFeuds);
        messageBroker.Unsubscribe<NetworkNotifyBloodFeudStateChanged>(Handle_NetworkNotifyBloodFeudStateChanged);
        messageBroker.Unsubscribe<NetworkNotifyBloodFeudClanMemberExecuted>(Handle_NetworkNotifyBloodFeudClanMemberExecuted);
        messageBroker.Unsubscribe<NetworkNotifyBloodFeudLordExecuted>(Handle_NetworkNotifyBloodFeudLordExecuted);
        messageBroker.Unsubscribe<NetworkNotifyBloodFeudClanMemberCaptured>(Handle_NetworkNotifyBloodFeudClanMemberCaptured);
        messageBroker.Unsubscribe<NetworkNotifyBloodFeudSettlementExecutionStarted>(Handle_NetworkNotifyBloodFeudSettlementExecutionStarted);
        messageBroker.Unsubscribe<NetworkNotifyBloodFeudSettlementExecutionCancelled>(Handle_NetworkNotifyBloodFeudSettlementExecutionCancelled);
        messageBroker.Unsubscribe<NetworkSettleBloodFeudByRansomPayment>(Handle_NetworkSettleBloodFeudByRansomPayment);
    }

    private void Handle_NetworkUpdateBloodFeuds(MessagePayload<NetworkUpdateBloodFeuds> payload)
    {
        var feuds = payload.What.Feuds;

        GameThread.RunSafe(() =>
        {
            executionInterface.ApplyBloodFeuds(feuds);
        });
    }

    private void Handle_NetworkNotifyBloodFeudStateChanged(MessagePayload<NetworkNotifyBloodFeudStateChanged> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clanWithFeud)) return;
            Hero executedHero = null;
            if (data.ExecutedHeroId != null) objectManager.TryGetObjectWithLogging(data.ExecutedHeroId, out executedHero);

            if (data.Detail == ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero ||
                data.Detail == ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.StartedByAIExecutePlayerRelative)
            {
                TextObject textObject;
                if (data.Detail == ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.StartedByPlayerExecuteAHero)
                {
                    textObject = new TextObject("{=3RvhG1HN}You have started a blood feud with the {CLAN}.");
                }
                else
                {
                    textObject = new TextObject("{=99k8PGvq}The {CLAN} have started a blood feud with your clan, the {PLAYER_CLAN}.");
                    textObject.SetTextVariable("PLAYER_CLAN", Clan.PlayerClan.Name);
                }
                textObject.SetTextVariable("CLAN", clanWithFeud.Name);
                Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudStartedMapNotification(textObject));

                if (data.PenalizedClanCount > 0)
                {
                    TextObject relationText = new TextObject("{=oqO9kjeW}The execution has hurt your relations with {COUNT} {?IS_PLURAL}clans{?}clan{\\?}.");
                    MBTextManager.SetTextVariable("IS_PLURAL", (data.PenalizedClanCount > 1) ? 1 : 0);
                    relationText.SetTextVariable("COUNT", data.PenalizedClanCount);
                    MBInformationManager.AddQuickInformation(relationText);
                }
            }
            else
            {
                TextObject textObject = new TextObject("{=Px3DDMvV}You have decided not to pursue your blood feud with the {CLAN}, and it has ended.");
                switch (data.Detail)
                {
                    case ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.SettledByRansomPayment when data.SettledByAgreement:
                        textObject = new TextObject("{=str_coop_blood_feud_ended_by_agreement}Your blood feud with the {CLAN} has ended by agreement.");
                        break;
                    case ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.SettledByRansomPayment:
                        textObject = new TextObject("{=fsfeamMz}Your blood feud with the {CLAN} has ended, as you paid them money to settle it.");
                        break;
                    case ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.SettledByRelationIncrease:
                        textObject = new TextObject("{=iNPcIqUj}Your blood feud with the {CLAN} has ended after relations between your clans improved.");
                        break;
                }
                textObject.SetTextVariable("CLAN", clanWithFeud.Name);
                Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudEndedMapNotification(textObject));
            }

            // Refreshes the feud icons, the server already applied the feud
            CampaignEventDispatcher.Instance.OnBloodFeudStateChanged(clanWithFeud, executedHero, data.Detail);
        });
    }

    private void Handle_NetworkNotifyBloodFeudClanMemberExecuted(MessagePayload<NetworkNotifyBloodFeudClanMemberExecuted> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ExecutedHeroId, out var executedHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ExecutorHeroId, out var executor)) return;

            TextObject textObject = new TextObject("{=xwMNKupG}The {EXECUTOR_CLAN} pursues their feud against you by executing your {RELATION} {CLAN_MEMBER.NAME}.");
            StringHelpers.SetCharacterProperties("CLAN_MEMBER", executedHero.CharacterObject, textObject);
            textObject.SetTextVariable("EXECUTOR_CLAN", executor.Clan.Name);
            textObject.SetTextVariable("RELATION", ConversationHelper.GetHeroRelationToHeroTextShort(executedHero, Hero.MainHero, uppercaseFirst: false));
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudClanMemberGotExecutedMapNotification(executedHero, executor, CampaignTime.Now, textObject));
        });
    }

    private void Handle_NetworkNotifyBloodFeudLordExecuted(MessagePayload<NetworkNotifyBloodFeudLordExecuted> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ExecutedHeroId, out var heroToExecute)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ClanMemberId, out var clanMember)) return;

            TextObject textObject = new TextObject("{=9OTgBLHx}{CLAN_MEMBER} ordered the execution of {LORD}, continuing the blood feud between the {PLAYER_CLAN} and the {OTHER_CLAN}.");
            textObject.SetTextVariable("LORD", heroToExecute.Name);
            textObject.SetTextVariable("CLAN_MEMBER", clanMember.Name);
            textObject.SetTextVariable("PLAYER_CLAN", Clan.PlayerClan.Name);
            textObject.SetTextVariable("OTHER_CLAN", heroToExecute.Clan.Name);
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudClanMemberExecutedLordMapNotification(heroToExecute, clanMember, CampaignTime.Now, textObject));
        });
    }

    private void Handle_NetworkNotifyBloodFeudClanMemberCaptured(MessagePayload<NetworkNotifyBloodFeudClanMemberCaptured> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ClanMemberId, out var clanMember)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.FeudedClanId, out var feudedClan)) return;

            TextObject textObject = new TextObject("{=tOGIagn0}The {FEUDED_CLAN} has captured your {RELATION} {CLAN_MEMBER.NAME}. Unless {?CLAN_MEMBER.GENDER}she{?}he{\\?} is freed, they are likely to execute {?CLAN_MEMBER.GENDER}her{?}him{\\?} within a few days.");
            StringHelpers.SetCharacterProperties("CLAN_MEMBER", clanMember.CharacterObject, textObject);
            textObject.SetTextVariable("FEUDED_CLAN", feudedClan.Name);
            textObject.SetTextVariable("RELATION", ConversationHelper.GetHeroRelationToHeroTextShort(clanMember, Hero.MainHero, uppercaseFirst: false));
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudClanMemberCapturedMapNotification(clanMember, textObject));
        });
    }

    private void Handle_NetworkNotifyBloodFeudSettlementExecutionStarted(MessagePayload<NetworkNotifyBloodFeudSettlementExecutionStarted> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ClanMemberId, out var clanMember)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.FeudedClanId, out var clanWithFeud)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;

            executionInterface.ShowSettlementExecutionNotice(clanMember, clanWithFeud, settlement, new CampaignTime(data.ExecutionDateTicks));
        });
    }

    private void Handle_NetworkNotifyBloodFeudSettlementExecutionCancelled(MessagePayload<NetworkNotifyBloodFeudSettlementExecutionCancelled> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.ClanMemberId, out var clanMember)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.FeudedClanId, out var feudedClan)) return;

            // Vanilla leaves the pending notice counting down when the execution is cancelled while the hero stays imprisoned
            var pendingNotice = MapScreen.Instance?.GetMapView<GauntletMapNotificationView>()?._dataSource?.NotificationItems
                .FirstOrDefault(item => item.Data is BloodFeudClanMemberCapturedMapNotification notice && notice.CaptiveHero == clanMember && notice.Settlement != null);
            pendingNotice?.ExecuteRemove();

            TextObject textObject = new TextObject("{=*}Your {RELATION} {CLAN_MEMBER.NAME} is no longer at risk of being executed as part of your blood feud with {FEUDED_CLAN}.");
            StringHelpers.SetCharacterProperties("CLAN_MEMBER", clanMember.CharacterObject, textObject);
            textObject.SetTextVariable("FEUDED_CLAN", feudedClan.Name);
            textObject.SetTextVariable("RELATION", ConversationHelper.GetHeroRelationToHeroTextShort(clanMember, Hero.MainHero, uppercaseFirst: false));
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new BloodFeudClanMemberExecuteCancelledMapNotification(clanMember, textObject));
        });
    }

    private void Handle_NetworkSettleBloodFeudByRansomPayment(MessagePayload<NetworkSettleBloodFeudByRansomPayment> payload)
    {
        var data = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!playerManager.TryGetSenderHero(payload.Who, out var playerHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            executionInterface.SettleBloodFeudByRansomPayment(playerHero, clan);
        });
    }
}
