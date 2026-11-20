using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Data;

/// <summary>
/// One slot a closed screen changed, with what the screen saw there and what the server has there now.
/// The screen's changes are only valid together, so they are accepted or refused as a group.
/// </summary>
internal class EquipmentSlotChange
{
    private readonly EquipmentElement[] slots;
    private readonly int index;
    private readonly EquipmentElement live;

    public EquipmentElement Initial { get; }
    public EquipmentElement Submitted { get; }

    public EquipmentSlotChange(EquipmentElement[] slots, int index, EquipmentElement initial, EquipmentElement live)
    {
        this.slots = slots;
        this.index = index;
        this.live = live;
        Initial = initial;
        Submitted = slots[index];
    }

    /// <summary>Gets whether the slot no longer holds what the screen saw, which voids the roster move the screen made for it.</summary>
    public bool IsConflict => !Initial.IsEqualTo(live);

    /// <summary>Leaves the slot as the server has it.</summary>
    public void Refuse() => slots[index] = live;
}
