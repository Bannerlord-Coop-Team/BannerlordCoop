using Common.Messaging;
using GameInterface.Services.Players.Data;
using ProtoBuf;

namespace GameInterface.Services.Heroes.HeirSelection.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkChangePlayerCharacterAfterHeirSelection : ICommand
{
    [ProtoMember(1)]
    public readonly Player Player;

    [ProtoMember(2)]
    public readonly string OriginalHeroId;

    public NetworkChangePlayerCharacterAfterHeirSelection(
        Player player,
        string originalHeroId)
    {
        Player = player;
        OriginalHeroId = originalHeroId;
    }
}
