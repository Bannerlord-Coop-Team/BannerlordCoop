namespace Sync.Behaviour
{
    public enum ETriggerOrigin
    {
        /// <summary>
        /// The call originated from a direct call to a function, property or field. This is to be interpreted as a
        /// regular call from a caller that is not aware of any patches.
        ///
        /// In case of fields, this is emulated through <see cref="FieldChangeBuffer"/>. 
        /// </summary>
        Local,
        
        /// <summary>
        /// The call is an authoritative action such as the server sending a state update. This should be applied
        /// without modification.
        /// </summary>
        Authoritative
    }
}