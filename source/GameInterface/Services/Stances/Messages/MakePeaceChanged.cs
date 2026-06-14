using Common.Messaging;

namespace GameInterface.Services.Stances.Messages
{
    /// <summary>
    /// Command handled on the client when the server sends NetworkMakePeace.
    /// </summary>
    public class MakePeaceChanged : ICommand
    {
        public string Faction1Id { get; }
        public string Faction2Id { get; }
        public int DailyTribute { get; }
        public int DailyTributeDuration { get; }
        public int Detail { get; }

        public MakePeaceChanged(string faction1Id, string faction2Id, int dailyTribute, int dailyTributeDuration, int detail)
        {
            Faction1Id = faction1Id;
            Faction2Id = faction2Id;
            DailyTribute = dailyTribute;
            DailyTributeDuration = dailyTributeDuration;
            Detail = detail;
        }
    }
}
