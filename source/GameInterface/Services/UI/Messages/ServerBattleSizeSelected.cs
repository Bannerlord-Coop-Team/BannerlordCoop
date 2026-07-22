using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

/// <summary>Reports a battle-size value applied from the dedicated server options screen.</summary>
public readonly struct ServerBattleSizeSelected : IEvent
{
    public readonly int BattleSize;

    public ServerBattleSizeSelected(int battleSize)
    {
        BattleSize = battleSize;
    }
}
