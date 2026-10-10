using HarmonyLib;

namespace Dizzy.NamedSaves.Patches
{
    [HarmonyPatch(typeof(StartMenuButton), "Awake")]
    internal static class StartMenuButtonAwakePatch
    {
        private static void Prefix(StartMenuButton __instance)
        {
            SlotButtons.Register(__instance);
        }

        private static void Postfix(StartMenuButton __instance)
        {
            if (SlotButtons.TypeOf(__instance) == StartMenuButtonType.Slot
                && __instance.saveSlot >= 0
                && __instance.saveSlot < SaveStore.SlotCount)
                SlotButtons.RefreshLabel(__instance.saveSlot);
        }
    }

    // A click on the Save Menu also lands on whatever 3D button sits behind it.
    [HarmonyPatch(typeof(StartMenu), nameof(StartMenu.ButtonClick), typeof(StartMenuButtonType))]
    internal static class StartMenuButtonClickPatch
    {
        private static bool Prefix()
        {
            return !SaveMenu.MouseOverMenu();
        }
    }

    [HarmonyPatch(typeof(StartMenu), nameof(StartMenu.ButtonClick), typeof(int), typeof(int))]
    internal static class StartMenuSlotClickPatch
    {
        private static bool Prefix()
        {
            return !SaveMenu.MouseOverMenu();
        }
    }
}
