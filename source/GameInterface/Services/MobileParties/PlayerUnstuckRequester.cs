using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Unstuck;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Client side of coop.unstuck, shared by the console command and the escape menu "I'm stuck" button.
/// </summary>
internal interface IPlayerUnstuckRequester
{
    /// <summary>
    /// Publishes <see cref="PlayerUnstuckRequested"/> for the main party.
    /// </summary>
    /// <returns>False when this client has no main party.</returns>
    bool TryRequest();
}

/// <inheritdoc cref="IPlayerUnstuckRequester"/>
internal class PlayerUnstuckRequester : IPlayerUnstuckRequester
{
    private readonly IMessageBroker messageBroker;

    public PlayerUnstuckRequester(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
    }

    public bool TryRequest()
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null) return false;

        messageBroker.Publish(mainParty, new PlayerUnstuckRequested(mainParty));
        return true;
    }
}
