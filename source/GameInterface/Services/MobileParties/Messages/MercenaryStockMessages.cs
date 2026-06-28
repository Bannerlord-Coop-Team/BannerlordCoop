using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages;

internal readonly struct MercenaryStockChanged : IEvent
{
    public readonly Town Town;
    public readonly CharacterObject TroopType;
    public readonly int Number;

    public MercenaryStockChanged(Town town, CharacterObject troopType, int number)
    {
        Town = town;
        TroopType = troopType;
        Number = number;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateMercenaryStock : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;
    [ProtoMember(2)]
    public readonly string TroopTypeId;
    [ProtoMember(3)]
    public readonly int Number;

    public NetworkUpdateMercenaryStock(string townId, string troopTypeId, int number)
    {
        TownId = townId;
        TroopTypeId = troopTypeId;
        Number = number;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestMercenaryStockAudit : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;

    public NetworkRequestMercenaryStockAudit(string townId)
    {
        TownId = townId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestMercenaryStockSync : ICommand
{
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMercenaryStockAudit : ICommand
{
    [ProtoMember(1)]
    public readonly string TownId;
    [ProtoMember(2)]
    public readonly string TroopTypeId;
    [ProtoMember(3)]
    public readonly int Number;

    public NetworkMercenaryStockAudit(string townId, string troopTypeId, int number)
    {
        TownId = townId;
        TroopTypeId = troopTypeId;
        Number = number;
    }
}
