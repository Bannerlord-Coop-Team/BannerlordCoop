using Common.Messaging;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Command to remove an attached party
/// </summary>
public record NetworkRemoveBesiegerParty : ICommand
{
    public NetworkRemoveBesiegerParty(string besiegerCampId, string besiegerPartyId)
    {
        BesiegerCampId = besiegerCampId;
        BesiegerPartyId = besiegerPartyId;
    }

    public string BesiegerCampId { get; }
    public string BesiegerPartyId { get; }
}