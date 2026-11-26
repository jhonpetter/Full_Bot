using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Full_Bot
{
    public class PendingDialogInvite
    {
        public string DialogType { get; set; } = string.Empty;
        public uint PlayerId { get; set; }
        public uint TimeHit { get; set; }
        public bool IsGroupInvite => string.Equals(DialogType, "Game_InviteGroup", StringComparison.OrdinalIgnoreCase);
    }

    public class InviteParty
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "InviteParty.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public InviteParty(MemoryProcess mem, AsmRunner asm)
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

        public void Execute(uint targetPlayerId)
        {
            if (targetPlayerId == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "TARGET_PLAYER_ID", targetPlayerId }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }
    }

    public class AcceptParty
    {
        private const string ScriptsFolder = "Scripts";
        private const string ScriptFileName = "AcceptParty.asm";

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;
        private uint _lastAcceptedTimeHit = 0u;

        public AcceptParty(MemoryProcess mem, AsmRunner asm)
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

        private uint GetNodeOffset(string key)
        {
            var node = _mem.GetNode(key);
            if (node == null) return 0u;

            var local = node.GetLocalOffsetsUInt();
            if (local != null && local.Length > 0)
                return local[0];

            return node.HasOwnAddressDisplacement ? node.OwnAddressDisplacement : 0u;
        }

        private string ReadAnsiString(uint addr, int maxLen = 100)
        {
            if (addr == 0u || maxLen <= 0)
                return string.Empty;

            var bytes = new List<byte>();
            for (int i = 0; i < maxLen; i++)
            {
                byte b = _mem.ReadByte(unchecked(addr + (uint)i));
                if (b == 0)
                    break;
                bytes.Add(b);
            }

            if (bytes.Count == 0)
                return string.Empty;

            return Encoding.ASCII.GetString(bytes.ToArray()).Trim();
        }

        private string ReadDialogTypeString(uint popupBase)
        {
            var typeNode = _mem.GetNode("msg_dialog_type");
            if (typeNode != null)
            {
                uint resolvedAddr = _mem.ResolveNodeAddress(typeNode, dereferenceLastPointer: false);
                if (resolvedAddr != 0u)
                {
                    string s = ReadAnsiString(resolvedAddr, 100);
                    if (!string.IsNullOrEmpty(s))
                        return s;
                }
            }

            if (popupBase == 0u)
                return string.Empty;

            // Fallback direto da cadeia +0 -> +0 a partir de popupBase
            uint ptr1 = _mem.ReadUInt(popupBase);
            if (ptr1 != 0u)
            {
                string directFromPtr1 = ReadAnsiString(ptr1, 100);
                if (!string.IsNullOrEmpty(directFromPtr1) && IsAsciiIdentifier(directFromPtr1))
                    return directFromPtr1;

                uint ptr2 = _mem.ReadUInt(ptr1);
                if (ptr2 != 0u)
                {
                    string fromPtr2 = ReadAnsiString(ptr2, 100);
                    if (!string.IsNullOrEmpty(fromPtr2))
                        return fromPtr2;
                }
            }

            return ReadAnsiString(popupBase, 100);
        }

        private static bool IsAsciiIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= 'A' && c <= 'Z') ||
                          (c >= 'a' && c <= 'z') ||
                          (c >= '0' && c <= '9') ||
                          c == '_' || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        public PendingDialogInvite ReadPendingInvite()
        {
            var countNode = _mem.GetNode("dialog_count");
            uint countAddr = _mem.GetNodeBaseAddress(countNode);
            if (countAddr == 0u || _mem.ReadUInt(countAddr) == 0u)
                return null;

            var popupNode = _mem.GetNode("msg_dialog_popup_base");
            uint popupPtrAddr = _mem.GetNodeBaseAddress(popupNode);
            uint popupBase = popupPtrAddr != 0u ? _mem.ReadUInt(popupPtrAddr) : 0u;
            if (!MemoryProcess.IsValidPointer(popupBase))
                return null;

            uint playerIdOff = GetNodeOffset("msg_dialog_player_id");
            uint timeHitOff = GetNodeOffset("msg_dialog_time_hit");

            uint playerId = _mem.ReadUInt(unchecked(popupBase + playerIdOff));
            uint timeHit = _mem.ReadUInt(unchecked(popupBase + timeHitOff));
            string dialogType = ReadDialogTypeString(popupBase);

            return new PendingDialogInvite
            {
                DialogType = dialogType,
                PlayerId = playerId,
                TimeHit = timeHit
            };
        }

        public void Execute(uint leaderId, uint timeHit)
        {
            if (leaderId == 0u || timeHit == 0u)
                return;

            var vars = new Dictionary<string, uint>
            {
                { "LEADER_ID", leaderId },
                { "TIME_HIT", timeHit }
            };

            _asm.RunScript(_mem, ResolveScriptPath(), vars);
        }

        public bool TryAcceptGroupInvite(ICollection<uint> allowedLeaderIds = null)
        {
            var invite = ReadPendingInvite();
            if (invite == null || !invite.IsGroupInvite)
                return false;

            if (invite.PlayerId == 0u || invite.TimeHit == 0u || invite.TimeHit == _lastAcceptedTimeHit)
                return false;

            if (allowedLeaderIds != null && allowedLeaderIds.Count > 0 && !allowedLeaderIds.Contains(invite.PlayerId))
                return false;

            _lastAcceptedTimeHit = invite.TimeHit;
            Execute(invite.PlayerId, invite.TimeHit);
            return true;
        }
    }

    public partial class PlayerCore
    {
        private InviteParty _inviteParty;
        private AcceptParty _acceptParty;

        public InviteParty InvitePartyModule
        {
            get
            {
                if (_inviteParty == null)
                    _inviteParty = new InviteParty(_mem, _asm);
                return _inviteParty;
            }
        }

        public AcceptParty AcceptPartyModule
        {
            get
            {
                if (_acceptParty == null)
                    _acceptParty = new AcceptParty(_mem, _asm);
                return _acceptParty;
            }
        }

        public void InvitePlayerToParty(uint targetPlayerId)
        {
            InvitePartyModule.Execute(targetPlayerId);
        }

        public PendingDialogInvite GetPendingDialogInvite()
        {
            return AcceptPartyModule.ReadPendingInvite();
        }

        public void AcceptPartyInvite(uint leaderId, uint timeHit)
        {
            AcceptPartyModule.Execute(leaderId, timeHit);
        }

        public bool TryAcceptPendingPartyInvite(ICollection<uint> allowedLeaderIds = null)
        {
            return AcceptPartyModule.TryAcceptGroupInvite(allowedLeaderIds);
        }
    }
}