using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Heroes;

internal interface ICultureObjectProvider
{
    IEnumerable<CultureObject> GetAll();
}

/// <inheritdoc cref="ICultureObjectProvider"/>
internal class CultureObjectProvider : ICultureObjectProvider
{
    public IEnumerable<CultureObject> GetAll()
    {
        var objectManager = MBObjectManager.Instance;
        if (objectManager == null) return Array.Empty<CultureObject>();

        return objectManager.GetObjectTypeList<CultureObject>();
    }
}
