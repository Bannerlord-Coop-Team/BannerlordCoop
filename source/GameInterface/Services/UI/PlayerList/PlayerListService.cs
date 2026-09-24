using GameInterface.Services.Chat;
using Common.Messaging;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.UITab;
using TaleWorlds.InputSystem;
using System;
using System.Linq;

namespace GameInterface.Services.UI.PlayerList;

/// <summary>Owns the client player-list presentation for one co-op session.</summary>
public interface IPlayerListService : IGameAbstraction
{
    void Initialize();
    void Update(PlayerListEntry[] entries);
    bool Toggle();
    string Describe();
#if DEBUG
    void PreviewLayout(bool enabled);
#endif
}

/// <inheritdoc cref="IPlayerListService"/>
public sealed class PlayerListService : IPlayerListService, IDisposable
{
    private readonly IChatService chat;
    private readonly IMessageBroker messageBroker;
    private InputKey toggleKey;
    private PlayerListOverlay overlay;
    private readonly PlayerListVM viewModel;
#if DEBUG
    private bool previewLayout;
    private PlayerListEntry[] receivedEntries = Array.Empty<PlayerListEntry>();
#endif

    // Keeps chat focus separate from the player-list toggle.
    public PlayerListService(IChatService chat, ICoopOptionsStore optionsStore, IMessageBroker messageBroker)
    {
        this.chat = chat;
        this.messageBroker = messageBroker;
        toggleKey = UIOptionsTabProvider.GetPlayerListKey(optionsStore.LoadOrDefault());
        messageBroker.Subscribe<PlayerListKeySelected>(HandleKeySelected);
        viewModel = new PlayerListVM(() => overlay?.Close());
    }

    // Creates the map-only global layer once the campaign is ready.
    public void Initialize()
    {
        if (overlay != null) return;
        overlay = new PlayerListOverlay(viewModel, chat, () => toggleKey);
        overlay.Initialize();
    }

    // Applies a presentation snapshot without mutating the client's player registry.
    public void Update(PlayerListEntry[] entries)
    {
#if DEBUG
        receivedEntries = entries;
        if (previewLayout) return;
#endif
        viewModel.Update(entries);
    }

#if DEBUG
    // Previews overflowing names and all activity icons without changing players or saved game data.
    public void PreviewLayout(bool enabled)
    {
        previewLayout = enabled;
        if (!enabled)
        {
            viewModel.Update(receivedEntries);
            return;
        }
        viewModel.Update(Enumerable.Range(0, 20).Select(index => new PlayerListEntry
        {
            ControllerId = $"layout-preview-{index}",
            PlatformName = index == 0 ? "Preview: A very long platform display name that exceeds this column" : $"Preview Player {index + 1:00}",
            HeroName = index == 0 ? "Preview: A very long hero name that exceeds this column" : $"Preview Hero {index + 1:00}",
            Online = index % 10 != 9,
            Activity = (PlayerActivity)(index % 10 == 9 ? 0 : index % 10)
        }).ToArray());
    }
#endif

    // Shares the exact UI toggle with the debug command used by live tests.
    public bool Toggle() => overlay?.Toggle() == true;

    // Exposes read-only UI state for manual verification on the receiving client.
    public string Describe() => $"Open: {viewModel.IsOpen}\n" + string.Join("\n",
        viewModel.Rows.Select(row => $"{row.ControllerId}: {row.PlatformName} | {row.HeroName} | {row.Status}"));

    // Applies saved key changes without recreating the map overlay.
    private void HandleKeySelected(MessagePayload<PlayerListKeySelected> payload) => toggleKey = payload.What.Key;

    // Removes the global layer and its bindings when the co-op session ends.
    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerListKeySelected>(HandleKeySelected);
        overlay?.Dispose();
        overlay = null;
        viewModel.OnFinalize();
    }
}
