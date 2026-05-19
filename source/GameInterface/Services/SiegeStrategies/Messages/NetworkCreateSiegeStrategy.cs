using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.SiegeStrategies.Messages.Lifetime;

[ProtoContract(SkipConstructor = true)]
internal class NetworkCreateSiegeStrategy : ICommand
{
    [ProtoMember(1)]
    public string SiegeStrategyId { get; }

    public NetworkCreateSiegeStrategy(string siegeStrategyId)
    {
        SiegeStrategyId = siegeStrategyId;
    }
}