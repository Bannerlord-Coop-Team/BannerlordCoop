#if DEBUG
using Missions.Messages;
using TaleWorlds.Library;

namespace Missions.Battles;

// Separate optional seam keeps the other lab modes and their existing adapters unchanged.
public interface INavalLabShipAdapter
{
    NetworkNavalLabShipSample CaptureOwnedShip(long sequence, long callback);
    bool ValidateForeignShip(NetworkNavalLabShipSample sample);
    void AcceptForeignShip(NetworkNavalLabShipSample sample);
    bool ApplyForeignShipFrame(int slot, MatrixFrame frame);
    object InspectShipAuthority();
}
#endif
