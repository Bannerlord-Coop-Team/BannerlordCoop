using Common.Messaging;

namespace GameInterface.Services.Stances.Messages
{
    /// <summary>
    /// Command handled on the client when the server sends NetworkDeclareWar.
    /// </summary>
    public class DeclareWarChanged : ICommand
    {
        public string Faction1Id { get; }
        public string Faction2Id { get; }
        public int Detail { get; }

        public DeclareWarChanged(string faction1Id, string faction2Id, int detail)
        {
            Faction1Id = faction1Id;
            Faction2Id = faction2Id;
            Detail = detail;
        }
    }
}
