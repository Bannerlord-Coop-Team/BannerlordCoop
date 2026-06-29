using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.Notifications.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkNotifyDailyGoldChange : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly int GoldChange;

    public NetworkNotifyDailyGoldChange(string clanId, int goldChange)
    {
        ClanId = clanId;
        GoldChange = goldChange;
    }
}
