using System;
using System.Collections.Generic;
using System.IO;

namespace Full_Bot
{
    public class PickupItem
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "PickupItem.asm";

        // Valores de {IS_MAT}:
        // 0 = drops
        // 1 = matts
        // 3 = moedas
        public const uint TypeDrop = 0u;
        public const uint TypeMatt = 1u;
        public const uint TypeCoins = 3u;

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public PickupItem(MemoryProcess mem, AsmRunner asm)
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

        /// <summary>
        /// Executa PickupItem.asm passando {ITEM_SERVER_ID} e {IS_MAT}.
        /// Valores de isMat:
        /// 0 = drops
        /// 1 = matts (materiais)
        /// 3 = moedas
        /// </summary>
        public void Execute(uint itemServerId, uint isMat)
        {
            if (itemServerId == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "ITEM_SERVER_ID", itemServerId },
                { "IS_MAT", isMat }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }

        /// <summary>
        /// Atalho para coletar Matt ({IS_MAT} = 1).
        /// Valores de {IS_MAT}: 0 = drops | 1 = matts | 3 = moedas.
        /// </summary>
        public void PickupMatt(uint itemServerId)
        {
            Execute(itemServerId, TypeMatt);
        }

        /// <summary>
        /// Atalho para coletar Drop ({IS_MAT} = 0).
        /// Valores de {IS_MAT}: 0 = drops | 1 = matts | 3 = moedas.
        /// </summary>
        public void PickupDrop(uint itemServerId)
        {
            Execute(itemServerId, TypeDrop);
        }

        /// <summary>
        /// Atalho para coletar Moedas ({IS_MAT} = 3).
        /// Valores de {IS_MAT}: 0 = drops | 1 = matts | 3 = moedas.
        /// </summary>
        public void PickupCoins(uint itemServerId)
        {
            Execute(itemServerId, TypeCoins);
        }
    }

    public partial class PlayerCore
    {
        private PickupItem _pickupItem;

        public PickupItem PickupItemModule
        {
            get
            {
                if (_pickupItem == null)
                    _pickupItem = new PickupItem(_mem, _asm);
                return _pickupItem;
            }
        }

        /// <summary>
        /// Coleta item no chão passando ServerId e tipo ({IS_MAT}: 0 = drops, 1 = matts, 3 = moedas).
        /// </summary>
        public void CollectGroundItem(uint itemServerId, uint isMat)
        {
            PickupItemModule.Execute(itemServerId, isMat);
        }

        /// <summary>
        /// Coleta Matt ({IS_MAT} = 1). Valores de {IS_MAT}: 0 = drops | 1 = matts | 3 = moedas.
        /// </summary>
        public void CollectMatt(uint itemServerId)
        {
            PickupItemModule.PickupMatt(itemServerId);
        }

        /// <summary>
        /// Coleta Drop ({IS_MAT} = 0). Valores de {IS_MAT}: 0 = drops | 1 = matts | 3 = moedas.
        /// </summary>
        public void CollectDrop(uint itemServerId)
        {
            PickupItemModule.PickupDrop(itemServerId);
        }

        /// <summary>
        /// Coleta Moedas ({IS_MAT} = 3). Valores de {IS_MAT}: 0 = drops | 1 = matts | 3 = moedas.
        /// </summary>
        public void CollectCoins(uint itemServerId)
        {
            PickupItemModule.PickupCoins(itemServerId);
        }
    }
}