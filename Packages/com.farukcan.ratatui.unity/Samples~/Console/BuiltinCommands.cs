using System.Text;
using UnityEngine;

namespace RatatuiUnity.Samples.Console
{
    /// <summary>
    /// Registers the always-available console commands: help, clear, quit, echo.
    /// </summary>
    internal static class BuiltinCommands
    {
        public static void Register()
        {
            RatatuiConsole.RegisterCommand("help", "List all registered commands.", HelpCmd);
            RatatuiConsole.RegisterCommand("clear", "Clear the log buffer.", ClearCmd);
            RatatuiConsole.RegisterCommand("quit", "Exit the application.", QuitCmd);
            RatatuiConsole.RegisterCommand("echo", "Print arguments back to the console.", EchoCmd);
        }

        private static void HelpCmd(string[] args)
        {
            var commands = RatatuiConsole.Registry.SortedAll();
            var sb = new StringBuilder(256);
            sb.Append("Registered commands (").Append(commands.Count).Append("):");
            foreach (var cmd in commands)
            {
                sb.Append('\n').Append("  ").Append(cmd.Name);
                if (!string.IsNullOrEmpty(cmd.Description))
                    sb.Append("  -  ").Append(cmd.Description);
            }
            Debug.Log(sb.ToString());
        }

        private static void ClearCmd(string[] args)
        {
            RatatuiConsole.ClearLogs();
        }

        private static void QuitCmd(string[] args)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void EchoCmd(string[] args)
        {
            Debug.Log(string.Join(" ", args));
        }
    }
}
