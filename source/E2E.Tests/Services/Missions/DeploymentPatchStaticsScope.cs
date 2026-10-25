using System.Collections;
using System.Linq;
using System.Reflection;
using GameInterface.Services.MapEvents;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Zeroes the deployment patch's static observation state for one test and puts the previous values back on dispose.
/// </summary>
internal sealed class DeploymentPatchStaticsScope : IDisposable
{
    private readonly List<(FieldInfo Field, object? Value)> previousFields = new();

    // Snapshots then clears every resettable static; readonly lists are cleared in place, readonly scalars are skipped.
    public DeploymentPatchStaticsScope()
    {
        foreach (var field in PatchType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                     .Where(field => !field.IsInitOnly || typeof(IList).IsAssignableFrom(field.FieldType)))
        {
            var value = field.GetValue(null);
            if (value is IList list)
            {
                previousFields.Add((field, list.Cast<object>().ToArray()));
                list.Clear();
            }
            else
            {
                previousFields.Add((field, value));
                field.SetValue(null, field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null);
            }
        }
    }

    /// <summary>The production patch class, which is internal to GameInterface.</summary>
    public static Type PatchType => typeof(CoopBattleMissionRegistry).Assembly
        .GetType("GameInterface.Services.MapEvents.Patches.CoopEmptyTeamDeploymentPatch", throwOnError: true)!;

    // Restores each snapshotted static, refilling lists in place.
    public void Dispose()
    {
        foreach (var entry in previousFields)
        {
            if (entry.Field.GetValue(null) is IList list)
            {
                list.Clear();
                foreach (var value in (object[])entry.Value!) list.Add(value);
            }
            else
            {
                entry.Field.SetValue(null, entry.Value);
            }
        }
    }
}
