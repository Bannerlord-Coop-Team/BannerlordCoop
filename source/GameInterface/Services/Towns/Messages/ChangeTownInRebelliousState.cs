using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the InRebelliousState changes in a Town.
    /// </summary>
    public record ChangeTownInRebelliousState : ICommand
    {
        public string TownId { get; }
        public bool InRebelliousState { get; }

        public ChangeTownInRebelliousState(string townId, bool inRebelliousState)
        {
            TownId = townId;
            InRebelliousState = inRebelliousState;
        }
    }
}
