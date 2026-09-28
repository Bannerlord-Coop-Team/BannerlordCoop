using GameInterface.Services.Chat;
using System;
using System.Linq;

namespace GameInterface.Services.UI.Motd;

/// <summary>Owns the client's message of the day popup for one co-op session.</summary>
public interface IMotdService : IGameAbstraction
{
    void Initialize();
    void Show(string[] paragraphs);
    string Describe();
}

/// <inheritdoc cref="IMotdService"/>
public sealed class MotdService : IMotdService, IDisposable
{
    private readonly Func<MotdVM, Action, IMotdPopup> createPopup;
    private readonly MotdVM viewModel;
    private IMotdPopup popup;
    private string[] pending;

    // Builds the Gauntlet overlay only when Initialize runs on a client with a campaign.
    public MotdService(IChatService chat) : this((viewModel, update) =>
    {
        var overlay = new MotdOverlay(viewModel, chat, update);
        overlay.Initialize();
        return overlay;
    })
    {
    }

    internal MotdService(Func<MotdVM, Action, IMotdPopup> createPopup)
    {
        this.createPopup = createPopup;
        viewModel = new MotdVM(() => popup?.Close());
    }

    // Creates the map overlay once; a message that arrived earlier waits for it.
    public void Initialize()
    {
        if (popup != null) return;
        popup = createPopup(viewModel, Update);
    }

    // Keeps the newest message for this join; missing or blank text shows nothing.
    public void Show(string[] paragraphs)
    {
        var text = paragraphs?.Where(paragraph => !string.IsNullOrWhiteSpace(paragraph)).ToArray() ?? Array.Empty<string>();
        if (text.Length == 0) return;
        if (viewModel.IsOpen)
        {
            viewModel.SetParagraphs(text);
            return;
        }
        pending = text;
    }

    // Runs each frame while closed: opens a pending message once, when the map is free.
    internal void Update()
    {
        if (pending == null || popup == null || viewModel.IsOpen || !popup.CanOpen()) return;
        viewModel.SetParagraphs(pending);
        pending = null;
        popup.Open();
    }

    // Exposes read-only popup state for manual verification on the receiving client.
    public string Describe() => $"Open: {viewModel.IsOpen}\n" +
        $"Pending: {(pending == null ? "none" : $"{pending.Length} paragraph(s)")}\n" +
        $"Shown: {viewModel.Paragraphs.Count} paragraph(s)" +
        string.Concat(viewModel.Paragraphs.Select((paragraph, index) => $"\n{index + 1}: {paragraph.Text}"));

    // Removes the global layer when the co-op session ends.
    public void Dispose()
    {
        popup?.Dispose();
        popup = null;
        pending = null;
        viewModel.OnFinalize();
    }
}
