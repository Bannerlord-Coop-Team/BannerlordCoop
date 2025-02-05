using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.CharacterSkills.Messages
{
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateCharacterSkills : ICommand
    {
        [ProtoMember(1)]
        public string CharacterSkillsId;
        public NetworkCreateCharacterSkills(string characterSkillsId)
        {
            CharacterSkillsId = characterSkillsId;
        }
    }
}
