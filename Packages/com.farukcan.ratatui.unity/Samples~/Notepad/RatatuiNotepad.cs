using System.Collections.Generic;
using UnityEngine;

namespace RatatuiUnity.Samples.Notepad
{
    /// <summary>
    /// Public facade and bootstrap entry point for the Ratatui Notepad terminal app.
    /// The renderer is created by <see cref="RatatuiTerminalApps"/> at boot.
    /// </summary>
    public static class RatatuiNotepad
    {
        private const string AppId = "notepad";

        private static bool _servicesBooted;
        private static RatatuiNotepadConfig _config;

        public static RatatuiNotepadConfig Config => _config;

        public static bool IsOpen => RatatuiTerminalApps.IsOpen(AppId);

        public static string StoragePath => NotepadStorage.RootPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _servicesBooted = false;
            _config = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureServicesBooted();
        }

        internal static void EnsureServicesBooted()
        {
            if (_servicesBooted) return;
            _servicesBooted = true;

            _config = Resources.Load<RatatuiNotepadConfig>("RatatuiNotepadConfig");
            if (_config == null)
                _config = RatatuiNotepadConfig.CreateDefault();
        }

        public static List<NotepadEntry> LoadNotes() => NotepadStorage.LoadAll();

        public static void SaveNote(NotepadEntry entry) => NotepadStorage.Save(entry);

        public static void DeleteNote(string id) => NotepadStorage.Delete(id);

        public static void Open() => RatatuiTerminalApps.Open(AppId);

        public static void Close() => RatatuiTerminalApps.Close(AppId);

        public static void Toggle() => RatatuiTerminalApps.Toggle(AppId);
    }
}
