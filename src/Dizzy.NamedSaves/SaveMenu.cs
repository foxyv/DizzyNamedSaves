using UnityEngine;

namespace Dizzy.NamedSaves
{
    // The "Save Menu" button and window, drawn over the title screen's slot menu.
    internal sealed class SaveMenu
    {
        private const float WindowWidth = 560f;
        private const float ButtonWidth = 150f;
        private const float NameWidth = 230f;
        private const float DateWidth = 130f;
        private const float RowBadgeSize = 22f;
        private const float PanelBadgeSize = 96f;

        // The default skin multiplies its dark button texture by this, so it has to be bright.
        private static readonly Color ButtonGreen = new Color(0.2f, 1f, 0.3f);

        private static Rect _buttonRect;
        private static Rect _windowRect;
        private static Rect _badgeWindowRect;
        private static bool _badgeWindowOpen;
        private static bool _open;
        private static int _lastDrawnFrame = -10;
        private static float _scale = 1f;

        private readonly string[] _slotEdits = new string[SaveStore.SlotCount];
        private readonly string[] _descriptionEdits = new string[SaveStore.SlotCount];

        private bool _placed;
        private int _selectedSlot;
        private Vector2 _scroll;
        private string _status = string.Empty;
        private readonly BadgeEditor _badgeEditor = new BadgeEditor();

        internal static bool MouseOverMenu()
        {
            if (Time.frameCount - _lastDrawnFrame > 1)
                return false;

            Vector3 mouse = Input.mousePosition;
            var point = new Vector2(mouse.x / _scale, (Screen.height - mouse.y) / _scale);
            return _buttonRect.Contains(point)
                || (_open && _windowRect.Contains(point))
                || (_open && _badgeWindowOpen && _badgeWindowRect.Contains(point));
        }

        internal void OnGUI()
        {
            if (!NamedSavesConfig.Enabled.Value || !SlotButtons.SlotMenuOpen())
                return;

            _lastDrawnFrame = Time.frameCount;
            SlotButtons.ApplyBadgesIfDirty();
            // IMGUI is laid out for 1080p; scale it up on larger screens.
            _scale = Mathf.Max(1f, Screen.height / 1080f);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));

            float width = Screen.width / _scale;
            _buttonRect = new Rect(width - ButtonWidth - 16f, 16f, ButtonWidth, 32f);
            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = ButtonGreen;
            bool clicked = GUI.Button(_buttonRect, _open ? "Close Save Menu" : "Save Menu");
            GUI.backgroundColor = oldBackground;
            if (clicked)
                SetOpen(!_open);

            if (_open)
            {
                if (!_placed)
                {
                    _windowRect = new Rect(width - WindowWidth - 16f, 60f, WindowWidth, 100f);
                    _placed = true;
                }

                _windowRect = GUILayout.Window(
                    Plugin.PluginGuid.GetHashCode(),
                    _windowRect,
                    DrawWindow,
                    "Save Menu",
                    GUILayout.Width(WindowWidth));

                _badgeWindowOpen = _badgeEditor.IsOpen;
                if (_badgeWindowOpen)
                {
                    _badgeWindowRect = GUILayout.Window(
                        Plugin.PluginGuid.GetHashCode() + 1,
                        _badgeWindowRect,
                        DrawBadgeWindow,
                        "Badge for slot " + _badgeEditor.Slot,
                        GUILayout.Width(BadgeEditor.WindowWidth));
                }
            }

            GUI.matrix = oldMatrix;
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (!open)
            {
                _badgeEditor.Close();
                return;
            }

