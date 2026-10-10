using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Dizzy.NamedSaves
{
    // Loads a picture, lets the player pick a square of it, and saves that square as the
    // slot's badge.
    internal sealed class BadgeEditor
    {
        internal const float WindowWidth = 360f;

        private const float PreviewSize = 330f;
        private const int BadgeSize = 256;
        private const float MinCrop = 0.05f;

        private string _path = string.Empty;
        private string _status = string.Empty;
        private Texture2D _source;
        private bool _showCrop;

        // Crop square: side as a fraction of the picture's short edge, and where it sits
        // in the room left over (0 = left/top, 1 = right/bottom).
        private float _cropSize = 1f;
        private float _cropX = 0.5f;
        private float _cropY = 0.5f;

        internal bool IsOpen { get; private set; }
        internal int Slot { get; private set; }

        internal void Open(int slot)
        {
            Close();
            Slot = slot;
            IsOpen = true;
            _status = "Browse for a PNG or JPG, or paste its path and press Load.";
        }

        internal void Close()
        {
            IsOpen = false;
            if (_source != null)
            {
                UnityEngine.Object.Destroy(_source);
                _source = null;
            }
        }

        internal void Draw()
        {
            GUILayout.Label(_status);

            GUILayout.BeginHorizontal();
            _path = GUILayout.TextField(_path, GUILayout.Width(WindowWidth - 150f));
            if (GUILayout.Button("Browse...", GUILayout.Width(70f)))
            {
                string picked = FileDialog.PickImage();
                if (picked != null)
                {
                    _path = picked;
                    Load();
                }
            }
            if (GUILayout.Button("Load", GUILayout.Width(50f)))
                Load();
            GUILayout.EndHorizontal();

            // A picture loaded by a click must wait for the next layout pass before it adds controls.
            if (Event.current.type == EventType.Layout)
                _showCrop = _source != null;
            if (_showCrop && _source != null)
                DrawCrop();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            GUI.enabled = _source != null;
            if (GUILayout.Button("Save badge", GUILayout.Height(26f)))
                Save();
            GUI.enabled = true;
            if (GUILayout.Button("Cancel", GUILayout.Height(26f)))
                Close();
            GUILayout.EndHorizontal();
        }

        private void Load()
        {
            string path = _path.Trim().Trim('"');
            Texture2D loaded = Badges.LoadImage(path);
            if (loaded == null)
            {
                _status = "Could not load that file as a PNG or JPG.";
                return;
            }

            if (_source != null)
                UnityEngine.Object.Destroy(_source);
            _source = loaded;
            _cropSize = 1f;
            _cropX = 0.5f;
            _cropY = 0.5f;
            _status = "Drag on the picture to place the square, then set its size.";
        }

        private void DrawCrop()
        {
            Rect area = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));

            // Picture fitted inside the square preview area.
            float fit = Mathf.Min(area.width / _source.width, area.height / _source.height);
            var picture = new Rect(
                area.x + (area.width - _source.width * fit) * 0.5f,
                area.y + (area.height - _source.height * fit) * 0.5f,
                _source.width * fit,
                _source.height * fit);

            float side = _cropSize * Mathf.Min(picture.width, picture.height);
            HandleDrag(picture, side);
            var crop = new Rect(
                picture.x + _cropX * (picture.width - side),
                picture.y + _cropY * (picture.height - side),
                side,
                side);

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(picture, _source);

                Color old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(Rect.MinMaxRect(picture.xMin, picture.yMin, picture.xMax, crop.yMin), Texture2D.whiteTexture);
                GUI.DrawTexture(Rect.MinMaxRect(picture.xMin, crop.yMax, picture.xMax, picture.yMax), Texture2D.whiteTexture);
                GUI.DrawTexture(Rect.MinMaxRect(picture.xMin, crop.yMin, crop.xMin, crop.yMax), Texture2D.whiteTexture);
                GUI.DrawTexture(Rect.MinMaxRect(crop.xMax, crop.yMin, picture.xMax, crop.yMax), Texture2D.whiteTexture);

                GUI.color = Color.green;
                GUI.DrawTexture(new Rect(crop.x, crop.y, crop.width, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(crop.x, crop.yMax - 2f, crop.width, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(crop.x, crop.y, 2f, crop.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(crop.xMax - 2f, crop.y, 2f, crop.height), Texture2D.whiteTexture);
                GUI.color = old;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Size", GUILayout.Width(40f));
            _cropSize = GUILayout.HorizontalSlider(_cropSize, MinCrop, 1f);
            GUILayout.EndHorizontal();
        }

        // Centres the square on the mouse while the button is held over the picture.
        private void HandleDrag(Rect picture, float side)
        {
            Event e = Event.current;
            if ((e.type != EventType.MouseDown && e.type != EventType.MouseDrag) || e.button != 0 || !picture.Contains(e.mousePosition))
                return;

            float roomX = picture.width - side;
            float roomY = picture.height - side;
            _cropX = roomX > 0.5f ? Mathf.Clamp01((e.mousePosition.x - picture.x - side * 0.5f) / roomX) : 0.5f;
            _cropY = roomY > 0.5f ? Mathf.Clamp01((e.mousePosition.y - picture.y - side * 0.5f) / roomY) : 0.5f;
            e.Use();
        }

        private void Save()
        {
            // Same square as the preview, in source pixels measured from the top left.
            float side = _cropSize * Mathf.Min(_source.width, _source.height);
            float left = _cropX * (_source.width - side);
            float top = _cropY * (_source.height - side);

            var badge = new Texture2D(BadgeSize, BadgeSize, TextureFormat.RGBA32, false);
            var pixels = new Color[BadgeSize * BadgeSize];
            for (int y = 0; y < BadgeSize; y++)
            {
                // Texture rows run bottom to top.
                float fromTop = top + side * (1f - (y + 0.5f) / BadgeSize);
                float v = 1f - fromTop / _source.height;
                for (int x = 0; x < BadgeSize; x++)
                {
                    float u = (left + side * (x + 0.5f) / BadgeSize) / _source.width;
                    pixels[y * BadgeSize + x] = _source.GetPixelBilinear(u, v);
                }
            }
            badge.SetPixels(pixels);
            badge.Apply();
            byte[] png = badge.EncodeToPNG();
            UnityEngine.Object.Destroy(badge);

            if (!SaveStore.SetSlotBadge(Slot, png))
            {
                _status = "Could not save the badge. See the BepInEx log.";
                return;
            }

            Badges.Clear();
            SlotButtons.MarkBadgesDirty();
            Close();
        }

        // The Windows "Open" dialog. Returns null if cancelled or unavailable; the path
        // box still works then.
        private static class FileDialog
        {
            private const int ExplorerStyle = 0x00080000;
            private const int FileMustExist = 0x00001000;
            private const int PathMustExist = 0x00000800;
            private const int NoChangeDir = 0x00000008;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            private class OpenFileName
            {
                public int structSize;
                public IntPtr dlgOwner = IntPtr.Zero;
                public IntPtr instance = IntPtr.Zero;
                public string filter;
                public string customFilter;
                public int maxCustFilter;
                public int filterIndex;
                public string file;
                public int maxFile;
                public string fileTitle;
                public int maxFileTitle;
                public string initialDir;
                public string title;
                public int flags;
                public short fileOffset;
                public short fileExtension;
                public string defExt;
                public IntPtr custData = IntPtr.Zero;
                public IntPtr hook = IntPtr.Zero;
                public string templateName;
                public IntPtr reservedPtr = IntPtr.Zero;
                public int reservedInt;
                public int flagsEx;
            }

            [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

            [DllImport("user32.dll")]
            private static extern IntPtr GetActiveWindow();

            internal static string PickImage()
            {
                try
                {
                    var ofn = new OpenFileName();
                    ofn.structSize = Marshal.SizeOf(ofn);
                    ofn.dlgOwner = GetActiveWindow();
                    ofn.filter = "Pictures (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0";
                    ofn.file = new string(new char[1024]);
                    ofn.maxFile = ofn.file.Length;
                    ofn.fileTitle = new string(new char[256]);
                    ofn.maxFileTitle = ofn.fileTitle.Length;
                    ofn.title = "Choose a picture for the badge";
                    ofn.flags = ExplorerStyle | FileMustExist | PathMustExist | NoChangeDir;

                    if (!GetOpenFileName(ofn))
                        return null;

                    int end = ofn.file.IndexOf('\0');
                    return end >= 0 ? ofn.file.Substring(0, end) : ofn.file;
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning("Could not open the file dialog: " + ex);
                    return null;
                }
            }
        }
    }
}
