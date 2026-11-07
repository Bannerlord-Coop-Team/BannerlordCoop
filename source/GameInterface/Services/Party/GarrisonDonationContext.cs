using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Party;

/// <summary>
/// Carries the town a garrison donation Done handler donated troops to back to the party screen Done that called it.
/// </summary>
internal interface IGarrisonDonationContext
{
    /// <summary>The town this Done donates troops to, or null when it donates none.</summary>
    Settlement DonationSettlement { get; set; }

    /// <summary>Starts a Done with no donation and restores the previous town when disposed.</summary>
    IDisposable BeginDone();
}

/// <summary>
/// Holds the garrison donation town for the party screen Done that is running on this client.
/// </summary>
internal class GarrisonDonationContext : IGarrisonDonationContext
{
    /// <inheritdoc/>
    public Settlement DonationSettlement { get; set; }

    /// <inheritdoc/>
    public IDisposable BeginDone() => new DoneScope(this);

    private sealed class DoneScope : IDisposable
    {
        private readonly GarrisonDonationContext context;
        private readonly Settlement previousSettlement;

        public DoneScope(GarrisonDonationContext context)
        {
            this.context = context;
            previousSettlement = context.DonationSettlement;
            context.DonationSettlement = null;
        }

        public void Dispose()
        {
            context.DonationSettlement = previousSettlement;
        }
    }
}
