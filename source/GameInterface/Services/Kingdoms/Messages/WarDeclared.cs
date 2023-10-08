using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Event to declare war from game interface
    /// </summary>
    public record WarDeclared : IEvent
    {
        public string Faction1Id { get; }
        public string Faction2Id { get; }
        public int Detail { get; }

        public WarDeclared(string faction1Id, string faction2Id, int detail)
        {
            Faction1Id = faction1Id;
            Faction2Id = faction2Id;
            Detail = detail;
        }
    }
}