using Game;
using Game.Modding;

namespace CS2DataExport
{
    public sealed partial class Mod : IMod
    {
        private static Mod? s_instance;

        internal static Mod? TryGetInstance()
        {
            return s_instance;
        }

        void IMod.OnLoad(UpdateSystem updateSystem)
        {
            s_instance = this;
            updateSystem.UpdateAt<CS2DataExportRuntimeSystem>(SystemUpdatePhase.GameSimulation);
            OnLoad();
        }

        void IMod.OnDispose()
        {
            // Tear down world-bound state but keep s_instance. CS2 can call OnDispose
            // when leaving a city; GameSimulation systems still run after the next load
            // and must be able to re-EnsureInitialized without a second IMod.OnLoad.
            OnDispose();
        }
    }
}
