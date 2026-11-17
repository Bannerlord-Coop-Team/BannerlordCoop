using System;
using System.Collections.Generic;

namespace GameInterface.Configuration;

public static class ManagedConfigOptionValidation
{
    public static void Validate(ModOptionsData options)
    {
        foreach (var property in typeof(ModOptionsData).GetProperties())
        {
            var metadata = (ManagedConfigOptionAttribute)Attribute.GetCustomAttribute(property, typeof(ManagedConfigOptionAttribute));
            var value = property.GetValue(options);
            if (metadata == null || value == null) continue;
            if (!double.IsNaN(metadata.Min) && Convert.ToDouble(value) < metadata.Min)
                throw new ArgumentException($"{property.Name} is below its minimum");
            if (!double.IsNaN(metadata.Max) && Convert.ToDouble(value) > metadata.Max)
                throw new ArgumentException($"{property.Name} is above its maximum");
            if (value is IList<string> list)
            {
                if (metadata.MaxItems >= 0 && list.Count > metadata.MaxItems)
                    throw new ArgumentException($"{property.Name} has too many entries");
                var ids = new HashSet<string>();
                foreach (var item in list)
                {
                    // Blank entries are legacy placeholders ignored by the game.
                    if (!string.IsNullOrEmpty(item) && metadata.ItemPattern != null
                        && !System.Text.RegularExpressions.Regex.IsMatch(item, metadata.ItemPattern))
                        throw new ArgumentException($"{property.Name} contains an invalid entry");
                    if (!string.IsNullOrEmpty(item) && metadata.UniqueItems && !ids.Add(item))
                        throw new ArgumentException($"{property.Name} contains a duplicate");
                }
            }
            if (metadata.ForbiddenRanges == "1-9" && Convert.ToDouble(value) is >= 1 and <= 9)
                throw new ArgumentException($"{property.Name} is in a forbidden range");
        }
    }
}
