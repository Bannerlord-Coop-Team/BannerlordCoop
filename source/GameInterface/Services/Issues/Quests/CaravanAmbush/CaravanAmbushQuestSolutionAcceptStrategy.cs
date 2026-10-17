using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest;

namespace GameInterface.Services.Issues.Quests.CaravanAmbush;

internal class CaravanAmbushQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private readonly IIssueOwnerResolver owners;
    private readonly ICaravanAmbushStatePublisher publisher;

    public CaravanAmbushQuestSolutionAcceptStrategy(IIssueOwnerResolver owners, ICaravanAmbushStatePublisher publisher)
    {
        this.owners = owners;
        this.publisher = publisher;
    }

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsClient || !(issue.IssueQuest is Quest quest)) return;
        if (!owners.TryResolveOwnerHero(issue, out var hero)) throw new InvalidOperationException("Caravan quest has no owner");
        using (new MainHeroSubstitutionScope(hero, hero.PartyBelongedTo))
        {
            // StartQuest is suppressed on the server, so vanilla spawns the parties without registering listeners.
            quest.OnQuestAccepted();
        }
        publisher.Publish(quest);
    }
}
