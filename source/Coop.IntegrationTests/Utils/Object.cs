using System.Runtime.Serialization;

namespace Coop.IntegrationTests.Utils;

internal class Object
{
    public static T CreateUninitialized<T>()
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
