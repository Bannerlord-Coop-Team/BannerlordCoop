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
    public readonly MountLoss Loss;

    public NetworkMainHeroMountKilled(string heroId, bool usesCivilianEquipment, MountLoss loss)
    {
        HeroId = heroId;
        UsesCivilianEquipment = usesCivilianEquipment;
        Loss = loss;
    }
}
