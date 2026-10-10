using E2E.Tests.Environment.Instance;
using HarmonyLib;
using System.Reflection;

namespace E2E.Tests.Util;

internal interface IMethodCallRecorder : IDisposable
{
    int Count { get; }
    int CountFor(EnvironmentInstance instance);
    string[] MenusFor(EnvironmentInstance instance);
    void Clear();
}

internal sealed class MethodCallRecorder : IMethodCallRecorder
{
    private static readonly Dictionary<MethodBase, MethodCallRecorder> ActiveRecorders = new();
    private readonly Harmony harmony = new($"test-call-recorder-{Guid.NewGuid()}");
    private readonly List<MethodInfo> methods = new();
    private readonly List<(object? Container, string? MenuId)> calls = new();

    public MethodCallRecorder(params MethodInfo[] targets) : this(Priority.Normal, targets) { }

    public MethodCallRecorder(int priority, params MethodInfo[] targets)
    {
        try
        {
            foreach (var method in targets)
            {
                ActiveRecorders.Add(method, this);
                methods.Add(method);
                var prefix = method.GetParameters().Any(parameter => parameter.Name == "menuId")
                    ? nameof(RecordMenu) : nameof(RecordCall);
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(MethodCallRecorder), prefix)
                {
                    priority = priority,
                });
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public int Count => calls.Count;

    public int CountFor(EnvironmentInstance instance) =>
        calls.Count(call => ReferenceEquals(call.Container, instance.Container));

    public string[] MenusFor(EnvironmentInstance instance) =>
        calls.Where(call => ReferenceEquals(call.Container, instance.Container) && call.MenuId != null)
            .Select(call => call.MenuId!)
            .ToArray();

    public void Clear() => calls.Clear();

    public void Dispose()
    {
        foreach (var method in methods)
        {
            harmony.Unpatch(method, HarmonyPatchType.Prefix, harmony.Id);
            ActiveRecorders.Remove(method);
        }
        methods.Clear();
        calls.Clear();
    }

    private static bool RecordCall(MethodBase __originalMethod) => Record(__originalMethod, null);

    private static bool RecordMenu(MethodBase __originalMethod, string menuId) => Record(__originalMethod, menuId);

    private static bool Record(MethodBase method, string? menuId)
    {
        // The container is restored across network calls; ambient game statics are not.
        GameInterface.ContainerProvider.TryGetContainer(out var container);
        ActiveRecorders[method].calls.Add((container, menuId));
        return false;
    }
}
