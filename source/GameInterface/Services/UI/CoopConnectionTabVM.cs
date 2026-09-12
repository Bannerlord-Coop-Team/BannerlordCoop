using System;
using TaleWorlds.Library;

namespace GameInterface.Services.UI;

/// <summary>
/// A selectable entry in the Join Co-op screen's left-side menu.
/// </summary>
public sealed class CoopConnectionTabVM : ViewModel
{
    private readonly Action<CoopConnectionTabVM> onSelect;
    private bool isSelected;

    public CoopConnectionTabVM(string id, string name, Action<CoopConnectionTabVM> onSelect)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        this.onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
    }

    public string Id { get; }

    [DataSourceProperty]
    public string Name { get; }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;

            isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public void ExecuteSelection()
    {
        onSelect(this);
    }
}
