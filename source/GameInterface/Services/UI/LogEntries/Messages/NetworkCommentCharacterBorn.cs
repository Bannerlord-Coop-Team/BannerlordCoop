using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.LogEntries.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCommentCharacterBorn : ICommand
{
    [ProtoMember(1)]
    public readonly string HeroId;

    public NetworkCommentCharacterBorn(string heroId)
    {
        HeroId = heroId;
    }
}
