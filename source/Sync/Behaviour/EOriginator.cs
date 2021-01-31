namespace Sync.Behaviour
{
    /// <summary>
    ///     Defines the origin of an action, i.e. who made a method call, property getter / setter call or a field change.
    /// </summary>
    public enum EOriginator
    {
        /// <summary>
        ///     The call was made locally by the regular game loop.
        /// </summary>
        Game,
        
        /// <summary>
        ///     The call was received through the coop framework and is an authoritative action, such as the server
        ///     sending a state update. This is intended to be applied immediately and without modification.
        /// </summary>
        RemoteAuthority
    }
}