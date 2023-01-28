namespace GameInterface.Services.Heroes.Handlers
{
    public readonly struct NewPlayerHeroRecieved
    {
        public byte[] Bytes { get; }
        public NewPlayerHeroRecieved(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
}