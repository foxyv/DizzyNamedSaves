using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Dizzy.NamedSaves
{
    internal sealed class StoredSave
    {
        internal string Dir;
        internal string Name;
        internal string Description;
        internal string BadgePath;
        internal DateTime LastPlayed;
    }

    // Slot names and the stored saves, all kept under <save folder>/NamedSaves.
    //
    // A slot owns more than slotN.save: its five backups, and whatever other mods keep
    // beside it (slotN.save.png, a slotN/ folder). Everything named "slotN" followed by
    // '.' or '_', plus the slotN folder, moves together. In a stored save's folder the
    // same files carry the bare prefix "slot".
    internal static class SaveStore
    {
        internal const int SlotCount = 6;
        internal const int MaxNameLength = 40;
        internal const int MaxDescriptionLength = 600;

        private const string StoredPrefix = "slot";
        private const string NameFile = "name.txt";
        private const string DescriptionFile = "description.txt";
        private const string BadgeFile = "badge.png";

        private static readonly string[] SlotNames = new string[SlotCount];
        private static readonly string[] SlotDescriptions = new string[SlotCount];

        internal static readonly List<StoredSave> Stored = new List<StoredSave>();

        private static string Root { get { return Application.persistentDataPath; } }
        private static string ModDir { get { return Path.Combine(Root, "NamedSaves"); } }
        private static string LibraryDir { get { return Path.Combine(ModDir, "library"); } }
        private static string SlotNamesPath { get { return Path.Combine(ModDir, "slot-names.txt"); } }

        internal static string SlotBadgePath(int slot)
        {
            return Path.Combine(ModDir, "slot" + slot + "-badge.png");
        }

        private static string SlotDescriptionPath(int slot)
        {
            return Path.Combine(ModDir, "slot" + slot + "-description.txt");
        }

        internal static void Load()
        {
            try
            {
                LoadSlotNames();
                LoadStored();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not read the named saves folder: " + ex);
            }
        }

        internal static bool SlotHasSave(int slot)
        {
            return File.Exists(SaveSlots.GetSlotSavePath(slot));
        }

        internal static DateTime SlotLastPlayed(int slot)
        {
            return File.GetLastWriteTime(SaveSlots.GetSlotSavePath(slot));
        }

        internal static string GetSlotName(int slot)
        {
            return SlotNames[slot] ?? string.Empty;
        }

        internal static void SetSlotName(int slot, string name)
        {
            name = CleanName(name);
            if (name == GetSlotName(slot))
                return;

            SlotNames[slot] = name;
            SaveSlotNames();
        }

        internal static string GetSlotDescription(int slot)
        {
            return SlotDescriptions[slot] ?? string.Empty;
        }

        internal static void SetSlotDescription(int slot, string description)
        {
            description = CleanDescription(description);
            if (description == GetSlotDescription(slot))
                return;

            SlotDescriptions[slot] = description;
            try
            {
                Directory.CreateDirectory(ModDir);
                File.WriteAllText(SlotDescriptionPath(slot), description);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not save the description for slot " + slot + ": " + ex);
            }
        }

        internal static bool SetSlotBadge(int slot, byte[] png)
        {
            try
            {
                Directory.CreateDirectory(ModDir);
                File.WriteAllBytes(SlotBadgePath(slot), png);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not save the badge for slot " + slot + ": " + ex);
                return false;
            }
        }

        // Removes only the mod's cropped copy; the picture it was made from is untouched.
        internal static void RemoveSlotBadge(int slot)
        {
            try
            {
                if (File.Exists(SlotBadgePath(slot)))
                    File.Delete(SlotBadgePath(slot));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not remove the badge for slot " + slot + ": " + ex);
            }
        }

        // A badge is a convenience; failing to move it must not fail the save move.
        private static void MoveBadge(string from, string to)
        {
            try
            {
                if (File.Exists(to))
                    File.Delete(to);
                if (File.Exists(from))
                    File.Move(from, to);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not move badge " + from + ": " + ex);
            }
        }

        internal static void RenameStored(StoredSave save, string name)
        {
            name = CleanName(name);
            if (name == save.Name)
                return;

            save.Name = name;
            try
            {
                File.WriteAllText(Path.Combine(save.Dir, NameFile), name);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not rename stored save: " + ex);
            }
        }

        // Moves the slot's save out to the Save Menu and leaves the slot empty.
        internal static bool StoreSlot(int slot, out string message)
        {
            if (!SlotHasSave(slot))
            {
                message = "Slot " + slot + " is empty.";
                return false;
            }

            try
            {
                string name = MoveSlotToLibrary(slot).Name;
                Load();
                message = "Stored \"" + name + "\". Slot " + slot + " is now empty.";
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not store slot " + slot + ": " + ex);
                Load();
                message = "Could not store slot " + slot + ": " + ex.Message;
                return false;
            }
        }

        // Puts a stored save into the slot. A save already in the slot takes its place
        // in the Save Menu.
        internal static bool SwapIntoSlot(StoredSave incoming, int slot, out string message)
        {
            StoredSave outgoing = null;
            try
            {
                if (SlotHasSave(slot))
                    outgoing = MoveSlotToLibrary(slot);

                try
                {
                    MoveSet(incoming.Dir, StoredPrefix, Root, "slot" + slot);
                }
                catch
                {
                    // Put the slot's own save back so a failed swap changes nothing.
                    if (outgoing != null)
                        RestoreSlot(outgoing, slot);
                    throw;
                }

                SlotNames[slot] = incoming.Name;
                SaveSlotNames();
                SetSlotDescription(slot, incoming.Description);
                MoveBadge(incoming.BadgePath, SlotBadgePath(slot));
                RemoveEmptiedFolder(incoming.Dir);

                Load();
                message = outgoing != null
                    ? "Slot " + slot + " now holds \"" + incoming.Name + "\". Stored \"" + outgoing.Name + "\"."
                    : "Slot " + slot + " now holds \"" + incoming.Name + "\".";
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not swap into slot " + slot + ": " + ex);
                Load();
                message = "Could not swap into slot " + slot + ": " + ex.Message;
                return false;
            }
        }

        private static StoredSave MoveSlotToLibrary(int slot)
        {
            string name = GetSlotName(slot);
            if (name.Length == 0)
                name = "Slot " + slot + " save";

            string dir = NewLibraryFolder();
            Directory.CreateDirectory(dir);
            try
            {
                MoveSet(Root, "slot" + slot, dir, StoredPrefix);
            }
            catch
            {
                RemoveEmptiedFolder(dir);
                throw;
            }

            string description = GetSlotDescription(slot);
            File.WriteAllText(Path.Combine(dir, NameFile), name);
            if (description.Length > 0)
                File.WriteAllText(Path.Combine(dir, DescriptionFile), description);
            SlotNames[slot] = string.Empty;
            SaveSlotNames();
            SetSlotDescription(slot, string.Empty);
            string badgePath = Path.Combine(dir, BadgeFile);
            MoveBadge(SlotBadgePath(slot), badgePath);

            return new StoredSave { Dir = dir, Name = name, Description = description, BadgePath = badgePath };
        }

        private static void RestoreSlot(StoredSave outgoing, int slot)
        {
            try
            {
                MoveSet(outgoing.Dir, StoredPrefix, Root, "slot" + slot);
                SlotNames[slot] = outgoing.Name;
                SaveSlotNames();
                SetSlotDescription(slot, outgoing.Description);
                MoveBadge(outgoing.BadgePath, SlotBadgePath(slot));
                RemoveEmptiedFolder(outgoing.Dir);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not put slot " + slot + " back; its save is in " + outgoing.Dir + ": " + ex);
            }
        }

        // Moves every file and folder that belongs to srcPrefix. Nothing is overwritten:
        // if any destination already exists nothing moves, and a failure part way through
        // moves the earlier ones back.
        private static void MoveSet(string srcDir, string srcPrefix, string dstDir, string dstPrefix)
        {
            var moves = new List<KeyValuePair<string, string>>();

            foreach (string file in Directory.GetFiles(srcDir))
            {
                string name = Path.GetFileName(file);
                if (BelongsTo(name, srcPrefix))
                    moves.Add(new KeyValuePair<string, string>(file, Path.Combine(dstDir, dstPrefix + name.Substring(srcPrefix.Length))));
            }

            string folder = Path.Combine(srcDir, srcPrefix);
            if (Directory.Exists(folder))
                moves.Add(new KeyValuePair<string, string>(folder, Path.Combine(dstDir, dstPrefix)));

            foreach (KeyValuePair<string, string> move in moves)
            {
                if (File.Exists(move.Value) || Directory.Exists(move.Value))
                    throw new IOException(move.Value + " already exists.");
            }

            int done = 0;
            try
            {
                for (; done < moves.Count; done++)
                    MovePath(moves[done].Key, moves[done].Value);
            }
            catch
            {
                for (int i = done - 1; i >= 0; i--)
                {
                    try
                    {
                        MovePath(moves[i].Value, moves[i].Key);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogError("Could not move " + moves[i].Value + " back: " + ex);
                    }
                }
                throw;
            }
        }

        private static void MovePath(string from, string to)
        {
            if (Directory.Exists(from))
                Directory.Move(from, to);
            else
                File.Move(from, to);
        }

        // "slot1" owns slot1.save and slot1_backup2.save but not slot10.save.
        private static bool BelongsTo(string fileName, string prefix)
        {
            if (fileName.Length <= prefix.Length || !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            char next = fileName[prefix.Length];
            return next == '.' || next == '_';
        }

        private static string NewLibraryFolder()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string dir = Path.Combine(LibraryDir, stamp);
            for (int i = 2; Directory.Exists(dir); i++)
                dir = Path.Combine(LibraryDir, stamp + "-" + i);
            return dir;
        }

        // Only removes the mod's own name and description files and the folder once the save has left it.
        private static void RemoveEmptiedFolder(string dir)
        {
            try
            {
                string nameFile = Path.Combine(dir, NameFile);
                if (File.Exists(nameFile))
                    File.Delete(nameFile);
                string descriptionFile = Path.Combine(dir, DescriptionFile);
                if (File.Exists(descriptionFile))
                    File.Delete(descriptionFile);
                if (Directory.GetFileSystemEntries(dir).Length == 0)
                    Directory.Delete(dir);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("Could not tidy " + dir + ": " + ex.Message);
            }
        }

        private static void LoadSlotNames()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                SlotNames[i] = string.Empty;
                string descriptionPath = SlotDescriptionPath(i);
                SlotDescriptions[i] = File.Exists(descriptionPath) ? CleanDescription(File.ReadAllText(descriptionPath)) : string.Empty;
            }

            if (!File.Exists(SlotNamesPath))
                return;

            foreach (string line in File.ReadAllLines(SlotNamesPath))
            {
                int eq = line.IndexOf('=');
                int slot;
                if (eq <= 0 || !int.TryParse(line.Substring(0, eq), out slot) || slot < 0 || slot >= SlotCount)
                    continue;
                SlotNames[slot] = CleanName(line.Substring(eq + 1));
            }
        }

        private static void SaveSlotNames()
        {
            try
            {
                Directory.CreateDirectory(ModDir);
                var lines = new string[SlotCount];
                for (int i = 0; i < SlotCount; i++)
                    lines[i] = i + "=" + GetSlotName(i);
                File.WriteAllLines(SlotNamesPath, lines);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError("Could not save slot names: " + ex);
            }
        }

        private static void LoadStored()
        {
            Stored.Clear();
            if (!Directory.Exists(LibraryDir))
                return;

            foreach (string dir in Directory.GetDirectories(LibraryDir))
            {
                string savePath = Path.Combine(dir, StoredPrefix + ".save");
                if (!File.Exists(savePath))
                    continue;

                string nameFile = Path.Combine(dir, NameFile);
                string name = File.Exists(nameFile) ? CleanName(File.ReadAllText(nameFile)) : string.Empty;
                if (name.Length == 0)
                    name = Path.GetFileName(dir);

                string descriptionFile = Path.Combine(dir, DescriptionFile);
                string description = File.Exists(descriptionFile) ? CleanDescription(File.ReadAllText(descriptionFile)) : string.Empty;

                Stored.Add(new StoredSave { Dir = dir, Name = name, Description = description, BadgePath = Path.Combine(dir, BadgeFile), LastPlayed = File.GetLastWriteTime(savePath) });
            }

            Stored.Sort((a, b) => b.LastPlayed.CompareTo(a.LastPlayed));
        }

        private static string CleanDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return string.Empty;
            description = description.Replace("\r", string.Empty);
            if (description.Length > MaxDescriptionLength)
                description = description.Substring(0, MaxDescriptionLength);
            return description;
        }

        private static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            name = name.Replace("\r", string.Empty).Replace("\n", " ");
            if (name.Length > MaxNameLength)
                name = name.Substring(0, MaxNameLength);
            return name;
        }
    }
}
