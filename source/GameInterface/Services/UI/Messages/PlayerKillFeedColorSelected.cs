using Common.Messaging;

namespace GameInterface.Services.UI.Messages;

public readonly struct PlayerKillFeedColorSelected : IEvent
{
    public readonly PlayerKillFeedColor Color;

    public PlayerKillFeedColorSelected(PlayerKillFeedColor color)
    {
        Color = color;
    }
}
