using GameInterface.Serialization.External;

namespace GameInterface.Services.CharacterCreation.Messages
{
    public readonly struct CharacterCreatedHeroPackaged
    {
        public byte[] Package { get; }

        public CharacterCreatedHeroPackaged(byte[] package)
        {
            Package = package;
        }
    }
}