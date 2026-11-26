using System;
using System.IO;

namespace Full_Bot
{
    public class AutoAttack
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "AutoAttack.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public AutoAttack(MemoryProcess mem, AsmRunner asm)
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

        public void Execute()
        {
            _asm.RunScript(_mem, ResolveScriptPath());
        }

        public void AttackTargetId(PlayerCore player, uint targetId)
        {
            if (player == null || targetId == 0u)
                return;

            player.SelectTargetById(targetId);
            Execute();
        }

        public void MoveAndAttack(PlayerCore player, uint targetId, float x, float z)
        {
            if (player == null || targetId == 0u)
                return;

            player.SelectTargetById(targetId);
            player.MoveSimple(x, z);
            Execute();
        }
    }

    public partial class PlayerCore
    {
        private AutoAttack _autoAttack;

        public AutoAttack AutoAttackModule
        {
            get
            {
                if (_autoAttack == null)
                    _autoAttack = new AutoAttack(_mem, _asm);
                return _autoAttack;
            }
        }

        public void AttackCurrentTarget()
        {
            AutoAttackModule.Execute();
        }

        public void AttackById(uint targetId)
        {
            AutoAttackModule.AttackTargetId(this, targetId);
        }

        public void MoveToAndAttackById(uint targetId, float x, float z)
        {
            AutoAttackModule.MoveAndAttack(this, targetId, x, z);
        }
    }
}