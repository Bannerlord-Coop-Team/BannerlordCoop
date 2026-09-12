using System;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Installs an identity-only <see cref="Mission"/> as <c>Mission.Current</c> for the duration of a
/// test, so mission-scoped message handlers (which early-out on a null mission) run their real
/// bodies. The mission is constructor-skipped (no native engine calls) and carries an empty
/// <c>MissionObjects</c> list, so cache refreshes iterate nothing.
/// <para>
/// <c>Mission.Current</c> is process-global: every test class using this scope must share the
/// <c>[Collection("Mission.Current")]</c> collection so they serialize against each other.
/// </para>
/// </summary>
internal sealed class MissionCurrentScope : IDisposable
{
    private static readonly FieldInfo CurrentField = typeof(Mission)
        .GetField("_current", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Mission._current not found");

    private static readonly FieldInfo MissionObjectsField = typeof(Mission)
        .GetField("_missionObjects", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Mission._missionObjects not found");

    private static readonly FieldInfo TeamAITypeField = typeof(Mission)
        .GetField("<MissionTeamAIType>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Mission.MissionTeamAIType backing field not found");

    public Mission Instance { get; }

    public MissionCurrentScope()
    {
#pragma warning disable SYSLIB0050 // Identity-only test double; no native Mission constructor is invoked.
        Instance = (Mission)FormatterServices.GetUninitializedObject(typeof(Mission));
#pragma warning restore SYSLIB0050
        MissionObjectsField.SetValue(Instance, new MBList<MissionObject>());
        CurrentField.SetValue(null, Instance);
        Assert.Same(Instance, Mission.Current);
    }

    /// <summary>Marks the mission as a siege battle (<c>IsSiegeBattle</c> compares the team-AI type to Siege).</summary>
    public MissionCurrentScope AsSiegeBattle()
    {
        TeamAITypeField.SetValue(Instance, Mission.MissionTeamAITypeEnum.Siege);
        Assert.True(Mission.Current.IsSiegeBattle);
        return this;
    }

    public void Dispose() => CurrentField.SetValue(null, null);
}
