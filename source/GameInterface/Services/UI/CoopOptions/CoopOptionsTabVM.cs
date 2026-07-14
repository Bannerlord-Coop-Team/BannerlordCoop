using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.TacticalSymbolsTab;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions;

public class CoopOptionsTabVM : ViewModel
{
    private readonly Action<CoopOptionsTabVM> onSelect;

    private bool isSelected;

    public CoopOptionsTabVM(
        string id,
        string name,
        IEnumerable<CoopOptionsSectionVM> sections,
        Action<CoopOptionsTabVM> onSelect,
        bool persistsOptions = true)
    {
        Id = id;
        Name = name;
        this.onSelect = onSelect;
        PersistsOptions = persistsOptions;

        Sections = new MBBindingList<CoopOptionsSectionVM>();
        foreach (var section in sections)
        {
            Sections.Add(section);
        }
    }

    public string Id { get; }

    [DataSourceProperty]
    public string Name { get; }

    [DataSourceProperty]
    public MBBindingList<CoopOptionsSectionVM> Sections { get; }

    public bool PersistsOptions { get; }

    [DataSourceProperty]
    public bool CanApply
    {
        get
        {
            foreach (var section in Sections)
            {
                if (section.CanApply) return true;
            }

            return false;
        }
    }

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

    [DataSourceProperty]
    public bool IsKillFeedTab => Id == KillFeedOptionsTabProvider.TabId;

    [DataSourceProperty]
    public bool IsTacticalSymbolsTab => Id == TacticalSymbolsOptionsTabProvider.TabId;

    public void ExecuteSelection()
    {
        onSelect(this);
    }

    public void Apply(CoopOptionsData options)
    {
        foreach (var section in Sections)
        {
            section.Apply(Id, options);
        }
    }

    public void AfterApply()
    {
        foreach (var section in Sections)
        {
            section.AfterApply();
        }
    }
}
