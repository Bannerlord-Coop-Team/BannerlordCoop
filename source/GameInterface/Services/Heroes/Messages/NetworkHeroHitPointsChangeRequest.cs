using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Heroes.Messages;

/// <summary>
/// Sent from a client to the server to request a change to a controlled hero's <see cref="TaleWorlds.CampaignSystem.Hero.HitPoints"/>.
/// The server applies it authoritatively; the existing HitPoints property sync then replicates the value
/// back to every client.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkHeroHitPointsChangeRequest : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;
    [ProtoMember(2)]
    public readonly int HitPoints;

    public NetworkHeroHitPointsChangeRequest(string heroId, int hitPoints)
    {
        HeroId = heroId;
        HitPoints = hitPoints;
    }
}
