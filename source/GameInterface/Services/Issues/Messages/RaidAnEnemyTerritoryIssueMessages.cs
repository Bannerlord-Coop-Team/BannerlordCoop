using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.RaidAnEnemyTerritoryIssueCreationPatch"/>) immediately after
/// <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/> for real. Carries the live issue so
/// <see cref="Handlers.RaidAnEnemyTerritoryIssueHandler"/> can capture the rolled enemy kingdom and replicate it.
/// </summary>
public readonly struct RaidAnEnemyTerritoryIssueCreated : IEvent
{
    public readonly RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue Issue;

    public RaidAnEnemyTerritoryIssueCreated(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published on the server (from <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/>) after it
/// resolves a genuine, owner-attributable village raid completion against one of this issue type's active
/// quests, and has already applied the mutation to its OWN local mirror via
/// <see cref="Interfaces.IRaidAnEnemyTerritoryIssueInterface.TryApplyRaidedVillage"/>. Carries the quest giver
/// (the <see cref="VillageNeedsToolsIssueOwnership"/> dictionary key) and the raided settlement so
/// <see cref="Handlers.RaidAnEnemyTerritoryIssueHandler"/> can broadcast it to every OTHER peer.
/// </summary>
public readonly struct RaidAnEnemyTerritoryVillageRaided : IEvent
{
    public readonly Hero QuestGiver;
    public readonly Settlement Settlement;

    public RaidAnEnemyTerritoryVillageRaided(Hero questGiver, Settlement settlement)
    {
        QuestGiver = questGiver;
        Settlement = settlement;
    }
}

// --- Networked messages ---

/// <summary>Server -&gt; all clients: the authoritatively-picked enemy kingdom for a newly created Raid an Enemy
/// Territory issue, so every client constructs a byte-identical instance instead of independently re-rolling
/// <c>Kingdom.All.Where(k =&gt; k.IsAtWarWith(...)).GetRandomElementInefficiently()</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRaidAnEnemyTerritoryIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string EnemyKingdomId;

    public NetworkRaidAnEnemyTerritoryIssueCreated(string ownerId, string enemyKingdomId)
    {
        OwnerId = ownerId;
        EnemyKingdomId = enemyKingdomId;
    }
}

/// <summary>Server -&gt; all clients: a village the real quest owner genuinely finished raiding, so every peer's
/// own mirror quest (including the real owner's own, if they aren't the server) advances
/// <c>_raidedVillages</c>/<c>_raidedVillagesTrackLog</c> identically instead of only the server's own local
/// mirror knowing about it - see <see cref="Patches.RaidAnEnemyTerritoryRaidCompletionPatches"/>'s doc comment
/// for why this needs its own explicit sync (unlike a plain quest finalization, there is no generic mid-quest
/// progress-counter broadcast in this codebase to ride).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRaidAnEnemyTerritoryVillageRaided : ICommand
{
    [ProtoMember(1)]
    public readonly string QuestGiverId;
    [ProtoMember(2)]
    public readonly string SettlementId;

    public NetworkRaidAnEnemyTerritoryVillageRaided(string questGiverId, string settlementId)
    {
        QuestGiverId = questGiverId;
        SettlementId = settlementId;
    }
}
