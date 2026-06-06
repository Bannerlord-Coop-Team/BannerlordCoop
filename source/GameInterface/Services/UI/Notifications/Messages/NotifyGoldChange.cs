using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NotifyGoldChange : ICommand
{
    [ProtoMember(1)]
    public readonly int GoldAmount;

    public NotifyGoldChange(int goldAmount)
    {
        GoldAmount = goldAmount;
    }
}