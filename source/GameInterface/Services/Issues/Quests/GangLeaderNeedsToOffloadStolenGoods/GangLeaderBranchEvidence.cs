using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.GangLeaderNeedsToOffloadStolenGoods;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestGangLeaderBranchEvidence : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly byte Branch;

    public RequestGangLeaderBranchEvidence(string issueOwnerId, string issueId, byte branch)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Branch = branch;
    }
}

internal sealed class GangLeaderBranchEvidence : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly HashSet<string> evidence = new();

    public GangLeaderBranchEvidence(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IIssueOwnershipRegistry ownership)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.ownership = ownership;
        broker.Subscribe<RequestGangLeaderBranchEvidence>(Handle);
    }

    public void Dispose() => broker.Unsubscribe<RequestGangLeaderBranchEvidence>(Handle);

    internal void Record(QuestBase quest, byte branch)
    {
        if (ModInformation.IsServer || !objects.TryGetIdWithLogging(IssueManager.GetIssueOfQuest(quest)?.IssueOwner, out var ownerId)) return;
        var issue = IssueManager.GetIssueOfQuest(quest);
        network.SendAll(new RequestGangLeaderBranchEvidence(ownerId, issue.StringId, branch));
    }

    internal bool Consume(IssueBase issue, byte branch)
    {
        if (ModInformation.IsClient || !objects.TryGetId(issue.IssueOwner, out var ownerId)) return true;
        return evidence.Remove(Key(ownerId, issue.StringId, branch));
    }

    private void Handle(MessagePayload<RequestGangLeaderBranchEvidence> payload)
    {
        if (ModInformation.IsClient || !(payload.Who is NetPeer peer) || !players.TryGetPlayer(peer, out var player)) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObject<Hero>(data.IssueOwnerId, out var owner) || owner.Issue == null ||
                owner.Issue.StringId != data.IssueId || !owner.Issue.IsSolvingWithQuest ||
                !ownership.TryGetOwner(data.IssueOwnerId, data.IssueId, out var controller) || controller != player.ControllerId)
                return;
            evidence.Add(Key(data.IssueOwnerId, data.IssueId, data.Branch));
        }, context: nameof(GangLeaderBranchEvidence));
    }

    private static string Key(string ownerId, string issueId, byte branch) => $"{ownerId}\n{issueId}\n{branch}";
}
