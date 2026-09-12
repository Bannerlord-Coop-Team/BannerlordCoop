using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRefreshAfterClanNameChange : ICommand
{
    [ProtoMember(1)]
    public readonly string ClanId;

    [ProtoMember(2)]
    public readonly TextObject Name;

    [ProtoMember(3)]
    public readonly TextObject InformalName;

    public NetworkRefreshAfterClanNameChange(
        string clanId,
        TextObject name,
        TextObject informalName)
    {
        ClanId = clanId;
        Name = name;
        InformalName = informalName;
    }
}
