using System.Runtime.Serialization;

namespace Coop.IntegrationTests.Utils;

internal class ObjectHelper
{
    public static T SkipConstructor<T>()
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
