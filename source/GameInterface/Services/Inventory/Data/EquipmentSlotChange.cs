using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Data;

/// <summary>
/// One slot a closed screen changed, decided together with the roster move the screen made for it.
/// The screen moved the item it saw in the slot, so the roster is corrected for what the server really has there.
/// </summary>
internal class EquipmentSlotChange
{
    private readonly EquipmentElement[] slots;
    private readonly int index;
    private readonly EquipmentElement initial;
    private readonly EquipmentElement live;
    private bool isRejected;

    public EquipmentElement Submitted { get; }

    public EquipmentSlotChange(EquipmentElement[] slots, int index, EquipmentElement initial, EquipmentElement live)
    {
        this.slots = slots;
        this.index = index;
        this.initial = initial;
        this.live = live;
        Submitted = slots[index];
    }

    /// <summary>Gets the items whose roster count this change can correct.</summary>
    public EquipmentElement[] Elements => new[] { initial, live, Submitted };

    /// <summary>Leaves the slot as the server has it, because the roster can't supply the submitted item.</summary>
    public void Reject()
    {
        isRejected = true;
        slots[index] = live;
    }

    /// <summary>
    /// Gets the roster change the screen's own move missed. The item leaving the slot is the live one, not the one
    /// the screen saw, and a rejected change leaves the slot alone, so its submitted item never left the roster.
    /// </summary>
    public int RosterCorrection(EquipmentElement element)
    {
        var returned = isRejected ? Submitted : live;
        return (returned.IsEqualTo(element) ? 1 : 0) - (initial.IsEqualTo(element) ? 1 : 0);
    }
}
