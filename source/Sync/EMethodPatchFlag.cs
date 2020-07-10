using System;

namespace Sync
{
    [Flags]
    public enum EMethodPatchFlag : byte
    {
        None = 0,
        TransferArgumentsByValue = 1 << 1
    }
}
