using Common.Messaging;

namespace GameInterface.Services.BesiegerCamps.Messages;

/// <summary>
/// Command to add an attached party
/// </summary>
public record NetworkAddBesiegerParty : ICommand
{
    public NetworkAddBesiegerParty(string besiegerCampId, string besiegerPartyId)
    {
        BesiegerCampId = besiegerCampId;
        BesiegerPartyId = besiegerPartyId;
    }

    public string BesiegerCampId { get; }
    public string BesiegerPartyId { get; }
}