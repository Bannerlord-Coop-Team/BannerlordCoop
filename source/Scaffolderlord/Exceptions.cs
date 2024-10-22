using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scaffolderlord.Exceptions
{
    public class TypeNotFoundException : Exception
    {
        public TypeNotFoundException(string typeName)
            : base($"Type '{typeName}' not found.")
        {
        }
    }

    public class PropertyNotFoundException : Exception
    {
        public PropertyNotFoundException(string propertyName, string? typeName)
            : base($"Property '{propertyName}' not found on type '{typeName}'.")
        {
        }
    }

    public class PropertyWithNoSetterException : Exception
    {
        public PropertyWithNoSetterException(string propertyName, string? typeName)
            : base($"Property '{propertyName}' on type '{typeName}' does not have a setter.")
        {
        }
    }

    public class FieldNotFoundException : Exception
    {
        public FieldNotFoundException(string fieldName, string? typeName)
            : base($"Field '{fieldName}' not found on type '{typeName}'.")
        {
        }
    }


}
