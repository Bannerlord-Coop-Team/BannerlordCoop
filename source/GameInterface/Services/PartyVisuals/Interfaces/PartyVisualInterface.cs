using Common;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PartyVisuals.Extensions;
using GameInterface.Services.PartyVisuals.Messages;
using SandBox.View.Map.Managers;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyVisuals.Interfaces;

/// <summary>Applies server-side party visual changes and replicates them to clients.</summary>
public interface IPartyVisualInterface : IGameAbstraction
{
    bool IsHeadlessServer { get; }
    void CreateVisual(MobileParty party);
    void RemoveVisual(MobileParty party);
}

/// <inheritdoc cref="IPartyVisualInterface"/>
internal sealed class PartyVisualInterface : IPartyVisualInterface
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PartyVisualInterface(IObjectManager objectManager, INetwork network)
    {
        this.objectManager = objectManager;
        this.network = network;
    }

    public bool IsHeadlessServer => ModInformation.IsServer && MobilePartyVisualManager.Current == null;

    public void CreateVisual(MobileParty party)
    {
        if (!IsHeadlessServer)
        {
            // The graphical visual lifetime patches replicate creation and removal.
            party.CreateNewPartyVisual();
            return;
        }

        if (objectManager.TryGetHandleWithLogging(party, out var partyHandle))
            network.SendAll(new NetworkCreatePartyVisual(partyHandle));
    }

    public void RemoveVisual(MobileParty party)
    {
        if (!IsHeadlessServer)
        {
            party.RemovePartyVisual();
            return;
        }

        if (objectManager.TryGetHandleWithLogging(party, out var partyHandle))
            network.SendAll(new NetworkDestroyPartyVisual(partyHandle));
    }
}
