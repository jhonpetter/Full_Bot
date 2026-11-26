using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Full_Bot
{
    public class SelectTarget
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "SelectTarget.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public SelectTarget(MemoryProcess mem, AsmRunner asm)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
        }

        private static string ResolveScriptPath()
        {
            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ScriptsFolder, ScriptFileName);
            if (File.Exists(baseDirPath))
                return baseDirPath;

            return Path.Combine(ScriptsFolder, ScriptFileName);
        }

        public static uint ParseHexId(string hexId)
        {
            if (string.IsNullOrWhiteSpace(hexId))
                return 0u;

            string clean = hexId.Trim();
            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                clean = clean.Substring(2).Trim();

            if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsed))
                return parsed;

            throw new FormatException($"ID hexadecimal inválido para SelectTarget: '{hexId}'.");
        }

        public void Execute(uint targetId)
        {
            if (targetId == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "TARGET_ID", targetId }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }

        public void ExecuteHex(string targetIdHex)
        {
            uint targetId = ParseHexId(targetIdHex);
            Execute(targetId);
        }
    }

    public partial class PlayerCore
    {
        private SelectTarget _selectTarget;

        public SelectTarget SelectTargetModule
        {
            get
            {
                if (_selectTarget == null)
                    _selectTarget = new SelectTarget(_mem, _asm);
                return _selectTarget;
            }
        }

        public void SelectTargetById(uint targetId)
        {
            SelectTargetModule.Execute(targetId);
        }

        public void SelectTargetByHex(string targetIdHex)
        {
            SelectTargetModule.ExecuteHex(targetIdHex);
        }
    }
}