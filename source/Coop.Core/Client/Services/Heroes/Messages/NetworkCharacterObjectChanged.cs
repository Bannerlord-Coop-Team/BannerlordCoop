using Common.Messaging;
using Common.PacketHandlers;
using ProtoBuf;

namespace Coop.Core.Client.Services.Heroes.Messages
{
    /// <summary>
    /// Network Command for _characterObject
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    public record NetworkCharacterObjectChanged : ICommand
    {
        [ProtoMember(1)]
        public string CharacterObjectId { get; }
        [ProtoMember(2)]
        public string HeroId { get; }

        public NetworkCharacterObjectChanged(string characterObjectId, string heroId)
        {
            CharacterObjectId = characterObjectId;
            HeroId = heroId;
        }
    }
}