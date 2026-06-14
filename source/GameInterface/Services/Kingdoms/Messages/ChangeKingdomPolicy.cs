using Common.Messaging;

namespace GameInterface.Services.Kingdoms.Messages
{
    /// <summary>
    /// Command handled on the client when the server sends NetworkChangeKingdomPolicy.
    /// </summary>
    public class ChangeKingdomPolicy : ICommand
    {
        public string KingdomId { get; }
        public string PolicyId { get; }
        public bool IsAdd { get; }

        public ChangeKingdomPolicy(string kingdomId, string policyId, bool isAdd)
        {
            KingdomId = kingdomId;
            PolicyId = policyId;
            IsAdd = isAdd;
        }
    }
}
