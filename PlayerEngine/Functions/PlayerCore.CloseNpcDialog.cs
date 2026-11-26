using System;
using System.Collections.Generic;
using System.IO;

namespace Full_Bot
{
    public class CloseNpcDialog
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "CloseNpcDialog.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;
        private readonly PlayerCore _player;

        public CloseNpcDialog(MemoryProcess mem, AsmRunner asm, PlayerCore player)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
            _player = player ?? throw new ArgumentNullException(nameof(player));
        }

        private static string ResolveScriptPath()
        {
            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ScriptsFolder, ScriptFileName);
            if (File.Exists(baseDirPath))
                return baseDirPath;

            return Path.Combine(ScriptsFolder, ScriptFileName);
        }

        public bool IsDialogOpen()
        {
            uint windowPtr = ResolveFocusedWindowPtr();
            if (!MemoryProcess.IsValidPointer(windowPtr))
                return false;

            if (!IsFocusedWindowVisible())
                return false;

            uint dialogShowAddr = ResolveFlagAddress("GameUi_DialogShow");
            uint playerDialogAddr = ResolveFlagAddress("player_dialog_open");

            byte dialogShow = dialogShowAddr != 0u ? _mem.ReadByte(dialogShowAddr) : (byte)0;
            byte playerDialog = playerDialogAddr != 0u ? _mem.ReadByte(playerDialogAddr) : (byte)0;

            return dialogShow != 0 || playerDialog != 0 || IsFocusedWindowVisible();
        }

        public bool Execute()
        {
            if (_mem == null || _asm == null)
                return false;

            string scriptPath = ResolveScriptPath();
            if (!File.Exists(scriptPath))
                return false;

            uint windowPtr = ResolveFocusedWindowPtr();
            if (!MemoryProcess.IsValidPointer(windowPtr))
                return false;

            if (!IsFocusedWindowVisible())
                return false;

            uint dialogFnOff = _mem.GetNodeOffset("GameUi_Current_DialogShow_function");
            if (dialogFnOff == 0u)
                return false;

            var vars = new Dictionary<string, uint>
            {
                { "WINDOW_PTR", windowPtr },
                { "DIALOG_FN_OFF", dialogFnOff }
            };

            _asm.RunScript(_mem, scriptPath, vars);
            return true;
        }

        private uint ResolveFocusedWindowPtr()
        {
            var focusNode = _mem.GetNode("GameUi_Current_Window_Focus");
            if (focusNode == null)
                return 0u;

            uint ptr = _mem.ResolveNodeAddress(focusNode, dereferenceLastPointer: true);
            if (MemoryProcess.IsValidPointer(ptr))
                return ptr;

            uint altPtr = _mem.GetNodeAddressByKey("GameUi_Current_Window_Focus");
            return MemoryProcess.IsValidPointer(altPtr) ? altPtr : 0u;
        }

        private bool IsFocusedWindowVisible()
        {
            var showNode = _mem.GetNode("GameUi_Current_DialogShow_Focus");
            if (showNode == null)
                return true;

            uint addr = _mem.ResolveNodeAddress(showNode, dereferenceLastPointer: false);
            if (addr == 0u)
                return true;

            // Lê o byte baixo (mesmo critério de "cmp byte [ecx+90], 1", tratando valores como 1 ou 65537 [0x10001])
            return (_mem.ReadByte(addr) & 0x01) != 0;
        }

        private uint ResolveFlagAddress(string nodeKey)
        {
            var node = _mem.GetNode(nodeKey);
            if (node == null)
                return 0u;

            uint addr = _mem.ResolveNodeAddress(node, dereferenceLastPointer: false);
            if (MemoryProcess.IsValidPointer(addr))
                return addr;

            uint altAddr = _mem.GetNodeAddressByKey(nodeKey);
            return MemoryProcess.IsValidPointer(altAddr) ? altAddr : 0u;
        }
    }

    public partial class PlayerCore
    {
        private CloseNpcDialog _closeNpcDialog;

        public CloseNpcDialog CloseNpcDialogModule
        {
            get
            {
                if (_closeNpcDialog == null)
                    _closeNpcDialog = new CloseNpcDialog(_mem, _asm, this);
                return _closeNpcDialog;
            }
        }

        public bool IsNpcDialogOpen()
        {
            return CloseNpcDialogModule.IsDialogOpen();
        }

        public bool CloseNpcDialogWindow()
        {
            return CloseNpcDialogModule.Execute();
        }
    }
}