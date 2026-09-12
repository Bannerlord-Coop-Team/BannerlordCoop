using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.UI.LogEntries.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLogPlayerRetired : ICommand
{
    [ProtoMember(1)]
    public readonly string RetiredHeroId;

    public NetworkLogPlayerRetired(string retiredHeroId)
    {
        RetiredHeroId = retiredHeroId;
    }
}
