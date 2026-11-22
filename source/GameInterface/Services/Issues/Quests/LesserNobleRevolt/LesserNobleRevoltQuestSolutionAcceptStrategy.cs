using Common;
using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

internal sealed class LesserNobleRevoltQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IIssueOwnerResolver ownerResolver;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager playerManager;

    public LesserNobleRevoltQuestSolutionAcceptStrategy(
        IObjectManager objectManager,
        INetwork network,
        IIssueOwnerResolver ownerResolver,
        IIssueOwnershipRegistry ownership,
        IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.ownerResolver = ownerResolver;
        this.ownership = ownership;
        this.playerManager = playerManager;
    }

    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(quest.QuestDueTime.NumTicks);
        return stream.ToArray();
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured == null || captured.Length == 0)
        {
            return;
        }

        using var stream = new MemoryStream(captured);
        using var reader = new BinaryReader(stream);
        quest.QuestDueTime = new CampaignTime(reader.ReadInt64());

        if (ModInformation.IsClient || quest._lesserNobleParty == null ||
            !ownerResolver.TryResolveOwnerHero(issue, out _) ||
            !objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId) ||
            !objectManager.TryGetIdWithLogging(quest._lesserNobleParty, out var partyId) ||
            !ownership.TryGetOwner(issueOwnerId, issue.StringId, out var controllerId) ||
            !playerManager.TryGetPeer(controllerId, out var peer))
        {
            return;
        }

        network.Send(peer, new NetworkLesserNobleRevoltPartyCreated(issueOwnerId, issue.StringId, controllerId, partyId));
    }
}
