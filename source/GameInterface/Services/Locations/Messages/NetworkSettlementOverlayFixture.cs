#if DEBUG
using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Locations.Messages;

internal enum SettlementOverlayFixtureOperation
{
    Inject,
    Restore,
    Cleanup,
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSettlementOverlayFixture : ICommand
{
    [ProtoMember(1)]
    public readonly string TargetHeroId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    [ProtoMember(3)]
    public readonly SettlementOverlayFixtureOperation Operation;

    public NetworkSettlementOverlayFixture(
        string targetHeroId,
        string settlementId,
        SettlementOverlayFixtureOperation operation)
    {
        TargetHeroId = targetHeroId;
        SettlementId = settlementId;
        Operation = operation;
    }
}
#endif
