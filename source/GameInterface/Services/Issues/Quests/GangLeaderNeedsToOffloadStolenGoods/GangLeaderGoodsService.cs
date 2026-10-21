using Common;
using Common.GameThreading;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using ProtoBuf;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangLeaderGoodsPurchase : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string QuestId;

    public RequestGangLeaderGoodsPurchase(string issueOwnerId, string issueId, string questId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        QuestId = questId;
    }
}

internal interface IGangLeaderGoodsService
{
    void Purchase(Quest quest);
    bool TryConsume(Quest quest, byte branch);
    void BattleEnded(MapEvent mapEvent);
}

internal sealed class GangLeaderGoodsService : IGangLeaderGoodsService, IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderGoodsService>();
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IOutcomeDispatcher dispatcher;
    private readonly HashSet<Quest> consumed = new();

    public GangLeaderGoodsService(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IIssueOwnershipRegistry ownership, IOutcomeDispatcher dispatcher)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.ownership = ownership;
        this.dispatcher = dispatcher;
        broker.Subscribe<RequestGangLeaderGoodsPurchase>(HandlePurchase);
    }

    public void Dispose() => broker.Unsubscribe<RequestGangLeaderGoodsPurchase>(HandlePurchase);

    public void Purchase(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (ModInformation.IsClient && issue != null && objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId))
        {
            network.SendAll(new RequestGangLeaderGoodsPurchase(ownerId, issue.StringId, quest.StringId));
        }
    }

    private void HandlePurchase(MessagePayload<RequestGangLeaderGoodsPurchase> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObject<Hero>(data.IssueOwnerId, out var giver) ||
                giver.Issue == null || giver.Issue.StringId != data.IssueId || !giver.Issue.IsSolvingWithQuest ||
                !(giver.Issue.IssueQuest is Quest quest) || !quest.IsOngoing || quest.StringId != data.QuestId ||
                !ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var owner) || owner != player.ControllerId ||
                !objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                !objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party) ||
                quest._questHideout == null || party.CurrentSettlement != quest._questHideout ||
                quest._playerHasTheGoods || quest._isFightingForGoods || hero.Gold < quest._stolenTradeGoodPrice)
            {
                Logger.Warning("Rejected stolen goods purchase for {issue}", data.IssueId);
                return;
            }

            using (new MainHeroSubstitutionScope(hero, party))
            {
                GiveGoldAction.ApplyBetweenCharacters(hero, null, quest._stolenTradeGoodPrice);
                quest._isPayingForGoods = true;
                quest._playerHasTheGoods = true;
            }
        }, context: nameof(GangLeaderGoodsService));
    }

    public bool TryConsume(Quest quest, byte proof)
    {
        if (ModInformation.IsClient) return true;
        var branch = (GangLeaderNeedsToOffloadStolenGoodsBranch)proof;
        if (branch == GangLeaderNeedsToOffloadStolenGoodsBranch.FailByLosingHideoutBattle)
        {
            // Battle losses are dispatched from the server's battle result, not a client branch request.
            return false;
        }

        var success = branch == GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByKeepingGoods ||
            branch == GangLeaderNeedsToOffloadStolenGoodsBranch.SuccessByGivingGoodsBack;
        if (!quest._playerHasTheGoods || quest._isPayingForGoods != success || !consumed.Add(quest))
        {
            Logger.Warning("Rejected stolen goods branch {branch} for {quest}", branch, quest.StringId);
            return false;
        }
        return true;
    }

    public void BattleEnded(MapEvent mapEvent)
    {
        if (ModInformation.IsClient || !mapEvent.IsHideoutBattle) return;
        dispatcher.RunAsOwners<Quest>(quest =>
        {
            var ownerParty = MobileParty.MainParty.Party;
            var attacker = mapEvent.AttackerSide.Parties.Any(p => p.Party == ownerParty);
            var defender = mapEvent.DefenderSide.Parties.Any(p => p.Party == ownerParty);
            if (!attacker && !defender) return;
            var ownerSide = attacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
            if (mapEvent.WinningSide != ownerSide)
            {
                if (consumed.Add(quest)) quest.FailQuestByLosingHideoutBattle();
            }
            else if (mapEvent.MapEventSettlement == quest._questHideout)
            {
                quest._isFightingForGoods = true;
                quest._playerHasTheGoods = true;
            }
        });
    }
}