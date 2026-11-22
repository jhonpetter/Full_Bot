using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace Full_Bot
{
    public static class BotHub
    {
        private static readonly object _sync = new object();
        private static readonly List<BotController> _bots = new List<BotController>();

        public static IReadOnlyList<BotController> GetSnapshot()
        {
            lock (_sync)
            {
                CleanupExitedLocked();
                return _bots.ToList();
            }
        }

        public static int RefreshProcesses(string configPath = @"Config\config.CT")
        {
            lock (_sync)
            {
                CleanupExitedLocked();

                var probe = new BotController(configPath);
                string procName = probe.Config.DetectProcessName("elementclient");
                probe.Dispose();

                var runningProcs = Process.GetProcessesByName(procName);
                int added = 0;

                foreach (var proc in runningProcs)
                {
                    try
                    {
                        if (proc.HasExited) continue;

                        bool alreadyAttached = _bots.Any(b =>
                            b?.Memory?.Process != null &&
                            !b.Memory.Process.HasExited &&
                            b.Memory.Process.Id == proc.Id);

                        if (alreadyAttached) continue;

                        var bot = new BotController(configPath);
                        if (bot.AttachProcess(proc))
                        {
                            _bots.Add(bot);
                            added++;
                        }
                        else
                        {
                            bot.Dispose();
                        }
                    }
                    catch
                    {
                        // Ignora processos inacessíveis ou finalizando
                    }
                }

                return added;
            }
        }

        public static void PulseAll()
        {
            List<BotController> snapshot;
            lock (_sync)
            {
                CleanupExitedLocked();
                snapshot = _bots.ToList();
            }

            foreach (var bot in snapshot)
            {
                try
                {
                    bot?.Pulse();
                }
                catch
                {
                    // Evita que erro em um client derrube os outros
                }
            }
        }

        public static ToolStrip CreateDefaultModulesStrip(Func<uint> getSelectedPlayerId = null, IWin32Window owner = null)
        {
            return BotHubModulesMenu.CreateModulesToolStrip(getSelectedPlayerId, owner);
        }

        public static BotController FindBotByPlayerId(uint playerId)
        {
            if (playerId == 0)
                return null;

            var snapshot = GetSnapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var bot = snapshot[i];
                if (bot?.Player != null && bot.Player.Id == playerId)
                    return bot;
            }

            return null;
        }

        public static void ClearAll()
        {
            lock (_sync)
            {
                foreach (var bot in _bots)
                {
                    try { bot?.Dispose(); }
                    catch { }
                }
                _bots.Clear();
            }
        }

        private static void CleanupExitedLocked()
        {
            for (int i = _bots.Count - 1; i >= 0; i--)
            {
                var b = _bots[i];
                bool exited = b?.Memory?.Process == null || b.Memory.Process.HasExited;
                if (exited)
                {
                    try { b?.Dispose(); }
                    catch { }
                    _bots.RemoveAt(i);
                }
            }
        }
    }
}