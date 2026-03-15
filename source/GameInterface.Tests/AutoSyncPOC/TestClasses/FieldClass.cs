using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.AutoSyncPOC.TestClasses;

internal class FieldClass
{
    private int MyField;
    private int? MyNullableField;
    private RefClass? MyRefField;

    public FieldClass(int fieldValue, int? nullableField = null, RefClass? refField = null)
    {
        MyField = fieldValue;
        MyNullableField = nullableField;
        MyRefField = refField;
    }

    public int GetField() => MyField;
    public int? GetNullableField() => MyNullableField;
    public RefClass? GetRefField() => MyRefField;
}
