using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RatatuiUnity.Samples.Notepad
{
    /// <summary>
    /// A single note persisted under <see cref="Application.persistentDataPath"/>.
    /// </summary>
    public sealed class NotepadEntry
    {
        public string Id;
        public string Filename;
        public string Content;
    }

    [Serializable]
    sealed class NotepadEntryDto
    {
        public string filename;
        public string content;
    }

    /// <summary>
    /// Loads and saves notes as individual JSON files in persistent storage.
    /// </summary>
    public static class NotepadStorage
    {
        private const string FolderName = "ratatui-notepad";

        public static string RootPath => Path.Combine(Application.persistentDataPath, FolderName);

        public static void EnsureDirectory()
        {
            Directory.CreateDirectory(RootPath);
        }

        public static List<NotepadEntry> LoadAll()
        {
            EnsureDirectory();
            var list = new List<NotepadEntry>();

            foreach (string file in Directory.GetFiles(RootPath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var dto = JsonUtility.FromJson<NotepadEntryDto>(json);
                    if (dto == null) continue;

                    list.Add(new NotepadEntry
                    {
                        Id = Path.GetFileNameWithoutExtension(file),
                        Filename = dto.filename ?? string.Empty,
                        Content = dto.content ?? string.Empty,
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RatatuiNotepad] Skipping corrupt note file '{file}': {ex.Message}");
                }
            }

            list.Sort((a, b) => string.Compare(a.Filename, b.Filename, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        public static void Save(NotepadEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                return;

            EnsureDirectory();
            var dto = new NotepadEntryDto
            {
                filename = entry.Filename ?? string.Empty,
                content = entry.Content ?? string.Empty,
            };

            string path = Path.Combine(RootPath, entry.Id + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(dto, prettyPrint: true));
        }

        public static void Delete(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            string path = Path.Combine(RootPath, id + ".json");
            if (File.Exists(path))
                File.Delete(path);
        }

        public static string CreateId() => Guid.NewGuid().ToString("N");
    }
}
