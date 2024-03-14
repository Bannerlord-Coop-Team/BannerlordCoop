using Autofac;
using GameInterface.Services.Armies.Extensions;
using GameInterface.Services.ObjectManager;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace GameInterface.Surrogates;

[ProtoContract]
internal struct ArmySurrogate
{
    [ProtoMember(1)]
    public string StringId { get; }

    public ArmySurrogate(Army army)
    {
        StringId = army.GetStringId();
    }

    public static implicit operator ArmySurrogate(Army army)
    {
        return new ArmySurrogate(army);
    }

    public static implicit operator Army(ArmySurrogate surrogate)
    {
        if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager) == false) return null;

        if (objectManager.TryGetObject<Army>(surrogate.StringId, out var army) == false) return null;

        return army;
    }
}
