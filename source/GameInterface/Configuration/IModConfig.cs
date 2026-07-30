namespace GameInterface.Configuration;

/// <summary>
/// The loaded mod-config.json for this session container. The read is lazy and
/// cached, and a new container re-reads the file — so edits apply to the next
/// hosted session without restarting the game.
/// </summary>
public interface IModConfig
{
    ModConfigData Data { get; }
}
