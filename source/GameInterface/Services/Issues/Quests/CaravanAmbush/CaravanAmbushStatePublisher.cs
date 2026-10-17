using Common.Network;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal interface ICaravanAmbushStatePublisher
{
    void Publish(Quest quest);
}

internal class CaravanAmbushStatePublisher : ICaravanAmbushStatePublisher
{
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueOwnerResolver owners;

    public CaravanAmbushStatePublisher(INetwork network, IObjectManager objects, IIssueOwnerResolver owners)
    {
        this.network = network;
        this.objects = objects;
        this.owners = owners;
    }

    public void Publish(Quest quest)
    {
        var issue = IssueManager.GetIssueOfQuest(quest);
        if (issue == null || !objects.TryGetIdWithLogging(issue.IssueOwner, out var ownerId) ||
            !owners.TryGetOwnerControllerId(issue, out var controllerId)) return;
        var caravanId = IdOf(quest._caravanParty);
        var banditId = IdOf(quest._banditParty);
        network.SendAll(new NetworkCaravanAmbushState(ownerId, issue.StringId, controllerId, caravanId,
            banditId, quest._isCaravanSaved, quest._vicinityCheckFailCounter,
            quest.JournalEntries.Select(entry => entry.LogText.ToString()).ToArray()));
    }

    private string IdOf(object obj)
    {
        if (obj == null) return string.Empty;
        objects.TryGetIdWithLogging(obj, out var id);
        return id ?? string.Empty;
    }
}
