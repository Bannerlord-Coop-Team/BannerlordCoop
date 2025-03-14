using System;

namespace GameInterface.DynamicSync
{
    [Flags]
    public enum DynamicMessageType : long
    {
        ValueType = 1,
        ObjectManagerType = 2,
        Field = 4,
        Property = 8,
        Direct = 16,
        ValueField = ValueType | Field | Direct,
        ReferenceField = ObjectManagerType | Field | Direct,
        ValueProperty = ValueType | Property | Direct,
        ReferenceProperty = ObjectManagerType | Property | Direct,
        List = 32,
        ListValueField = ValueType | Field | List,
        ListReferenceField = ObjectManagerType | Field | List,
        ListValueProperty = ValueType | Property | List,
        ListReferenceProperty = ObjectManagerType | Property | List,
        MBList = 64,
        MBListValueField = ValueType | Field | MBList,
        MBListReferenceField = ObjectManagerType | Field | MBList,
        MBListValueProperty = ValueType | Property | MBList,
        MBListReferenceProperty = ObjectManagerType | Property | MBList,
        Queue = 128,
        QueueValueField = ValueType | Field | Queue,
        QueueReferenceField = ObjectManagerType | Field | Queue,
        QueueValueProperty = ValueType | Property | Queue,
        QueueReferenceProperty = ObjectManagerType | Property | Queue,
        Array = 256,
        ArrayValueField = ValueType | Field | Array,
        ArrayReferenceField = ObjectManagerType | Field | Array,
        ArrayValueProperty = ValueType | Property | Array,
        ArrayReferenceProperty = ObjectManagerType | Property | Array,
    }
}