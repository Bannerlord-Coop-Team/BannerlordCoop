using GameInterface.Serialization.External;

namespace GameInterface.Services.CharacterCreation.Messages
{
    public readonly struct CharacterCreatedHeroPackaged
    {
        public HeroBinaryPackage Package { get; }

        public CharacterCreatedHeroPackaged(HeroBinaryPackage package)
        {
            Package = package;
        }
    }
}