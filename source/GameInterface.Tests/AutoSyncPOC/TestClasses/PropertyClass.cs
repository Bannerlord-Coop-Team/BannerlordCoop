using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.AutoSyncPOC.TestClasses;

internal class PropertyClass
{
    public int MyProperty { get; set; }

    public PropertyClass(int propertyValue)
    {
        MyProperty = propertyValue; 
    }
}
