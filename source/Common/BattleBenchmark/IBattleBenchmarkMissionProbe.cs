namespace Common.BattleBenchmark;

/// <summary>Lets the battle benchmark ask the mission layer about the current battle; implemented in Missions.</summary>
public interface IBattleBenchmarkMissionProbe
{
    BattleBenchmarkMissionState Capture();
}

/// <summary>What this process's current mission looked like when the benchmark sampled it.</summary>
public sealed class BattleBenchmarkMissionState
{
    public static readonly BattleBenchmarkMissionState None = new BattleBenchmarkMissionState();

    public bool InMission { get; set; }

    public bool IsBattleHost { get; set; }

    public string ControllerId { get; set; }

    public string Scene { get; set; }

    public int ActiveAgents { get; set; }

    /// <summary>Active agents this process currently has authority over.</summary>
    public int OwnedAgents { get; set; }
}
