using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct SettlementMenuUse
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string MenuId;

    [ProtoMember(3)]
    public readonly string HeroId;

    public SettlementMenuUse(string settlementId, string menuId, string heroId)
    {
        SettlementId = settlementId;
        MenuId = menuId;
        HeroId = heroId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestSettlementMenuAccess : ICommand
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string MenuId;

    [ProtoMember(3)]
    public readonly bool Open;

    public RequestSettlementMenuAccess(string settlementId, string menuId, bool open)
    {
        SettlementId = settlementId;
        MenuId = menuId;
        Open = open;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSettlementMenuAccess : IEvent
{
    [ProtoMember(1)]
    public readonly string SettlementId;

    [ProtoMember(2)]
    public readonly string MenuId;

    [ProtoMember(3)]
    public readonly bool Granted;

    public NetworkSettlementMenuAccess(string settlementId, string menuId, bool granted)
    {
        SettlementId = settlementId;
        MenuId = menuId;
        Granted = granted;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSettlementMenusChanged : IEvent
{
    [ProtoMember(1)]
    public readonly SettlementMenuUse[] Menus;

    public NetworkSettlementMenusChanged(SettlementMenuUse[] menus)
    {
        Menus = menus;
    }
}
