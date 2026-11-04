using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.Barter;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Adds the spouse portrait that the barter item prefab shows beside the vanilla one.
internal class PlayerPartyMarriageBarterItemVM : BarterItemVM
{
    // Portraits only render undistorted at full slot size, so they are shifted apart rather than shrunk.
    private const float PortraitShift = 28f;

    [DataSourceProperty]
    public GenericImageIdentifierVM SpouseVisualIdentifier { get; }

    [DataSourceProperty]
    public bool HasSpouseVisualIdentifier => SpouseVisualIdentifier != null;

    [DataSourceProperty]
    public float PortraitXOffset => -PortraitShift;

    [DataSourceProperty]
    public float SpousePortraitXOffset => PortraitShift;

    public PlayerPartyMarriageBarterItemVM(
        PlayerPartyMarriageBarterable barterable,
        BarterTransferEventDelegate onTransfer,
        Action onAmountChange) : base(barterable, onTransfer, onAmountChange)
    {
        SpouseVisualIdentifier = new GenericImageIdentifierVM(barterable.GetSpouseVisualIdentifier());
    }
}
