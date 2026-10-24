using System;
using System.Linq;
using System.Text;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>Owns the client's server info panel for one co-op session.</summary>
public interface IServerInfoService : IGameAbstraction
{
    void Initialize();

    /// <summary>Replaces what the panel shows and, while it is closed, opens it once the map is free.</summary>
    void Show(NetworkServerInfo info);

    /// <summary>F3 on the campaign map: closes the open panel, or opens the last info on its first tab; false when nothing changed.</summary>
    bool Toggle();

    string Describe();
}

/// <inheritdoc cref="IServerInfoService"/>
public sealed class ServerInfoService : IServerInfoService, IDisposable
{
    private readonly Func<ServerInfoVM, Action, Action, IServerInfoPopup> createPopup;
    private readonly ServerInfoVM viewModel;
    private IServerInfoPopup popup;
    private bool pending;

    // Builds the Gauntlet overlay only when Initialize runs on a client with a campaign.
    public ServerInfoService(IMapAvailability mapAvailability, IServerInfoLinkRules linkRules, IBrowserLinkOpener opener) : this((viewModel, update, toggle) =>
    {
        var overlay = new ServerInfoOverlay(viewModel, mapAvailability, update, toggle);
        overlay.Initialize();
        return overlay;
    }, linkRules, opener)
    {
    }

    internal ServerInfoService(Func<ServerInfoVM, Action, Action, IServerInfoPopup> createPopup, IServerInfoLinkRules linkRules, IBrowserLinkOpener opener)
    {
        this.createPopup = createPopup;
        viewModel = new ServerInfoVM(() => popup?.Close(), linkRules, opener);
    }

    // Creates the map overlay once; info that arrived earlier waits for it.
    public void Initialize()
    {
        if (popup != null) return;
        popup = createPopup(viewModel, Update, () => Toggle());
    }

    // Keeps the newest info; info with nothing to show closes the panel instead.
    public void Show(NetworkServerInfo info)
    {
        if (!viewModel.SetContent(info))
        {
            pending = false;
            popup?.Close();
            return;
        }

        if (!viewModel.IsOpen) pending = true;
    }

    // Opens only when the map is free, so F3 in a battle or a town menu does nothing instead of queueing an open.
    public bool Toggle()
    {
        if (popup == null) return false;
        if (viewModel.IsOpen)
        {
            popup.Close();
            return true;
        }

        if (!viewModel.HasContent || !popup.CanOpen()) return false;
        Open();
        return true;
    }

    // Runs each frame while closed: opens pending info once the map is free.
    internal void Update()
    {
        if (!pending || popup == null || viewModel.IsOpen || !popup.CanOpen()) return;
        Open();
    }

    // Every open starts on the first tab.
    private void Open()
    {
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
