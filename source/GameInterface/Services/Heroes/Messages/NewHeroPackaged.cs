namespace GameInterface.Services.CharacterCreation.Messages
{
    public readonly struct NewHeroPackaged
    {
        public byte[] Package { get; }

        public NewHeroPackaged(byte[] package)
        {
            Package = package;
        }
    }
}