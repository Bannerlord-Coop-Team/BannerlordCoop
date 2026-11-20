using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal interface IExtortionByDesertersProgress
{
    void Send(Quest quest, TextObject log = null,
        ExtortionByDesertersNotice notice = ExtortionByDesertersNotice.None);
}

internal sealed class ExtortionByDesertersProgress : IExtortionByDesertersProgress
{
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;
    private readonly IPlayerManager players;
    private readonly INetwork network;

    public ExtortionByDesertersProgress(IObjectManager objects, IIssueOwnerResolver owners,
        IPlayerManager players, INetwork network)
    {
        this.objects = objects;
        this.owners = owners;
        this.players = players;
        this.network = network;
    }

    public void Send(Quest quest, TextObject log = null,
        ExtortionByDesertersNotice notice = ExtortionByDesertersNotice.None)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId) ||
            !players.TryGetPeer(controllerId, out var peer))
        {
            return;
        }

        var deserterId = string.Empty;
        var defenderId = string.Empty;
        if (quest._deserterMobileParty != null && !objects.TryGetIdWithLogging(quest._deserterMobileParty, out deserterId)) return;
        if (quest._defenderMobileParty != null && !objects.TryGetIdWithLogging(quest._defenderMobileParty, out defenderId)) return;

        network.Send(peer, new NetworkExtortionByDesertersState(ownerId, issue.StringId, controllerId,
            deserterId, defenderId, (int)quest._currentState, log, (byte)notice));
    }
}
