using System;

namespace Sync
{
    /// <summary>
    ///     Flags indicating the desired behaviour of a generated patch for a method.
    /// </summary>
    [Flags]
    public enum EMethodPatchFlag : byte
    {
        None = 0,

        /// <summary>
        ///     Bit 1: Set if the arguments to the method should be preferably sent by value. This
        ///     means it will either be serialized directly into the event or transferred
        ///     using the <see cref="Sync.Store.RemoteStore" />.
        ///     Unset if by-reference should be preferred.
        /// </summary>
        TransferArgumentsByValue = 1 << 1
    }
}
