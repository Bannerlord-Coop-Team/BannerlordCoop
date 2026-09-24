using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CharacterSkills.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateCharacterSkills : ICommand
    {
        [ProtoMember(1)]
        public string CharacterSkillsId;
        [ProtoMember(2)]
        public uint Handle;
        public NetworkCreateCharacterSkills(string characterSkillsId, uint handle)
        {
            CharacterSkillsId = characterSkillsId;
            Handle = handle;
        }
    }
}
