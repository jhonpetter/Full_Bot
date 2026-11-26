using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Full_Bot
{
    public class PickupQuest
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "PickupQuest.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public PickupQuest(MemoryProcess mem, AsmRunner asm)
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

        public bool IsQuestAlreadyActive(PlayerCore player, uint questId)
        {
            if (player == null || questId == 0u)
                return false;

            return player.GetAllActiveQuestsFlat().Any(q => q != null && q.QuestId == questId);
        }

        public void Execute(uint questId)
        {
            if (questId == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "QUEST_ID", questId }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }

        public bool TryPickupNewQuest(PlayerCore player, uint questId)
        {
            if (questId == 0u)
                return false;

            if (IsQuestAlreadyActive(player, questId))
                return false;

            Execute(questId);
            return true;
        }
    }

    public class FinishQuest
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "FinishQuest.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public FinishQuest(MemoryProcess mem, AsmRunner asm)
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

        public ActiveQuestInfo FindActiveQuest(PlayerCore player, uint questId)
        {
            if (player == null || questId == 0u)
                return null;

            var tree = player.GetActiveQuests();
            if (tree != null)
            {
                foreach (var root in tree)
                {
                    var found = FindInNode(root, questId);
                    if (found != null)
                        return found;
                }
            }

            return player.GetAllActiveQuestsFlat().FirstOrDefault(q => q != null && q.QuestId == questId);
        }

        private static ActiveQuestInfo FindInNode(ActiveQuestInfo node, uint questId)
        {
            if (node == null)
                return null;

            if (node.QuestId == questId)
                return node;

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var match = FindInNode(child, questId);
                    if (match != null)
                        return match;
                }
            }

            return null;
        }

        public void Execute(uint questId)
        {
            if (questId == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "QUEST_ID", questId }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }

        public bool TryFinishOrContinueQuest(PlayerCore player, uint questId)
        {
            if (questId == 0u)
                return false;

            Execute(questId);
            return true;
        }
    }

    public partial class PlayerCore
    {
        private PickupQuest _pickupQuest;
        private FinishQuest _finishQuest;

        public PickupQuest PickupQuestModule
        {
            get
            {
                if (_pickupQuest == null)
                    _pickupQuest = new PickupQuest(_mem, _asm);
                return _pickupQuest;
            }
        }

        public FinishQuest FinishQuestModule
        {
            get
            {
                if (_finishQuest == null)
                    _finishQuest = new FinishQuest(_mem, _asm);
                return _finishQuest;
            }
        }

        public bool PickupNewQuest(uint questId)
        {
            return PickupQuestModule.TryPickupNewQuest(this, questId);
        }

        public bool FinishQuestAtNpc(uint questId)
        {
            return FinishQuestModule.TryFinishOrContinueQuest(this, questId);
        }
    }
}