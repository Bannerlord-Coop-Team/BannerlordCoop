using E2E.Tests.Environment.Instance;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace E2E.Tests.Services.Issues;

internal sealed class StubIssuePreconditions : IDisposable
{
    private static readonly object Gate = new();
    private static readonly Harmony harmony = new("test-stub-issue-preconditions");
    private static readonly Dictionary<Type, int> Users = new();
    private static readonly Dictionary<object, IssueBase.PreconditionFlags> RefusingContainers = new();
    private static readonly HashSet<object> StayAliveFailingContainers = new();
    private static readonly HashSet<object> ThrowingContainers = new();

    private readonly Type issueType;
    private readonly List<object> stubbed = new();

    public StubIssuePreconditions(Type issueType)
    {
        this.issueType = issueType;

        lock (Gate)
        {
            Users.TryGetValue(issueType, out var users);

            if (users == 0)
            {
                harmony.Patch(StayAliveTarget(issueType), prefix: new HarmonyMethod(typeof(StubIssuePreconditions), nameof(StayAlivePrefix)));
                harmony.Patch(CanTakeTarget(issueType), prefix: new HarmonyMethod(typeof(StubIssuePreconditions), nameof(CanTakePrefix)));
            }

            Users[issueType] = users + 1;
        }
    }

    public void Refuse(EnvironmentInstance instance, IssueBase.PreconditionFlags flag = IssueBase.PreconditionFlags.Relation)
    {
        lock (Gate)
        {
            RefusingContainers[instance.Container] = flag;
            stubbed.Add(instance.Container);
        }
    }

    public void FailStayAlive(EnvironmentInstance instance)
    {
        lock (Gate)
        {
            StayAliveFailingContainers.Add(instance.Container);
            stubbed.Add(instance.Container);
        }
    }

    public void Throw(EnvironmentInstance instance)
    {
        lock (Gate)
        {
            ThrowingContainers.Add(instance.Container);
            stubbed.Add(instance.Container);
        }
    }

    public void Dispose()
    {
        lock (Gate)
        {
            foreach (var container in stubbed)
            {
                RefusingContainers.Remove(container);
                StayAliveFailingContainers.Remove(container);
                ThrowingContainers.Remove(container);
            }

            Users[issueType]--;

            if (Users[issueType] == 0)
            {
                harmony.Unpatch(StayAliveTarget(issueType), HarmonyPatchType.Prefix, harmony.Id);
                harmony.Unpatch(CanTakeTarget(issueType), HarmonyPatchType.Prefix, harmony.Id);
            }
        }
    }

    private static MethodInfo StayAliveTarget(Type issueType) => AccessTools.Method(issueType, nameof(IssueBase.IssueStayAliveConditions));

    private static MethodInfo CanTakeTarget(Type issueType) => AccessTools.Method(issueType, nameof(IssueBase.CanPlayerTakeQuestConditions));

    private static object? CurrentContainer()
    {
        GameInterface.ContainerProvider.TryGetContainer(out var container);
        return container;
    }

    private static bool StayAlivePrefix(ref bool __result)
    {
        var container = CurrentContainer();

        lock (Gate)
        {
            __result = container == null || !StayAliveFailingContainers.Contains(container);
        }

        return false;
    }

    private static bool CanTakePrefix(
        Hero __0,
        out IssueBase.PreconditionFlags __1,
        out Hero? __2,
        out SkillObject? __3,
        out int __4,
        ref bool __result)
    {
        var container = CurrentContainer();
        var flag = IssueBase.PreconditionFlags.None;

        lock (Gate)
        {
            if (container != null && ThrowingContainers.Contains(container))
            {
                throw new InvalidOperationException("Stubbed precondition failure");
            }

            if (container != null)
            {
                RefusingContainers.TryGetValue(container, out flag);
            }
        }

        __1 = flag;
        __2 = (flag & IssueBase.PreconditionFlags.Relation) == IssueBase.PreconditionFlags.Relation ? __0 : null;
        __3 = null;
        __4 = 0;
        __result = flag == IssueBase.PreconditionFlags.None;
        return false;
    }
}
