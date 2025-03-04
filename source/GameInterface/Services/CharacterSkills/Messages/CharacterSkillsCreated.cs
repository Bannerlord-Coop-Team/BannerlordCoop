using Common.Messaging;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterSkills.Messages
{
    internal class CharacterSkillsCreated : IEvent
    {
        public MBCharacterSkills CharacterSkills { get; }

        public CharacterSkillsCreated(MBCharacterSkills characterSkills)
        {
            CharacterSkills = characterSkills;
        }
    }
}
