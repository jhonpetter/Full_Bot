using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Full_Bot
{
    public static class BotHub
    {
        public const string DefaultConfigPath = @"Config\config.CT";

        private static readonly object _sync = new object();
        private static readonly List<PlayerSession> _sessions = new List<PlayerSession>();

        public static string LastStatus { get; private set; } = "Aguardando";

        public static string ResolveConfigPath(string configPath = null)
        {
            string target = string.IsNullOrWhiteSpace(configPath) ? DefaultConfigPath : configPath.Trim();
            if (Path.IsPathRooted(target))
                return target;

            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, target);
            if (File.Exists(baseDirPath))
                return baseDirPath;

            return Path.GetFullPath(target);
        }

        public static string DetectProcessName(string configPath = null)
        {
            string resolved = ResolveConfigPath(configPath);
            if (!File.Exists(resolved))
                return "elementclient";

            try
            {
                var doc = CheatTableReader.Load(resolved);
                return doc.DetectProcessName("elementclient");
            }
            catch
            {
                return "elementclient";
            }
        }

        public static IReadOnlyList<PlayerSession> GetSnapshot()
        {
            lock (_sync)
            {
                CleanupExitedLocked();
                return _sessions.ToList();
            }
        }

        public static int RefreshProcesses(string configPath = null)
        {
            lock (_sync)
            {
                CleanupExitedLocked();

                string resolvedPath = ResolveConfigPath(configPath);
                if (!File.Exists(resolvedPath))
                {
                    LastStatus = $"Config não encontrado: {resolvedPath}";
                    return 0;
                }

                var doc = CheatTableReader.Load(resolvedPath);
                string procName = doc.DetectProcessName("elementclient");
                var runningProcs = Process.GetProcessesByName(procName);
                int added = 0;
                string lastErr = string.Empty;

                foreach (var proc in runningProcs)
                {
                    try
                    {
                        if (proc.HasExited) continue;

                        bool alreadyAttached = _sessions.Any(s =>
                            s?.Memory?.Process != null &&
                            !s.Memory.Process.HasExited &&
                            s.Memory.Process.Id == proc.Id);

                        if (alreadyAttached) continue;

                        var session = new PlayerSession(resolvedPath, proc, doc);
                        _sessions.Add(session);
                        added++;
                    }
                    catch (Exception ex)
                    {
                        lastErr = $"PID {proc.Id}: {ex.Message}";
                    }
                }

                if (!string.IsNullOrEmpty(lastErr))
                    LastStatus = $"Proc '{procName}' ({runningProcs.Length}) | Erro: {lastErr}";
                else
                    LastStatus = $"Proc '{procName}' ({runningProcs.Length}) | Sessões: {_sessions.Count}";

                return added;
            }
        }

        public static void ClearAll()
        {
            lock (_sync)
            {
                foreach (var session in _sessions)
                {
                    try { session?.Dispose(); }
                    catch { }
                }
                _sessions.Clear();
                LastStatus = "Limpo";
            }
        }

        private static void CleanupExitedLocked()
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                var s = _sessions[i];
                if (s == null || !s.IsAlive)
                {
                    try { s?.Dispose(); }
                    catch { }
                    _sessions.RemoveAt(i);
                }
            }
        }
    }
}