using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Dizzy.NamedSaves
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("Sailwind.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.dizzy.sailwind.namedsaves";
        public const string PluginName = "Named Saves Mod";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static Plugin Instance;

        private Harmony _harmony;
        private SaveMenu _menu;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            NamedSavesConfig.Bind(Config);
            SaveStore.Load();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Plugin).Assembly);

            _menu = new SaveMenu();

            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void OnGUI()
        {
            _menu?.OnGUI();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();

            if (Instance == this)
                Instance = null;
        }
    }
}
