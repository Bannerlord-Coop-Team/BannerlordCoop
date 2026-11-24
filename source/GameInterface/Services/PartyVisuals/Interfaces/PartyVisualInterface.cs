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
    /// <summary>Replicates visual creation when a party is added on a headless server.</summary>
    void OnPartyAdded(MobileParty party);

    /// <summary>Replicates visual removal when a party is destroyed on a headless server.</summary>
    void OnPartyDestroyed(MobileParty party);

    /// <summary>Replicates a visual created by the graphical map.</summary>
    void OnNativeVisualCreated(MobileParty party);

    /// <summary>Replicates a visual removed by the graphical map.</summary>
    void OnNativeVisualRemoved(MobileParty party);

    /// <summary>Replicates visual creation and creates the local visual when the server has a visual manager.</summary>
    void CreateVisual(MobileParty party);

    /// <summary>Replicates visual removal and removes the local visual when the server has a visual manager.</summary>
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

    public void OnNativeVisualCreated(MobileParty party)
    {
        // Map initialization can build visuals before the campaign registers their parties.
        if (objectManager.TryGetHandle(party, out var partyHandle))
            network.SendAll(new NetworkCreatePartyVisual(partyHandle));
    }

    public void OnNativeVisualRemoved(MobileParty party) => SendDestroyVisual(party);

    public void CreateVisual(MobileParty party)
    {
        if (IsHeadlessServer)
            SendCreateVisual(party);
        else
            // PartyVisualLifetimePatches replicates the native creation.
            party.CreateNewPartyVisual();
    }

    public void RemoveVisual(MobileParty party)
    {
        if (IsHeadlessServer)
            SendDestroyVisual(party);
        else
            // PartyVisualLifetimePatches replicates the native removal.
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
