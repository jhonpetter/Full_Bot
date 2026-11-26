using System;
using System.Collections.Generic;
using System.IO;

namespace Full_Bot
{
    public class CraftItem
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "CraftItem.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public CraftItem(MemoryProcess mem, AsmRunner asm)
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

        public bool Execute(uint itemId, uint amount = 1u)
        {
            if (itemId == 0u || amount == 0u)
                return false;

            var vars = new Dictionary<string, uint>
            {
                { "ITEM_ID", itemId },
                { "ITEM_AMOUNT", amount }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
            return true;
        }
    }

    public partial class PlayerCore
    {
        private CraftItem _craftItem;

        public CraftItem CraftItemModule
        {
            get
            {
                if (_craftItem == null)
                    _craftItem = new CraftItem(_mem, _asm);
                return _craftItem;
            }
        }

        /// <summary>
        /// Fabrica um item passando o ID do item e a quantidade (padrão 1).
        /// </summary>
        public bool CraftItemById(uint itemId, uint amount = 1u)
        {
            return CraftItemModule.Execute(itemId, amount);
        }
    }
}