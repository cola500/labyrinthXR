// How every PlayMode test loads the labyrinth: synchronously (never LoadSceneAsync; see Lilla fruktståndet
// docs/TESTING.md), with script-driven physics so tests step the simulation deterministically via LabyrinthGame.Advance.
using System.Collections;
using Labyrinth.Game;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Labyrinth.Tests
{
    public static class SceneLoad
    {
        public const string Scene = "Labyrinth";

        public static IEnumerator Fresh(System.Action<LabyrinthGame> done)
        {
            yield return null; // let deferred Destroy calls of the last test finish first
            Physics.simulationMode = SimulationMode.FixedUpdate;
            SceneManager.LoadScene(Scene); // synchronous: takes effect at the start of the next frame
            yield return null;
            Physics.simulationMode = SimulationMode.Script;
            var g = Object.FindAnyObjectByType<LabyrinthGame>();
            g.Restart();
            done(g);
        }

        public static void Restore() => Physics.simulationMode = SimulationMode.FixedUpdate;
    }
}
