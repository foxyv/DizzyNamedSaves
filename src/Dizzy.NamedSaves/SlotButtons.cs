using System;
using System.IO;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.NamedSaves
{
    // The title screen's slot buttons, and the game state that has to follow the files
    // when a save moves in or out of a slot.
    internal static class SlotButtons
    {
        private static readonly AccessTools.FieldRef<StartMenuButton, StartMenuButtonType> TypeRef =
            AccessTools.FieldRefAccess<StartMenuButton, StartMenuButtonType>("type");
        private static readonly AccessTools.FieldRef<StartMenuButton, StartMenu> MenuRef =
            AccessTools.FieldRefAccess<StartMenuButton, StartMenu>("menu");
        private static readonly AccessTools.FieldRef<StartMenu, GameObject> SaveSlotUiRef =
            AccessTools.FieldRefAccess<StartMenu, GameObject>("saveSlotUI");

        private static readonly StartMenuButton[] Buttons = new StartMenuButton[SaveStore.SlotCount];
        private static readonly string[] EmptyText = new string[SaveStore.SlotCount];

        // Vanilla look of each slot button, captured before NANDTweaks puts a thumbnail on it.
        private static readonly Material[] VanillaMaterial = new Material[SaveStore.SlotCount];
        private static readonly Color[] VanillaTextColor = new Color[SaveStore.SlotCount];
        private static readonly int[] VanillaFontSize = new int[SaveStore.SlotCount];
        private static readonly Vector3[] VanillaTextPosition = new Vector3[SaveStore.SlotCount];
        private static readonly Material[] OwnMaterial = new Material[SaveStore.SlotCount];

        private static readonly GameObject[] BadgeObjects = new GameObject[SaveStore.SlotCount];
        private static bool _badgesDirty = true;
        private static bool _badgeWarned;

        private static StartMenu _menu;
        private static GameObject _saveSlotUi;
        private static GameObject _continueButton;

        internal static StartMenuButtonType TypeOf(StartMenuButton button)
        {
            return TypeRef(button);
        }

        // Called before vanilla Awake overwrites the label, so the empty-slot text is still there.
        internal static void Register(StartMenuButton button)
        {
            StartMenuButtonType type = TypeRef(button);
            if (type == StartMenuButtonType.Continue)
            {
                _continueButton = button.transform.parent.gameObject;
                return;
            }

            if (type != StartMenuButtonType.Slot || button.saveSlot < 0 || button.saveSlot >= SaveStore.SlotCount)
                return;

            Buttons[button.saveSlot] = button;
            TextMesh label = button.transform.parent.GetComponentInChildren<TextMesh>(true);
            EmptyText[button.saveSlot] = label != null ? label.text : string.Empty;

            MeshRenderer renderer = button.GetComponent<MeshRenderer>();
            VanillaMaterial[button.saveSlot] = renderer != null ? renderer.sharedMaterial : null;
            if (label != null)
            {
                VanillaTextColor[button.saveSlot] = label.color;
                VanillaFontSize[button.saveSlot] = label.fontSize;
                VanillaTextPosition[button.saveSlot] = label.transform.localPosition;
            }

            if (_menu == null)
            {
                _menu = MenuRef(button);
                _saveSlotUi = _menu != null ? SaveSlotUiRef(_menu) : null;
            }
        }

        internal static bool SlotMenuOpen()
        {
            return _saveSlotUi != null
                && _saveSlotUi.activeInHierarchy
                && !GameState.playing
                && !GameState.currentlyLoading;
        }

        internal static void RefreshLabel(int slot)
        {
            StartMenuButton button = Buttons[slot];
            if (button == null)
                return;

            if (!SaveStore.SlotHasSave(slot))
            {
                button.SetButtonText(EmptyText[slot] ?? string.Empty);
                return;
            }

            DateTime played = SaveStore.SlotLastPlayed(slot);
            string text = played.ToShortTimeString() + "\n" + played.ToShortDateString();

            string name = SaveStore.GetSlotName(slot);
            if (name.Length > 0 && NamedSavesConfig.ShowNamesOnSlotButtons.Value)
            {
                int max = NamedSavesConfig.SlotButtonNameLength.Value;
                if (name.Length > max)
                    name = name.Substring(0, max - 2).TrimEnd() + "..";
                text = name + "\n" + text;
            }

            button.SetButtonText(text);
        }

        // After saves move: vanilla only counts slots once, when the title screen loads.
        internal static void SyncGame(bool savesMoved)
        {
            if (SaveSlots.slotsActive != null)
            {
                int count = 0;
                for (int i = 0; i < SaveStore.SlotCount && i < SaveSlots.slotsActive.Length; i++)
                {
                    SaveSlots.slotsActive[i] = SaveStore.SlotHasSave(i);
                    if (SaveSlots.slotsActive[i])
                        count++;
                }
                SaveSlots.activeSlotsCount = count;

                if (_continueButton != null)
                    _continueButton.SetActive(count > 0);
            }

            // The backup list is cached per slot; hiding it makes vanilla rebuild it.
            if (_menu != null && _menu.backupSavesUI != null)
                _menu.backupSavesUI.HideList();

            for (int i = 0; i < SaveStore.SlotCount; i++)
                RefreshLabel(i);

            if (savesMoved)
                _badgesDirty = true;

            if (savesMoved && NandThumbnailsOn())
            {
                for (int i = 0; i < SaveStore.SlotCount; i++)
                    RefreshThumbnail(i);
            }
        }

        // NANDTweaks writes slotN.save.png on every save and puts it on the slot button,
        // but only once, when the title screen loads.
        private static bool NandThumbnailsOn()
        {
            BepInEx.PluginInfo nand;
            if (!Chainloader.PluginInfos.TryGetValue("com.nandbrew.nandtweaks", out nand) || nand.Instance == null)
                return false;

            ConfigEntry<bool> enabled;
            if (nand.Instance.Config.TryGetEntry("---- Save Thumbnails ----", "Save and load thumbnails", out enabled))
                return enabled.Value;
            return true;
        }

        // Same look NANDTweaks gives a slot with a thumbnail; vanilla look for one without.
        private static void RefreshThumbnail(int slot)
        {
            StartMenuButton button = Buttons[slot];
            if (button == null)
                return;

            MeshRenderer renderer = button.GetComponent<MeshRenderer>();
            TextMesh label = button.transform.parent.GetComponentInChildren<TextMesh>(true);
            if (renderer == null || label == null)
                return;

            Material previous = OwnMaterial[slot];
            OwnMaterial[slot] = null;

            Texture2D texture = LoadThumbnail(slot);
            Shader shader = texture != null ? Shader.Find("UI/Default") : null;
            if (shader != null)
            {
                float aspect = (float)texture.height / texture.width;
                var material = new Material(shader)
                {
                    mainTexture = texture,
                    mainTextureScale = new Vector2(aspect, 1f),
                    mainTextureOffset = new Vector2((1f - aspect) / 2f, 0f)
                };
                renderer.material = material;
                OwnMaterial[slot] = material;

                label.color = new Color(1f, 0.8f, 0.6f);
                label.fontSize = 45;
                label.transform.localPosition = new Vector3(0f, -0.1f, 0.04f);
            }
            else
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
                if (VanillaMaterial[slot] != null)
                    renderer.sharedMaterial = VanillaMaterial[slot];

                label.color = VanillaTextColor[slot];
                label.fontSize = VanillaFontSize[slot];
                label.transform.localPosition = VanillaTextPosition[slot];
            }

            if (previous != null)
            {
                UnityEngine.Object.Destroy(previous.mainTexture);
                UnityEngine.Object.Destroy(previous);
            }
        }

        internal static void MarkBadgesDirty()
        {
            _badgesDirty = true;
        }

        // Runs while the slot menu is on screen, once the buttons and labels are in place.
        internal static void ApplyBadgesIfDirty()
        {
            if (!_badgesDirty)
                return;

            _badgesDirty = false;
            for (int i = 0; i < SaveStore.SlotCount; i++)
            {
                try
                {
                    RefreshBadge(i);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError("Could not put the badge on slot button " + i + ": " + ex);
                }
            }
        }

        private static void RefreshBadge(int slot)
        {
            if (BadgeObjects[slot] != null)
            {
                MeshRenderer old = BadgeObjects[slot].GetComponent<MeshRenderer>();
                if (old != null && old.sharedMaterial != null)
                {
                    UnityEngine.Object.Destroy(old.sharedMaterial.mainTexture);
                    UnityEngine.Object.Destroy(old.sharedMaterial);
                }
                UnityEngine.Object.Destroy(BadgeObjects[slot]);
                BadgeObjects[slot] = null;
            }

            StartMenuButton button = Buttons[slot];
            if (button == null || !NamedSavesConfig.ShowBadgesOnSlotButtons.Value || !SaveStore.SlotHasSave(slot))
                return;

            MeshFilter filter = button.GetComponent<MeshFilter>();
            Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            if (filter == null || filter.sharedMesh == null || shader == null)
            {
                if (!_badgeWarned)
                    Plugin.Log.LogWarning("Slot buttons have no mesh or shader to hang a badge on; badges stay in the Save Menu only.");
                _badgeWarned = true;
                return;
            }

            Texture2D texture = Badges.LoadImage(SaveStore.SlotBadgePath(slot));
            if (texture == null)
                return;

            // The label is known to face the player: its right and up are the button's,
            // and it is read from its back (-forward) side. No camera needed.
            TextMesh label = button.transform.parent.GetComponentInChildren<TextMesh>(true);
            if (label == null)
            {
                UnityEngine.Object.Destroy(texture);
                return;
            }

            Transform t = button.transform;
            Vector3 r = label.transform.right;
            Vector3 u = label.transform.up;
            Vector3 outward = -label.transform.forward;

            // Half size of the button along each of those directions.
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 centre = t.TransformPoint(bounds.center);
            float halfWidth = 0f, halfHeight = 0f, halfDepth = 0f;
            for (int i = 0; i < 3; i++)
            {
                Vector3 axis = t.TransformVector(Axis(i) * bounds.extents[i]);
                halfWidth += Mathf.Abs(Vector3.Dot(axis, r));
                halfHeight += Mathf.Abs(Vector3.Dot(axis, u));
                halfDepth += Mathf.Abs(Vector3.Dot(axis, outward));
            }

            float shortSide = 2f * Mathf.Min(halfWidth, halfHeight);
            float half = shortSide * NamedSavesConfig.SlotButtonBadgeSize.Value * 0.5f;
            float pad = shortSide * 0.05f;

            // Top left corner, in front of both the button face and the label.
            float labelDepth = Vector3.Dot(label.transform.position - centre, outward);
            float depth = Mathf.Max(halfDepth, labelDepth) + shortSide * 0.02f;
            Vector3 middle = centre
                + r * (-halfWidth + pad + half)
                + u * (halfHeight - pad - half)
                + outward * depth;

            Plugin.Log.LogInfo("Badge on slot " + slot + ": button " + (2f * halfWidth).ToString("0.###") + " x " + (2f * halfHeight).ToString("0.###")
                + ", depth " + halfDepth.ToString("0.###") + ", label depth " + labelDepth.ToString("0.###")
                + ", badge side " + (2f * half).ToString("0.###") + ", layer " + button.gameObject.layer + ", shader " + shader.name + ".");

            var badge = new GameObject("NamedSaves badge");
            badge.layer = button.gameObject.layer;
            badge.transform.SetParent(t, false);

            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                t.InverseTransformPoint(middle - r * half - u * half),
                t.InverseTransformPoint(middle + r * half - u * half),
                t.InverseTransformPoint(middle - r * half + u * half),
                t.InverseTransformPoint(middle + r * half + u * half)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            // Both windings, so it shows even under a shader that culls back faces.
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1, 0, 1, 2, 2, 1, 3 };
            mesh.RecalculateBounds();

            badge.AddComponent<MeshFilter>().sharedMesh = mesh;
            badge.AddComponent<MeshRenderer>().sharedMaterial = new Material(shader) { mainTexture = texture };
            BadgeObjects[slot] = badge;
        }

        private static Vector3 Axis(int index)
        {
            return index == 0 ? Vector3.right : (index == 1 ? Vector3.up : Vector3.forward);
        }

        private static Texture2D LoadThumbnail(int slot)
        {
            if (!SaveStore.SlotHasSave(slot))
                return null;

            string path = SaveSlots.GetSlotSavePath(slot) + ".png";
            try
            {
                if (!File.Exists(path))
                    return null;

                var texture = new Texture2D(1, 1);
                if (texture.LoadImage(File.ReadAllBytes(path)))
                    return texture;

                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not load the thumbnail for slot " + slot + ": " + ex);
            }
            return null;
        }
    }
}
