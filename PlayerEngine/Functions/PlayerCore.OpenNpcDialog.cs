using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Full_Bot
{
    public class OpenNpcDialog
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "OpenNpcDialog.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public OpenNpcDialog(MemoryProcess mem, AsmRunner asm)
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

        private static uint ParseHexOrDecimalId(string rawId)
        {
            if (string.IsNullOrWhiteSpace(rawId))
                return 0u;

            string clean = rawId.Trim();
            if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(2).Trim();
                if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexVal))
                    return hexVal;
            }

            if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexFallback))
                return hexFallback;

            throw new FormatException($"ID de NPC inválido: '{rawId}'.");
        }

        public void Execute(uint npcId)
        {
            if (npcId == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "NPC_ID", npcId }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }

        public void ExecuteHex(string npcIdHex)
        {
            uint npcId = ParseHexOrDecimalId(npcIdHex);
            Execute(npcId);
        }
    }

    public partial class PlayerCore
    {
        private OpenNpcDialog _openNpcDialog;

        public OpenNpcDialog OpenNpcDialogModule
        {
            get
            {
                if (_openNpcDialog == null)
                    _openNpcDialog = new OpenNpcDialog(_mem, _asm);
                return _openNpcDialog;
            }
        }

        public void OpenDialogWithNpcId(uint npcId)
        {
            OpenNpcDialogModule.Execute(npcId);
        }

        public void OpenDialogWithNpcHex(string npcIdHex)
        {
            OpenNpcDialogModule.ExecuteHex(npcIdHex);
        }
    }
}