using System.Runtime.Serialization;

namespace Common.Util;

/// <summary>
/// Helper class for creating and manipulating objects
/// </summary>
public class ObjectHelper
{
    /// <summary>
    /// Creates an object skipping the constructor
    /// </summary>
    /// <typeparam name="T">Type of object to create</typeparam>
    /// <returns>New instance of <typeparamref name="T"/></returns>
    public static T SkipConstructor<T>()
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }
}
