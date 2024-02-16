using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the Security changes in a Town.
    /// </summary>
    public record TownSecurityChanged : ICommand
    {
        public string TownId { get; }
        public float Security { get; }

        public TownSecurityChanged(string townId, float security)
        {
            TownId = townId;
            Security = security;
        }
    }
}
