using Mono.Cecil.Cil;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace Missions.Services.Arena
{
    /// <summary>
    /// Manages the spawns of players inside the arena.
    /// </summary>
    interface IArenaSpawnManager
    {
        /// <summary>
        /// Gets a random spawn location from a scene.
        /// </summary>
        /// <returns></returns>
        MatrixFrame GetRandomSpawnLocation(); 
    }

    /// <inheritdoc />
    internal sealed class ArenaSpawnManager : IArenaSpawnManager
    {
        private readonly Scene _scene;

        private IReadOnlyList<string> _sceneSpawnPoints = new List<string>();

        private IList<string> _selectedSpawnPoints = new List<string>();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scene"></param>
        public ArenaSpawnManager(Scene scene)
        {
            _scene = scene;
        }

        /// <inheritdoc />
        public MatrixFrame GetRandomSpawnLocation()
        {
            if (_sceneSpawnPoints.IsEmpty())
            {
                // TODO: get rid of this hard reference here
                _sceneSpawnPoints = ArenaTestGameManager.GetAllSpawnPoints(_scene);
            }

            if (_sceneSpawnPoints.Count == _selectedSpawnPoints.Count) return MatrixFrame.Identity;

            string spawnPoint = string.Empty;

            do
            {
                spawnPoint = _sceneSpawnPoints.GetRandomElement();
            } while (_selectedSpawnPoints.Contains(spawnPoint));

            _selectedSpawnPoints.Add(spawnPoint);
            return _scene.FindEntitiesWithTag(spawnPoint).First().GetGlobalFrame();
        }
    }
}
