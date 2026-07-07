namespace Common.Network.Session;

/// <summary>
/// Provides the join info an <see cref="ISessionAdvertiser"/> publishes.
/// </summary>
public interface ISessionJoinInfoSource
{
    SessionJoinInfo Get();
}
