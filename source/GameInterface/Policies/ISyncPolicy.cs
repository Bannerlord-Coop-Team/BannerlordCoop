namespace GameInterface.Policies;

/// <summary>
/// Policy that allows the original calls of patches or to enforce syncing
/// </summary>
/// <remarks>
/// This pattern is needed to allow the client to call things such as
/// setting the name of the clan without syncing while in character creation
/// </remarks>
public interface ISyncPolicy
{
    bool AllowOriginal();
}
