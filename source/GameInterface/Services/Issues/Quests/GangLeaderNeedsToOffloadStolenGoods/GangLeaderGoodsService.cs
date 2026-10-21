using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
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
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Quest = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangLeaderGoodsPurchase : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly bool Fight;

    public RequestGangLeaderGoodsPurchase(string issueOwnerId, string issueId, string questId, bool fight = false)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        QuestId = questId;
        Fight = fight;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderGoodsAcquired : IEvent
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly bool Fight;
    [ProtoMember(5)] public readonly bool BattleWon;

    public NetworkGangLeaderGoodsAcquired(string issueOwnerId, string issueId, string questId, bool fight, bool battleWon = false)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        QuestId = questId;
        Fight = fight;
        BattleWon = battleWon;
    }
}

internal interface IGangLeaderGoodsService
{
    void Purchase(Quest quest);
    void Fight(Quest quest);
    void ObserveBattle(Quest quest, MapEvent mapEvent);
    void LeaveHideout(Quest quest, MobileParty party, Settlement settlement);
    void MerchantConversationEnded(IEnumerable<CharacterObject> characters);
    bool TryConsume(Quest quest, byte branch);
    void BattleEnded(Quest quest, MapEvent mapEvent);
}

internal sealed class GangLeaderGoodsService : IGangLeaderGoodsService, IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderGoodsService>();
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly HashSet<Quest> consumed = new();
    private readonly Dictionary<Quest, NetPeer> merchantConversations = new();
    private readonly Dictionary<MapEvent, Dictionary<Quest, BattleSideEnum>> participants = new();

    public GangLeaderGoodsService(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IIssueOwnershipRegistry ownership)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.ownership = ownership;
        broker.Subscribe<RequestGangLeaderGoodsPurchase>(HandlePurchase);
        broker.Subscribe<NetworkGangLeaderGoodsAcquired>(HandleAcquired);
        broker.Subscribe<RequestGangLeaderMerchantConversationEnded>(HandleConversationEnded);
        broker.Subscribe<PlayerDisconnected>(HandleDisconnected);
        broker.Subscribe<IssueOutcomeObserved>(HandleOutcome);
    }

    public void Dispose()
    {
        broker.Unsubscribe<RequestGangLeaderGoodsPurchase>(HandlePurchase);
        broker.Unsubscribe<NetworkGangLeaderGoodsAcquired>(HandleAcquired);
        broker.Unsubscribe<RequestGangLeaderMerchantConversationEnded>(HandleConversationEnded);
        broker.Unsubscribe<PlayerDisconnected>(HandleDisconnected);
        broker.Unsubscribe<IssueOutcomeObserved>(HandleOutcome);
    }

    public void Purchase(Quest quest) => Choose(quest, false);
    public void Fight(Quest quest) => Choose(quest, true);

    private void Choose(Quest quest, bool fight)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (ModInformation.IsClient && issue != null && objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId))
        {
            network.SendAll(new RequestGangLeaderGoodsPurchase(ownerId, issue.StringId, quest.StringId, fight));
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
                quest._playerHasTheGoods || quest._isFightingForGoods || (!data.Fight && hero.Gold < quest._stolenTradeGoodPrice))
            {
                Logger.Warning("Rejected stolen goods purchase for {issue}", data.IssueId);
                return;
            }

            using (new MainHeroSubstitutionScope(hero, party))
            {
                if (data.Fight)
                {
                    quest._isFightingForGoods = true;
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(hero, null, quest._stolenTradeGoodPrice);
                    quest._isPayingForGoods = true;
                    quest._playerHasTheGoods = true;
                }
                network.Send(peer, new NetworkGangLeaderGoodsAcquired(data.IssueOwnerId, data.IssueId, data.QuestId, data.Fight));
            }
        }, context: nameof(GangLeaderGoodsService));
    }

    private void HandleAcquired(MessagePayload<NetworkGangLeaderGoodsAcquired> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObject<Hero>(data.IssueOwnerId, out var giver) || giver.Issue == null ||
                giver.Issue.StringId != data.IssueId || !(giver.Issue.IssueQuest is Quest quest) ||
                quest.StringId != data.QuestId) return;
            quest._isFightingForGoods = data.Fight;
            quest._isPayingForGoods = !data.Fight;
            quest._playerHasTheGoods = !data.Fight || data.BattleWon;
            if (data.Fight && !data.BattleWon) quest.AddLog(quest.BeforeAttackHideoutQuestLogText);
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
        if (!success && branch != GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByKeepingGoods &&
            branch != GangLeaderNeedsToOffloadStolenGoodsBranch.BetrayalByGivingGoodsBack)
        {
            Logger.Warning("Rejected unknown stolen goods branch {branch} for {quest}", proof, quest.StringId);
            return false;
        }
        if (!quest._playerHasTheGoods || quest._isPayingForGoods != success ||
            !merchantConversations.Remove(quest) || !consumed.Add(quest))
        {
            Logger.Warning("Rejected stolen goods branch {branch} for {quest}", branch, quest.StringId);
            return false;
        }
        return true;
    }

    public void LeaveHideout(Quest quest, MobileParty party, Settlement settlement)
    {
        if (ModInformation.IsClient || party != MobileParty.MainParty || settlement != quest._questHideout ||
            !quest._playerHasTheGoods) return;
        if (players.TryGetPlayer(Hero.MainHero, out var player) && players.TryGetPeer(player.ControllerId, out var peer))
            merchantConversations[quest] = peer;
    }

    public void MerchantConversationEnded(IEnumerable<CharacterObject> characters)
    {
        if (ModInformation.IsServer) return;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue.IssueQuest is Quest quest && quest.IsOngoing && quest._playerHasTheGoods &&
                quest._counterOfferHero != null && characters.Contains(quest._counterOfferHero.CharacterObject) &&
                objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId))
                network.SendAll(new RequestGangLeaderMerchantConversationEnded(ownerId, issue.StringId, quest.StringId));
        }
    }

    private void HandleConversationEnded(MessagePayload<RequestGangLeaderMerchantConversationEnded> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (objects.TryGetObject<Hero>(data.IssueOwnerId, out var giver) &&
                giver.Issue != null && giver.Issue.StringId == data.IssueId &&
                giver.Issue.IssueQuest is Quest quest && quest.StringId == data.QuestId &&
                merchantConversations.TryGetValue(quest, out var ownerPeer) && ownerPeer == peer)
                merchantConversations.Remove(quest);
        }, context: nameof(GangLeaderGoodsService));
    }

    private void HandleDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        var peer = payload.What.PlayerId;
        GameThread.RunSafe(() => ClearPeer(peer), context: nameof(GangLeaderGoodsService));
    }

    private void ClearPeer(NetPeer peer)
    {
        foreach (var quest in merchantConversations.Where(p => p.Value == peer).Select(p => p.Key).ToArray())
            merchantConversations.Remove(quest);
    }

    private void HandleOutcome(MessagePayload<IssueOutcomeObserved> payload)
    {
        var owner = payload.What.Issue.IssueOwner;
        GameThread.RunSafe(() =>
        {
            consumed.RemoveWhere(q => q.QuestGiver == owner);
            foreach (var quest in merchantConversations.Keys.Where(q => q.QuestGiver == owner).ToArray()) merchantConversations.Remove(quest);
            foreach (var battle in participants.Keys.ToArray())
            {
                foreach (var quest in participants[battle].Keys.Where(q => q.QuestGiver == owner).ToArray()) participants[battle].Remove(quest);
                if (participants[battle].Count == 0) participants.Remove(battle);
            }
        }, context: nameof(GangLeaderGoodsService));
    }

    public void ObserveBattle(Quest quest, MapEvent mapEvent)
    {
        if (ModInformation.IsClient || !mapEvent.IsHideoutBattle) return;
        var ownerParty = Hero.MainHero.PartyBelongedTo;
        if (ownerParty == null) return;
        var attacker = mapEvent.AttackerSide.Parties.Any(p => p.Party.MobileParty == ownerParty);
        var defender = mapEvent.DefenderSide.Parties.Any(p => p.Party.MobileParty == ownerParty);
        if (!attacker && !defender) return;
        if (mapEvent.MapEventSettlement == quest._questHideout) quest._isFightingForGoods = true;
        if (!participants.TryGetValue(mapEvent, out var quests)) participants.Add(mapEvent, quests = new Dictionary<Quest, BattleSideEnum>());
        quests[quest] = attacker ? BattleSideEnum.Attacker : BattleSideEnum.Defender;
    }

    public void BattleEnded(Quest quest, MapEvent mapEvent)
    {
        if (ModInformation.IsClient || !participants.TryGetValue(mapEvent, out var quests) ||
            !quests.TryGetValue(quest, out var ownerSide)) return;
        quests.Remove(quest);
        if (quests.Count == 0) participants.Remove(mapEvent);
        if (mapEvent.WinningSide != ownerSide)
        {
            if (consumed.Add(quest)) quest.FailQuestByLosingHideoutBattle();
        }
        else if (mapEvent.MapEventSettlement == quest._questHideout)
        {
            quest._isFightingForGoods = true;
            quest._playerHasTheGoods = true;
            var issue = IssueManager.GetIssueOfQuest(quest);
            if (issue != null && objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) &&
                players.TryGetPlayer(Hero.MainHero, out var player) && players.TryGetPeer(player.ControllerId, out var peer))
                network.Send(peer, new NetworkGangLeaderGoodsAcquired(ownerId, issue.StringId, quest.StringId, true, true));
        }
    }
}
