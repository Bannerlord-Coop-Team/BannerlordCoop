using GameInterface.Services;
using System;
using System.Threading;

namespace GameInterface.Services.MapEvents.BattleSize;

/// <summary>Provides the server-authoritative human-agent budget for the next battle mission.</summary>
public interface IServerBattleSizeProvider : IGameAbstraction
{
    int BattleSize { get; }

    void SetBattleSize(int battleSize);
}

/// <inheritdoc cref="IServerBattleSizeProvider"/>
public class ServerBattleSizeProvider : IServerBattleSizeProvider
{
    public const int MinimumBattleSize = 200;
    public const int MaximumBattleSize = 1000;
    public const int DefaultBattleSize = MaximumBattleSize;

    private int battleSize = DefaultBattleSize;

    public int BattleSize => Volatile.Read(ref battleSize);

    public void SetBattleSize(int battleSize)
    {
        Volatile.Write(ref this.battleSize, ClampBattleSize(battleSize));
    }

    public static int ClampBattleSize(int battleSize)
    {
        return Math.Max(MinimumBattleSize, Math.Min(MaximumBattleSize, battleSize));
    }
}
