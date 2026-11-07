namespace GameInterface.Utils;

/// <summary>
/// FNV-1a string hash that is identical on every machine used for deriving identical RNG seeds.
/// </summary>
internal static class StableHash
{
    // string.GetHashCode is randomized per process, so machines would derive different seeds from it
    public static uint FromString(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var c in value)
        {
            hash = (hash ^ c) * prime;
        }

        return hash;
    }
}
