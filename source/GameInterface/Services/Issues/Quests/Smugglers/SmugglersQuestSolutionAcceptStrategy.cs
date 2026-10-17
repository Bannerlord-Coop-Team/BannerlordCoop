using Common;
using Common.Logging;
using Common.Network;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.SmugglersIssueBehavior.SmugglersIssueQuest;

namespace GameInterface.Services.Issues.Quests.Smugglers;

/// <summary>
/// The accept rolls nothing another machine needs, but it spawns the smugglers' party. That party is a
/// world object, so the server spawns it for the owning player and tells the owner's quest which party it is.
/// </summary>
internal class SmugglersQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<SmugglersQuestSolutionAcceptStrategy>();

    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IIssueOwnerResolver ownerResolver;

    public SmugglersQuestSolutionAcceptStrategy(INetwork network, IObjectManager objectManager, IIssueOwnerResolver ownerResolver)
    {
        this.network = network;
        this.objectManager = objectManager;
        this.ownerResolver = ownerResolver;
    }

    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsClient || !(issue.IssueQuest is Quest quest))
        {
            return;
        }

        if (!ownerResolver.TryResolveOwnerHero(issue, out var ownerHero))
        {
            Logger.Error("{issue} was accepted without an owning player, its smugglers are not spawned", issue.StringId);
            return;
        }

        // Vanilla sizes the party and its speed from the accepting player's party
        using (new MainHeroSubstitutionScope(ownerHero, ownerHero.PartyBelongedTo))
        {
            quest._smugglerParty = quest.CreateSmugglerParty();
        }

        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var issueOwnerId) ||
            !objectManager.TryGetIdWithLogging(quest._smugglerParty, out var partyId))
        {
            return;
        }

        network.SendAll(new NetworkSmugglersPartyCreated(issueOwnerId, issue.StringId, partyId));
    }
}
