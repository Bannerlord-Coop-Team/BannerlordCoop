using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Equipments.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkMainHeroMountKilled : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;
    [ProtoMember(2)]
    public readonly bool UsesCivilianEquipment;
    [ProtoMember(3)]
    public readonly bool HorseLost;

    public NetworkMainHeroMountKilled(string heroId, bool usesCivilianEquipment, bool horseLost)
    {
        HeroId = heroId;
        UsesCivilianEquipment = usesCivilianEquipment;
        HorseLost = horseLost;
    }
}
