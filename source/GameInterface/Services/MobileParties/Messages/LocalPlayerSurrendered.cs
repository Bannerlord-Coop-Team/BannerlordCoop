using Common.Messaging;

namespace GameInterface.Services.MobileParties.Messages
{
    /// <summary>
    /// Event sent when player surrenders
    /// </summary>
    public record LocalPlayerSurrendered : IEvent
    {
        public string PlayerPartyId { get; }
        public string CaptorPartyId { get; }
        public string CharacterId { get; }

        public LocalPlayerSurrendered(string playerPartyId, string captorPartyId, string characterId)
        {
            PlayerPartyId = playerPartyId;
            CaptorPartyId = captorPartyId;
            CharacterId = characterId;
        }
    }
}
