namespace Sync.Behaviour
{
    /// <summary>
    ///     Defines the origin of an action, i.e. a method call, property getter / setter call or a field change.
    /// </summary>
    public enum EActionOrigin
    {
        /// <summary>
        /// The action originated from a direct call to a method, property or field. This is to be interpreted as a
        /// call from regular game logic that is not aware of any coop patches.
        /// </summary>
        Local,
        
        /// <summary>
        /// The call is an authoritative action such as the server sending a state update. This is intended to be
        /// applied immediately and without modification.
        /// </summary>
        Authoritative
    }
}