            SaveStore.Load();
            SlotButtons.SyncGame(false);
            ReadSlotNames();
            _status = "Pick a slot, then swap a stored save into it.";
        }

        private void ReadSlotNames()
        {
            for (int i = 0; i < SaveStore.SlotCount; i++)
            {
                _slotEdits[i] = SaveStore.GetSlotName(i);
                _descriptionEdits[i] = SaveStore.GetSlotDescription(i);
            }
        }

        private void DrawWindow(int windowId)
        {
            // Files move after the lists are drawn, so a click never changes a list mid-draw.
            int storeSlot = -1;
            StoredSave swapIn = null;

            GUILayout.Label(_status);

            GUILayout.Label("Save slots");
            for (int slot = 0; slot < SaveStore.SlotCount; slot++)
            {
                bool hasSave = SaveStore.SlotHasSave(slot);

                GUILayout.BeginHorizontal(GUI.skin.box);
                if (GUILayout.Toggle(_selectedSlot == slot, "Slot " + slot, GUI.skin.button, GUILayout.Width(64f)))
                    _selectedSlot = slot;

                if (hasSave)
                {
                    DrawRowBadge(SaveStore.SlotBadgePath(slot));
                    string edited = GUILayout.TextField(_slotEdits[slot] ?? string.Empty, SaveStore.MaxNameLength, GUILayout.Width(NameWidth));
                    if (edited != _slotEdits[slot])
                    {
                        _slotEdits[slot] = edited;
                        SaveStore.SetSlotName(slot, edited);
                        SlotButtons.RefreshLabel(slot);
                    }

                    GUILayout.Label(SaveStore.SlotLastPlayed(slot).ToString("g"), GUILayout.Width(DateWidth));
                    if (GUILayout.Button("Store"))
                        storeSlot = slot;
                }
                else
                {
                    GUILayout.Label("(empty)");
                }
                GUILayout.EndHorizontal();
            }

            DrawSlotInformation(_selectedSlot);

            GUILayout.Space(6f);
            GUILayout.Label("Stored saves");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(180f));
            if (SaveStore.Stored.Count == 0)
                GUILayout.Label("Nothing stored yet. \"Store\" moves a slot's save here and frees the slot.");

            string swapLabel = SaveStore.SlotHasSave(_selectedSlot)
                ? "Swap with slot " + _selectedSlot
                : "Move to slot " + _selectedSlot;
            for (int i = 0; i < SaveStore.Stored.Count; i++)
            {
                StoredSave save = SaveStore.Stored[i];

                GUILayout.BeginHorizontal(GUI.skin.box);
                DrawRowBadge(save.BadgePath);
                string edited = GUILayout.TextField(save.Name, SaveStore.MaxNameLength, GUILayout.Width(NameWidth));
                if (edited != save.Name)
                    SaveStore.RenameStored(save, edited);

                GUILayout.Label(save.LastPlayed.ToString("g"), GUILayout.Width(DateWidth));
                if (GUILayout.Button(swapLabel))
                    swapIn = save;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(4f);
            if (GUILayout.Button("Close", GUILayout.Height(26f)))
                SetOpen(false);

            GUI.DragWindow();

            if (storeSlot >= 0)
            {
                SaveStore.StoreSlot(storeSlot, out _status);
                AfterMove();
            }
            else if (swapIn != null)
            {
                SaveStore.SwapIntoSlot(swapIn, _selectedSlot, out _status);
                AfterMove();
            }
        }

        private void DrawSlotInformation(int slot)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Slot Information Panel");
            GUILayout.BeginVertical(GUI.skin.box);
            if (!SaveStore.SlotHasSave(slot))
            {
                GUILayout.Label("Slot " + slot + " is empty.");
                GUILayout.EndVertical();
                return;
            }

            GUILayout.BeginHorizontal();
            Rect badgeRect = GUILayoutUtility.GetRect(PanelBadgeSize, PanelBadgeSize, GUILayout.Width(PanelBadgeSize), GUILayout.Height(PanelBadgeSize));
            Texture2D badge = Badges.Get(SaveStore.SlotBadgePath(slot));
            if (badge != null)
                GUI.DrawTexture(badgeRect, badge, ScaleMode.ScaleToFit);
            else
                GUI.Box(badgeRect, "No badge");

            GUILayout.BeginVertical();
            string name = SaveStore.GetSlotName(slot);
            GUILayout.Label("Slot " + slot + (name.Length > 0 ? ": " + name : " (unnamed)"));
            GUILayout.Label("Last played: " + SaveStore.SlotLastPlayed(slot).ToString("f"));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(badge != null ? "Change badge..." : "Set badge...", GUILayout.Width(130f)))
                OpenBadgeEditor(slot);
            GUI.enabled = badge != null;
            if (GUILayout.Button("Remove badge", GUILayout.Width(110f)))
            {
                SaveStore.RemoveSlotBadge(slot);
                Badges.Clear();
                SlotButtons.MarkBadgesDirty();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Label("Description");
            string edited = GUILayout.TextArea(_descriptionEdits[slot] ?? string.Empty, SaveStore.MaxDescriptionLength, GUILayout.Height(84f));
            if (edited != _descriptionEdits[slot])
            {
                _descriptionEdits[slot] = edited;
                SaveStore.SetSlotDescription(slot, edited);
            }
            GUILayout.EndVertical();
        }

        private static void DrawRowBadge(string path)
        {
            Rect rect = GUILayoutUtility.GetRect(RowBadgeSize, RowBadgeSize, GUILayout.Width(RowBadgeSize), GUILayout.Height(RowBadgeSize));
            Texture2D badge = Badges.Get(path);
            if (badge != null)
                GUI.DrawTexture(rect, badge, ScaleMode.ScaleToFit);
        }

        private void OpenBadgeEditor(int slot)
        {
            _badgeWindowRect = new Rect(Mathf.Max(16f, _windowRect.x - BadgeEditor.WindowWidth - 12f), _windowRect.y, BadgeEditor.WindowWidth, 100f);
            _badgeEditor.Open(slot);
        }

        private void DrawBadgeWindow(int windowId)
        {
            _badgeEditor.Draw();
            GUI.DragWindow();
        }

        private void AfterMove()
        {
            _badgeEditor.Close();
            Badges.Clear();
            SlotButtons.SyncGame(true);
            ReadSlotNames();
            GUIUtility.keyboardControl = 0;
        }
    }
}
