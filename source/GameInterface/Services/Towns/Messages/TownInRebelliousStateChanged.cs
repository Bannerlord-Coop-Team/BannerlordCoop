using Common.Messaging;

namespace GameInterface.Services.Towns.Messages
{
    /// <summary>
    /// Used when the InRebelliousState changes in a Town.
    /// </summary>
    public record TownInRebelliousStateChanged : ICommand
    {
        public string TownId { get; }
        public bool InRebelliousState { get; }

        public TownInRebelliousStateChanged(string townId, bool inRebelliousState)
        {
            TownId = townId;
            InRebelliousState = inRebelliousState;
        }
    }
}
