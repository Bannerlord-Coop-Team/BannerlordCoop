using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Data
{
    public class CharacterObjectWrapper : WrapperBase<CharacterObject>
    {
        public static event Action<CharacterObjectWrapper> OnCharacterObjectCreated;

        internal CharacterObjectWrapper(CharacterObject characterObject) : base(characterObject) { OnCharacterObjectCreated?.Invoke(this); }
        internal CharacterObjectWrapper(Guid guid) : base(guid) { }
    }
}