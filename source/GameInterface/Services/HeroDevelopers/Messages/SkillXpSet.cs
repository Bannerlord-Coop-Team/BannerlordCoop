using Common.Messaging;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.HeroDevelopers.Messages
{
    public record SkillXpSet : IEvent
    {
        public HeroDeveloper HeroDeveloper;
        public SkillObject SkillObject;
        public float Value;

        public SkillXpSet(HeroDeveloper heroDeveloper, SkillObject skillObject, float value)
        {
            HeroDeveloper = heroDeveloper;
            SkillObject = skillObject;
            Value = value;
        }
    }
}
