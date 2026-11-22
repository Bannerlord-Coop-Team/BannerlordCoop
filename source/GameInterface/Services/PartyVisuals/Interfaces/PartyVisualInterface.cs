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
    void OnPartyAdded(MobileParty party);
    void OnPartyDestroyed(MobileParty party);
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

    private bool IsHeadlessServer => ModInformation.IsServer && MobilePartyVisualManager.Current == null;

    public void OnPartyAdded(MobileParty party)
    {
        if (IsHeadlessServer)
            SendCreateVisual(party);
    }

    public void OnPartyDestroyed(MobileParty party)
    {
        if (IsHeadlessServer)
            SendDestroyVisual(party);
    }

    // With a visual manager, the graphical lifetime patches replicate creation and removal.
    public void CreateVisual(MobileParty party)
    {
        if (IsHeadlessServer)
            SendCreateVisual(party);
        else
            party.CreateNewPartyVisual();
    }

    public void RemoveVisual(MobileParty party)
    {
        if (IsHeadlessServer)
            SendDestroyVisual(party);
        else
            party.RemovePartyVisual();
    }

    private void SendCreateVisual(MobileParty party)
    {
        if (objectManager.TryGetHandleWithLogging(party, out var partyHandle))
            network.SendAll(new NetworkCreatePartyVisual(partyHandle));
    }

    private void SendDestroyVisual(MobileParty party)
    {
        if (objectManager.TryGetHandleWithLogging(party, out var partyHandle))
            network.SendAll(new NetworkDestroyPartyVisual(partyHandle));
    }
}
