using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public TreeConfigRoot Config { get; }

        public MemoryProcess(Process process, TreeConfigRoot config)
        {
            Process = process ?? throw new ArgumentNullException(nameof(process));
            Config = config ?? throw new ArgumentNullException(nameof(config));

            Handle = OpenProcess(ProcessAllAccess, false, process.Id);
            if (Handle == IntPtr.Zero)
                throw new InvalidOperationException($"Falha ao abrir processo '{process.ProcessName}' (PID {process.Id}).");

            if (process.MainModule == null)
                throw new InvalidOperationException($"MainModule indisponível para o processo '{process.ProcessName}' (PID {process.Id}).");

            ModuleBase = unchecked((uint)process.MainModule.BaseAddress.ToInt64());
        }

        // --- ACESSO A NÓS DA ÁRVORE ---
        public OffsetNode GetNode(string key) => Config.FindByKey(key);
        public OffsetNode GetNodeByPath(string path) => Config.FindByPath(path);

        public uint GetNodeBaseAddress(OffsetNode node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.BaseAddress))
                return 0;

            var (_, rva) = ConfigManager.SplitModuleAndRva(node.BaseAddress);
            return ModuleBase + rva;
        }

        public uint GetNodeAddressByKey(string key)
        {
            var node = GetNode(key);
            if (node == null) return 0;

            uint baseAddr = GetNodeBaseAddress(node);
            if (baseAddr == 0) return 0;

            var offsets = node.GetOffsetsUInt();
            if (offsets.Length == 0)
                return ReadUInt(baseAddr);

            return ResolvePointer(baseAddr, offsets);
        }

        public uint GetFunctionAddressByKey(string key)
        {
            var node = GetNode(key);
            if (node == null) return 0;
            return GetNodeBaseAddress(node);
        }

        public uint GetFirstLocalOffset(string key, uint fallback = 0)
        {
            var node = GetNode(key);
            var local = node?.GetLocalOffsetsUInt();
            if (local == null || local.Length == 0) return fallback;
            return local[0];
        }
        // --- LEITURAS DE MEMÓRIA ---
        public byte ReadByte(uint addr)
        {
            if (addr == 0) return 0;
            byte[] buf = new byte[1];
            return ReadProcessMemory(Handle, addr, buf, 1, out int read) && read == 1 ? buf[0] : (byte)0;
        }

        public ushort ReadUShort(uint addr)
        {
            if (addr == 0) return 0;
            byte[] buf = new byte[2];
            return ReadProcessMemory(Handle, addr, buf, 2, out int read) && read == 2 ? BitConverter.ToUInt16(buf, 0) : (ushort)0;
        }

        public uint ReadUInt(uint addr)
        {
            if (addr == 0) return 0;
            byte[] buf = new byte[4];
            return ReadProcessMemory(Handle, addr, buf, 4, out int read) && read == 4 ? BitConverter.ToUInt32(buf, 0) : 0u;
        }

        public float ReadFloat(uint addr)
        {
            if (addr == 0) return 0f;
            byte[] buf = new byte[4];
            return ReadProcessMemory(Handle, addr, buf, 4, out int read) && read == 4 ? BitConverter.ToSingle(buf, 0) : 0f;
        }

        public string ReadUnicodeString(uint addr, int maxChars = 64)
        {
            if (addr == 0 || maxChars <= 0) return string.Empty;
            byte[] buf = new byte[maxChars * 2];
            if (!ReadProcessMemory(Handle, addr, buf, buf.Length, out int read) || read <= 0)
                return string.Empty;

            string str = Encoding.Unicode.GetString(buf, 0, read);
            int nullIdx = str.IndexOf('\0');
            return nullIdx >= 0 ? str.Substring(0, nullIdx) : str;
        }

        public string ReadNodeString(OffsetNode node, int maxChars = 64, bool derefFinalPointer = false, params uint[] arrayIndices)
        {
            if (node == null) return string.Empty;
            uint addr = ResolveNodeAddress(node, arrayIndices);
            if (addr == 0) return string.Empty;

            if (derefFinalPointer)
                addr = ReadUInt(addr);

            if (addr == 0) return string.Empty;
            return ReadUnicodeString(addr, maxChars);
        }

        public uint ResolvePointer(uint startAddr, params uint[] offsets)
        {
            if (startAddr == 0) return 0;
            uint addr = ReadUInt(startAddr);
            if (offsets == null) return addr;

            for (int i = 0; i < offsets.Length; i++)
            {
                if (addr == 0) return 0;
                addr = ReadUInt(addr + offsets[i]);
            }
            return addr;
        }

        // Resolve o endereço final de um nó considerando cadeias de ponteiros e fórmulas de array
        public uint ResolveNodeAddress(OffsetNode node, params uint[] arrayIndices)
        {
            if (node == null) return 0;

            uint basePtrAddr = GetNodeBaseAddress(node);
            if (basePtrAddr == 0) return 0;

            var offsets = node.GetOffsetsUInt();
            if (offsets.Length == 0)
                return ReadUInt(basePtrAddr);

            uint current = ReadUInt(basePtrAddr);
            int formulaIdx = 0;

            for (int i = 0; i < offsets.Length; i++)
            {
                if (current == 0) return 0;

                bool isLast = (i == offsets.Length - 1);
                uint stepOffset = offsets[i];

                if (isLast && node.IsArray && node.FormulaSteps != null && node.FormulaSteps.Count > 0)
                {
                    uint idx = (arrayIndices != null && formulaIdx < arrayIndices.Length) ? arrayIndices[formulaIdx] : 0u;
                    formulaIdx++;
                    uint evaluated = node.EvaluateFormula(idx);
                    return current + stepOffset + evaluated;
                }

                if (!isLast)
                    current = ReadUInt(current + stepOffset);
                else
                    return current + stepOffset;
            }

            return current;
        }
        // --- EXPORTA VARIÁVEIS PARA OS SCRIPTS .ASM A PARTIR DA ÁRVORE ---
        public Dictionary<string, uint> GetAsmVariables()
        {
            var vars = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

            var baseNode = GetNode("base_address") ?? GetNode("base_ptr_raw");
            if (baseNode != null)
            {
                vars["BASE_ADDR"] = GetNodeBaseAddress(baseNode);

                if (baseNode.Children != null && baseNode.Children.TryGetValue("struct_base", out var structBase))
                {
                    var sOffs = structBase.GetLocalOffsetsUInt();
                    if (sOffs.Length > 0) vars["OFF_GAMERUNOFFSET"] = sOffs[0];

                    if (structBase.Children != null)
                    {
                        if (structBase.Children.TryGetValue("player_struct", out var playerStruct))
                        {
                            var pOffs = playerStruct.GetLocalOffsetsUInt();
                            if (pOffs.Length > 0) vars["OFF_HOSTPLAYEROFFSET"] = pOffs[0];

                            if (playerStruct.Children != null &&
                                playerStruct.Children.TryGetValue("player_target_id", out var targetIdNode))
                            {
                                var tOffs = targetIdNode.GetLocalOffsetsUInt();
                                if (tOffs.Length > 0) vars["OFF_PLAYER_TARGETID"] = tOffs[0];
                            }
                        }

                        if (structBase.Children.TryGetValue("world_base", out var worldBase))
                        {
                            var wOffs = worldBase.GetLocalOffsetsUInt();
                            if (wOffs.Length > 0) vars["OFF_WORLDOFFSET"] = wOffs[0];
                        }
                    }
                }
            }

            var netNode = GetNode("network");
            if (netNode != null)
            {
                var nOffs = netNode.GetLocalOffsetsUInt();
                if (nOffs.Length > 0) vars["OFF_NETWORKOFFSET"] = nOffs[0];
            }

            var dialogTimeNode = GetNode("dialog_time_hit");
            if (dialogTimeNode != null)
            {
                var dOffs = dialogTimeNode.GetLocalOffsetsUInt();
                if (dOffs.Length > 0) vars["OFF_DIALOG_TIME_HIT"] = dOffs[0];
            }

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