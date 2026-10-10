using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dizzy.NamedSaves
{
    // Badge textures by file path. A missing badge is cached too, so the menu does not
    // touch the disk every frame; Clear after anything that adds, removes or moves one.
    internal static class Badges
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        internal static Texture2D Get(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            Texture2D texture;
            if (Cache.TryGetValue(path, out texture))
                return texture;

            texture = LoadImage(path);
            Cache[path] = texture;
            return texture;
        }

        internal static void Clear()
        {
            foreach (Texture2D texture in Cache.Values)
            {
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }
            Cache.Clear();
        }

        internal static Texture2D LoadImage(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(File.ReadAllBytes(path)))
                    return texture;

                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not load image " + path + ": " + ex);
            }
            return null;
        }
    }
}
