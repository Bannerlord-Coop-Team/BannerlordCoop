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

    public FieldClass(int fieldValue, int? nullableField = null)
    {
        MyField = fieldValue;
        MyNullableField = nullableField;
    }

    public int GetField() => MyField;
    public int? GetNullableField() => MyNullableField;
}
