using GameInterface.Services.Chat;
using System;
using System.Linq;
using System.Text;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Owns the client's server info panel for one co-op session.</summary>
public interface IServerInfoService : IGameAbstraction
{
    void Initialize();
    void Show(NetworkServerInfo info);

    /// <summary>Opens the last info this session received again, once the map is free; false when there is none.</summary>
    bool Reopen();

    /// <summary>Forgets this session's info and closes the panel, as when the client disconnects.</summary>
    void Clear();

    string Describe();
}

/// <inheritdoc cref="IServerInfoService"/>
public sealed class ServerInfoService : IServerInfoService, IDisposable
{
    private readonly Func<ServerInfoVM, Action, IServerInfoPopup> createPopup;
    private readonly ServerInfoVM viewModel;
    private IServerInfoPopup popup;
    private bool pending;

    // Builds the Gauntlet overlay only when Initialize runs on a client with a campaign.
    public ServerInfoService(IChatService chat, IServerInfoLinkRules linkRules, IBrowserLinkOpener opener) : this((viewModel, update) =>
    {
        var overlay = new ServerInfoOverlay(viewModel, chat, update);
        overlay.Initialize();
        return overlay;
    }, linkRules, opener)
    {
    }

    internal ServerInfoService(Func<ServerInfoVM, Action, IServerInfoPopup> createPopup, IServerInfoLinkRules linkRules, IBrowserLinkOpener opener)
    {
        this.createPopup = createPopup;
        viewModel = new ServerInfoVM(() => popup?.Close(), linkRules, opener);
    }

    // Creates the map overlay once; info that arrived earlier waits for it.
    public void Initialize()
    {
        if (popup != null) return;
        popup = createPopup(viewModel, Update);
    }

    // Keeps the newest info for this join; info with nothing to show opens nothing.
    public void Show(NetworkServerInfo info)
    {
        if (!viewModel.SetContent(info))
        {
            pending = false;
            popup?.Close();
            return;
        }

        // Info that arrives while the panel is open replaces it in place instead of opening it twice.
        pending = !viewModel.IsOpen;
    }

    // Behind !motd: an open panel stays as it is, a closed one waits for the map like a join.
    public bool Reopen()
    {
        if (!viewModel.HasContent) return false;
        if (!viewModel.IsOpen) pending = true;
        return true;
    }

    public void Clear()
    {
        pending = false;
        popup?.Close();
        viewModel.SetContent(null);
    }

    // Runs each frame while closed: opens pending info once, on its first tab, when the map is free.
    internal void Update()
    {
        if (!pending || popup == null || viewModel.IsOpen || !popup.CanOpen()) return;
        pending = false;
        viewModel.SelectFirstTab();
        popup.Open();
    }

    // Exposes read-only panel state for manual verification on the receiving client.
    public string Describe()
    {
        var tabs = viewModel.VisibleTabs.ToArray();
        var text = new StringBuilder()
            .Append("Open: ").Append(viewModel.IsOpen)
            .Append("\nPending: ").Append(pending)
            .Append("\nTab: ").Append(tabs.Length == 0 ? "none" : viewModel.SelectedTab.ToString())
            .Append("\nTabs: ").Append(tabs.Length == 0 ? "none" : string.Join(", ", tabs))
            .Append("\nCounts: motd ").Append(viewModel.Paragraphs.Count)
            .Append(", rules ").Append(viewModel.Rules.Count)
            .Append(", links ").Append(viewModel.Links.Count)
            .Append(", news ").Append(viewModel.News.Count)
            .Append("\nLink dialog: ").Append(viewModel.IsLinkDialogOpen ? "open " + viewModel.LinkDialogAddress : "closed");
        for (int i = 0; i < viewModel.Links.Count; i++)
            text.Append("\nLink ").Append(i + 1).Append(": ").Append(viewModel.Links[i].Label).Append(" | ").Append(viewModel.Links[i].Address);
        return text.ToString();
    }

    // Removes the global layer when the co-op session ends.
    public void Dispose()
    {
        popup?.Dispose();
        popup = null;
        pending = false;
        viewModel.OnFinalize();
    }
}
