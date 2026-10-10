using BepInEx.Configuration;

namespace Dizzy.NamedSaves
{
    internal static class NamedSavesConfig
    {
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ShowNamesOnSlotButtons;
        internal static ConfigEntry<int> SlotButtonNameLength;
        internal static ConfigEntry<bool> ShowBadgesOnSlotButtons;
        internal static ConfigEntry<float> SlotButtonBadgeSize;

        internal static void Bind(ConfigFile config)
        {
            Enabled = config.Bind(
                "General",
                "Enabled",
                true,
                "Show the Save Menu button on the title screen's slot menu. Vanilla has no way to name or store saves.");

            ShowNamesOnSlotButtons = config.Bind(
                "General",
                "ShowNamesOnSlotButtons",
                true,
                "Add each save's name above the time and date on the slot buttons. Vanilla shows only the time and date.");

            SlotButtonNameLength = config.Bind(
                "General",
                "SlotButtonNameLength",
                16,
                new ConfigDescription(
                    "Longest name shown on a slot button before it is cut short. The Save Menu always shows the full name.",
                    new AcceptableValueRange<int>(4, 40)));

            ShowBadgesOnSlotButtons = config.Bind(
                "General",
                "ShowBadgesOnSlotButtons",
                true,
                "Show each save's badge in the top left corner of its slot button. Vanilla slot buttons have no picture.");

            SlotButtonBadgeSize = config.Bind(
                "General",
                "SlotButtonBadgeSize",
                0.4f,
                new ConfigDescription(
                    "Badge size on a slot button, as a fraction of the button's shorter side.",
                    new AcceptableValueRange<float>(0.15f, 0.9f)));

            ShowBadgesOnSlotButtons.SettingChanged += (sender, args) => SlotButtons.MarkBadgesDirty();
            SlotButtonBadgeSize.SettingChanged += (sender, args) => SlotButtons.MarkBadgesDirty();
        }
    }
}
