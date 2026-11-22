using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Full_Bot
{
    public class MemoryProcess : IDisposable
    {
        private const int ProcessAllAccess = 0x1F0FFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, uint lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public Process Process { get; }
        public IntPtr Handle { get; private set; }
        public uint ModuleBase { get; }
        public CheatTableDocument Table { get; }

        public MemoryProcess(Process process, CheatTableDocument table)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            Table = table ?? throw new ArgumentNullException(nameof(table));

            Handle = OpenProcess(ProcessAllAccess, false, process.Id);
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException($"Falha ao abrir processo '{process.ProcessName}' (PID {process.Id}).");

            if (process.MainModule == null)
                throw new InvalidOperationException($"MainModule indisponível para o processo '{process.ProcessName}' (PID {process.Id}).");

            ModuleBase = unchecked((uint)process.MainModule.BaseAddress.ToInt64());
        }

        // --- ACESSO A NÓS DA TABELA .CT ---
        public CtEntryNode GetNodeByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || Table?.Entries == null)
                return null;

            string[] parts = path.Trim().Split(new[] { '/', '\\', '>' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return null;

            string targetName = parts[parts.Length - 1].Trim();
            foreach (var entry in Table.Entries)
            {
                if (entry == null) continue;
                if (string.Equals(entry.Key, targetName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.BaseName, targetName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Description, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length == 1)
                        return entry;

                    var cur = entry.Parent;
                    int idx = parts.Length - 2;
                    bool match = true;

                    while (idx >= 0)
                    {
                        if (cur == null ||
                            (!string.Equals(cur.Key, parts[idx].Trim(), StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(cur.BaseName, parts[idx].Trim(), StringComparison.OrdinalIgnoreCase)))
                        {
                            match = false;
                            break;
                        }
                        cur = cur.Parent;
                        idx--;
                    }

                    if (match)
                        return entry;
                }
            }

            return null;
        }
        public CtEntryNode GetNode(string key) => Table?.Find(key);

        public uint GetModuleBaseAddress(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return ModuleBase;

            string cleanMain = (Process.MainModule?.ModuleName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(cleanMain) &&
                string.Equals(cleanMain, moduleName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return ModuleBase;
            }

            if (Process.Modules != null)
            {
                foreach (ProcessModule mod in Process.Modules)
                {
                    if (mod != null && string.Equals(mod.ModuleName, moduleName.Trim(), StringComparison.OrdinalIgnoreCase))
                        return unchecked((uint)mod.BaseAddress.ToInt64());
                }
            }

            return ModuleBase;
        }

        public uint GetNodeBaseAddress(CtEntryNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.BaseAddress))
                return 0u;

            string raw = node.BaseAddress.Trim();
            int plusIdx = raw.IndexOf('+');
            if (plusIdx < 0)
                return ParseHexUInt(raw);

            string modName = raw.Substring(0, plusIdx).Trim();
            string rvaHex = raw.Substring(plusIdx + 1).Trim();
            uint modBase = GetModuleBaseAddress(modName);
            if (modBase == 0u) return 0u;

            return unchecked(modBase + ParseHexUInt(rvaHex));
        }

        public uint GetFunctionAddressByKey(string key)
        {
            var node = GetNode(key);
            if (node == null) return 0u;
            return GetNodeBaseAddress(node);
        }

        public uint GetFirstLocalOffset(string key, uint fallback = 0u)
        {
            var node = GetNode(key);
            if (node == null) return fallback;

            if (node.LocalOffsets != null && node.LocalOffsets.Length > 0)
                return node.LocalOffsets[0];

            if (node.HasOwnAddressDisplacement)
                return node.OwnAddressDisplacement;

            return fallback;
        }

        public uint GetNodeAddressByKey(string key, params uint[] arrayIndices)
        {
            var node = GetNode(key);
            if (node == null) return 0u;
            return ResolveNodeAddress(node, dereferenceLastPointer: true, arrayIndices: arrayIndices);
        }

        // --- LEITURAS DE MEMÓRIA ---
        public byte ReadByte(uint addr)
        {
            if (addr == 0u) return 0;
            byte[] buf = new byte[1];
            return ReadProcessMemory(Handle, addr, buf, 1, out int read) && read == 1 ? buf[0] : (byte)0;
        }

        public ushort ReadUShort(uint addr)
        {
            if (addr == 0u) return 0;
            byte[] buf = new byte[2];
            return ReadProcessMemory(Handle, addr, buf, 2, out int read) && read == 2 ? BitConverter.ToUInt16(buf, 0) : (ushort)0;
        }

        public uint ReadUInt(uint addr)
        {
            if (addr == 0u) return 0u;
            byte[] buf = new byte[4];
            return ReadProcessMemory(Handle, addr, buf, 4, out int read) && read == 4 ? BitConverter.ToUInt32(buf, 0) : 0u;
        }

        public float ReadFloat(uint addr)
        {
            if (addr == 0u) return 0f;
            byte[] buf = new byte[4];
            return ReadProcessMemory(Handle, addr, buf, 4, out int read) && read == 4 ? BitConverter.ToSingle(buf, 0) : 0f;
        }

        public string ReadUnicodeString(uint addr, int maxChars = 64)
        {
            if (addr == 0u || maxChars <= 0) return string.Empty;
            byte[] buf = new byte[maxChars * 2];
            if (!ReadProcessMemory(Handle, addr, buf, buf.Length, out int read) || read <= 0)
                return string.Empty;

            string str = Encoding.Unicode.GetString(buf, 0, read);
            int nullIdx = str.IndexOf('\0');
            return nullIdx >= 0 ? str.Substring(0, nullIdx) : str;
        }

        public string ReadNodeString(CtEntryNode node, int maxChars = 64, bool derefFinalPointer = false, params uint[] arrayIndices)
        {
            if (node == null) return string.Empty;
            uint addr = ResolveNodeAddress(node, dereferenceLastPointer: !derefFinalPointer, arrayIndices: arrayIndices);
            if (addr == 0u) return string.Empty;

            if (derefFinalPointer)
                addr = ReadUInt(addr);

            if (addr == 0u) return string.Empty;
            return ReadUnicodeString(addr, maxChars);
        }

        public uint ResolvePointer(uint startAddr, params uint[] offsets)
        {
            if (startAddr == 0u) return 0u;
            uint addr = ReadUInt(startAddr);
            if (offsets == null) return addr;

            for (int i = 0; i < offsets.Length; i++)
            {
                if (addr == 0u) return 0u;
                addr = ReadUInt(unchecked(addr + offsets[i]));
            }
            return addr;
        }

        /// <summary>
        /// Resolve o endereço final de um nó na árvore do .CT, respeitando:
        /// - deslocamento próprio de <Address> (+0, +D, +11, etc.)
        /// - cadeia de <Offsets>
        /// - fórmulas em qualquer nível da linhagem
        /// - opção de não desreferenciar o último passo quando o alvo já é o endereço do dado/estrutura
        /// </summary>
        public uint ResolveNodeAddress(CtEntryNode node, bool dereferenceLastPointer = true, params uint[] arrayIndices)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.BaseAddress))
                return 0u;

            var chainNodes = new List<CtEntryNode>();
            var cur = node;
            while (cur != null)
            {
                chainNodes.Add(cur);
                cur = cur.Parent;
            }
            chainNodes.Reverse();

            int startIdx = 0;
            for (int i = chainNodes.Count - 1; i > 0; i--)
            {
                string prevBase = chainNodes[i - 1].BaseAddress ?? string.Empty;
                string currBase = chainNodes[i].BaseAddress ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(currBase) &&
                    !string.Equals(currBase, prevBase, StringComparison.OrdinalIgnoreCase))
                {
                    startIdx = i;
                    break;
                }
            }

            uint basePtrAddr = GetNodeBaseAddress(chainNodes[startIdx]);
            if (basePtrAddr == 0u)
                return 0u;

            uint current = ReadUInt(basePtrAddr);
            if (current == 0u)
                return 0u;

            int formulaCursor = 0;

            for (int i = startIdx; i < chainNodes.Count; i++)
            {
                var n = chainNodes[i];
                bool isTargetNode = (i == chainNodes.Count - 1);

                uint idxVal = 0u;
                if (n.HasFormula && arrayIndices != null && formulaCursor < arrayIndices.Length)
                    idxVal = arrayIndices[formulaCursor++];

                bool hasOffsets = n.LocalOffsets != null && n.LocalOffsets.Length > 0;

                if (n.HasOwnAddressDisplacement)
                {
                    uint disp = n.OwnAddressDisplacement;
                    if (n.HasFormula && !hasOffsets)
                        disp = unchecked(disp + n.EvalFormula(idxVal));

                    if (!hasOffsets)
                    {
                        uint directAddr = unchecked(current + disp);
                        if (isTargetNode && !dereferenceLastPointer)
                            return directAddr;

                        current = ReadUInt(directAddr);
                        if (current == 0u)
                            return 0u;
                        continue;
                    }
                    else
                    {
                        current = unchecked(current + disp);
                    }
                }

                if (!hasOffsets)
                {
                    if (n.HasFormula)
                        current = unchecked(current + n.EvalFormula(idxVal));
                    continue;
                }

                for (int o = 0; o < n.LocalOffsets.Length; o++)
                {
                    uint off = n.LocalOffsets[o];
                    bool isLastOffsetOfTarget = isTargetNode && (o == n.LocalOffsets.Length - 1);

                    if (n.HasFormula && o == n.LocalOffsets.Length - 1)
                        off = unchecked(off + n.EvalFormula(idxVal));

                    uint targetAddr = unchecked(current + off);

                    if (isLastOffsetOfTarget && !dereferenceLastPointer)
                        return targetAddr;

                    current = ReadUInt(targetAddr);
                    if (current == 0u)
                        return 0u;
                }
            }

            return current;
        }

        // --- EXPORTA VARIÁVEIS PARA OS SCRIPTS .ASM A PARTIR DA TABELA .CT ---
        public Dictionary<string, uint> GetAsmVariables()
        {
            var vars = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

            var baseNode = GetNode("base_address") ?? GetNode("base_ptr_raw");
            if (baseNode != null)
            {
                vars["BASE_ADDR"] = GetNodeBaseAddress(baseNode);

                var structBase = FindChildByKey(baseNode, "struct_base");
                if (structBase != null)
                {
                    if (structBase.LocalOffsets.Length > 0)
                        vars["OFF_GAMERUNOFFSET"] = structBase.LocalOffsets[0];

                    var playerStruct = FindChildByKey(structBase, "player_struct");
                    if (playerStruct != null)
                    {
                        if (playerStruct.LocalOffsets.Length > 0)
                            vars["OFF_HOSTPLAYEROFFSET"] = playerStruct.LocalOffsets[0];

                        var targetIdNode = FindChildByKey(playerStruct, "player_target_id");
                        if (targetIdNode != null && targetIdNode.LocalOffsets.Length > 0)
                            vars["OFF_PLAYER_TARGETID"] = targetIdNode.LocalOffsets[0];
                    }

                    var worldBase = FindChildByKey(structBase, "world_base");
                    if (worldBase != null && worldBase.LocalOffsets.Length > 0)
                        vars["OFF_WORLDOFFSET"] = worldBase.LocalOffsets[0];
                }
            }

            var netNode = GetNode("network");
            if (netNode != null && netNode.LocalOffsets.Length > 0)
                vars["OFF_NETWORKOFFSET"] = netNode.LocalOffsets[0];

            var dialogTimeNode = GetNode("dialog_time_hit");
            if (dialogTimeNode != null && dialogTimeNode.LocalOffsets.Length > 0)
                vars["OFF_DIALOG_TIME_HIT"] = dialogTimeNode.LocalOffsets[0];

            AddNodeAddressVar(vars, "DIALOG_POPUP_BASE", "dialog_popup_base");
            AddNodeAddressVar(vars, "DIALOG_COUNT", "dialog_count");
            AddNodeAddressVar(vars, "PACKET_MGR", "var_template_sendpack");
            AddNodeAddressVar(vars, "HEAP_MGR", "var_template_sendpack");
            AddNodeAddressVar(vars, "UNFREEZER", "var_unfreezer");

            AddFuncAddressVar(vars, "FUNC_ALLOCBLOCK", "fn_mem_alloc");
            AddNodeAddressVar(vars, "NAV_HEAP_MGR", "nav_heap_mgr");
            AddFuncAddressVar(vars, "FUNC_ALLOCPACKET", "fn_mem_alloc_sendpack");
            AddFuncAddressVar(vars, "FUNC_SENDPACKET", "fn_send_packet");
            AddFuncAddressVar(vars, "FUNC_FREEPACKET", "fn_mem_free_sendpack");
            AddFuncAddressVar(vars, "FUNC_MOVETO", "fn_move_to");
            AddFuncAddressVar(vars, "FUNC_TOGGLEFLY", "fn_toggle_fly");
            AddFuncAddressVar(vars, "FUNC_SELECTTARGET", "fn_select_target");
            AddFuncAddressVar(vars, "FUNC_AUTOATTACK", "fn_auto_attack");
            AddFuncAddressVar(vars, "FUNC_PICKUPITEM", "fn_pickup_item");
            AddFuncAddressVar(vars, "FUNC_INVITEPARTY", "fn_send_packet");
            AddFuncAddressVar(vars, "FUNC_MOVETOFLYALT", "fn_move_to");

            return vars;
        }

        private static CtEntryNode FindChildByKey(CtEntryNode parent, string key)
        {
            if (parent == null || parent.Children == null) return null;
            foreach (var child in parent.Children)
            {
                if (child == null) continue;
                if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(child.BaseName, key, StringComparison.OrdinalIgnoreCase))
                    return child;

                var nested = FindChildByKey(child, key);
                if (nested != null) return nested;
            }
            return null;
        }

        private void AddNodeAddressVar(Dictionary<string, uint> vars, string varName, string nodeKey)
        {
            var node = GetNode(nodeKey);
            if (node != null)
                vars[varName] = GetNodeBaseAddress(node);
        }

        private void AddFuncAddressVar(Dictionary<string, uint> vars, string varName, string nodeKey)
        {
            var node = GetNode(nodeKey);
            if (node != null)
                vars[varName] = GetNodeBaseAddress(node);
        }

        private static uint ParseHexUInt(string raw)
        {
            string s = raw.Trim();
            if (s.StartsWith("+") || s.StartsWith("-"))
                s = s.Substring(1).Trim();
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);

            return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                CloseHandle(Handle);
                Handle = IntPtr.Zero;
            }
        }
    }
}