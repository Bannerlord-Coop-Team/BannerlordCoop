using E2E.Tests.Environment.Instance;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using AllowedIssue = TaleWorlds.CampaignSystem.Issues.GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;

namespace E2E.Tests.Services.Issues;

internal sealed class StubIssueQuestGenerator : IDisposable
{
    private static readonly object Gate = new();
    private static readonly Harmony harmony = new("test-stub-issue-quest-generator");
    private static readonly MethodInfo Target = AccessTools.Method(typeof(AllowedIssue), nameof(AllowedIssue.GenerateIssueQuest));
    private static readonly List<(object? Container, QuestBase Quest)> Created = new();
    private static int users;

    public StubIssueQuestGenerator()
    {
        lock (Gate)
        {
            if (users++ == 0)
            {
                harmony.Patch(Target, prefix: new HarmonyMethod(typeof(StubIssueQuestGenerator), nameof(Prefix)));
            }
        }
    }

    public QuestBase[] CreatedFor(EnvironmentInstance instance)
    {
        lock (Gate)
        {
            return Created
                .Where(created => ReferenceEquals(created.Container, instance.Container))
                .Select(created => created.Quest)
                .ToArray();
        }
    }

    public void Dispose()
    {
        lock (Gate)
        {
            if (--users == 0)
            {
                harmony.Unpatch(Target, HarmonyPatchType.Prefix, harmony.Id);
            }
        }
    }

    private static bool Prefix(IssueBase __instance, string questId, ref QuestBase __result)
    {
        GameInterface.ContainerProvider.TryGetContainer(out var container);

        var quest = new UnstartedQuest(questId, __instance.IssueOwner);

        lock (Gate)
        {
            Created.Add((container, quest));
        }

        __result = quest;
        return false;
    }
}
