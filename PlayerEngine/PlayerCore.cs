using System;
using System.Collections.Generic;

namespace Full_Bot
{
    public class PlayerCore
    {
        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        public PlayerCore(MemoryProcess mem, AsmRunner asm)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
        }

        public uint Base
        {
            get
            {
                var playerNode = _mem.GetNode("player_struct");
                if (playerNode != null)
                {
                    var offs = playerNode.GetOffsetsUInt();
                    if (offs.Length > 0)
                        return _mem.ResolvePointer(_mem.GetNodeBaseAddress(playerNode), offs);
                }

                var baseNode = _mem.GetNode("base_address") ?? _mem.GetNode("base_ptr_raw");
                uint baseAddr = _mem.GetNodeBaseAddress(baseNode);
                uint gameRunOff = _mem.GetFirstLocalOffset("struct_base", 0x1C);
                uint hostPlayerOff = _mem.GetFirstLocalOffset("player_struct", 0x2C);
                return _mem.ResolvePointer(baseAddr, gameRunOff, hostPlayerOff);
            }
        }

        private uint ReadPlayerUInt(string nodeKey, string altPath = null)
        {
            uint pBase = Base;
            if (pBase == 0) return 0;

            var node = _mem.GetNode(nodeKey) ?? (!string.IsNullOrWhiteSpace(altPath) ? _mem.GetNodeByPath(altPath) : null);
            var local = node?.GetLocalOffsetsUInt();
            if (local == null || local.Length == 0) return 0;

            return _mem.ReadUInt(pBase + local[0]);
        }

        private ushort ReadPlayerUShort(string nodeKey, string altPath = null)
        {
            uint pBase = Base;
            if (pBase == 0) return 0;

            var node = _mem.GetNode(nodeKey) ?? (!string.IsNullOrWhiteSpace(altPath) ? _mem.GetNodeByPath(altPath) : null);
            var local = node?.GetLocalOffsetsUInt();
            if (local == null || local.Length == 0) return 0;

            return _mem.ReadUShort(pBase + local[0]);
        }

        private float ReadPlayerFloat(string nodeKey, string altPath = null)
        {
            uint pBase = Base;
            if (pBase == 0) return 0f;

            var node = _mem.GetNode(nodeKey) ?? (!string.IsNullOrWhiteSpace(altPath) ? _mem.GetNodeByPath(altPath) : null);
            var local = node?.GetLocalOffsetsUInt();
            if (local == null || local.Length == 0) return 0f;

            return _mem.ReadFloat(pBase + local[0]);
        }

        // --- ATRIBUTOS VITAIS ---
        // --- ATRIBUTOS VITAIS ---
        public string Name
        {
            get
            {
                uint pBase = Base;
                if (pBase == 0) return $"PID: {_mem.Process.Id} (Deslogado)";

                var nameNode = _mem.GetNode("player_name");
                var local = nameNode?.GetLocalOffsetsUInt();
                if (local == null || local.Length == 0)
                    return $"PID: {_mem.Process.Id}";

                uint ptr = _mem.ReadUInt(pBase + local[0]);
                if (local.Length > 1 && local[1] != 0 && ptr != 0)
                    ptr = _mem.ReadUInt(ptr + local[1]);

                string n = _mem.ReadUnicodeString(ptr, 32);
                return string.IsNullOrWhiteSpace(n) ? $"PID: {_mem.Process.Id}" : n;
            }
        }

        public string CurrentRegionName
        {
            get
            {
                var locNode = _mem.GetNode("current_location");
                if (locNode == null) return string.Empty;

                uint strAddr = _mem.ResolveNodeAddress(locNode);
                return _mem.ReadUnicodeString(strAddr, 64);
            }
        }

        public uint Id => ReadPlayerUInt("player_id");
        public uint Level => ReadPlayerUInt("player_level");
        public uint HP => ReadPlayerUInt("player_hp");
        public uint HPMax => ReadPlayerUInt("player_hp_max");
        public uint MP => ReadPlayerUInt("player_mp");
        public uint MPMax => ReadPlayerUInt("player_mp_max");
        public uint DefFisica => ReadPlayerUInt("player_phys_def");
        public ushort Idle => ReadPlayerUShort("player_idle");
        public ushort ActionsCount => ReadPlayerUShort("player_actions_count");
        public uint LoggedTime => ReadPlayerUInt("player_logged_time");
        public uint MoveMode => ReadPlayerUInt("player_move_mode");
        public float SpeedWalk => ReadPlayerFloat("player_speed_walk");
        public float SpeedSwim => ReadPlayerFloat("player_speed_swim");
        public float SpeedFly => ReadPlayerFloat("player_speed_fly");

        // --- COORDENADAS NATIVAS BRUTAS ---
        public float X => ReadPlayerFloat("player_pos_x");
        public float Y => ReadPlayerFloat("player_pos_y");
        public float Z => ReadPlayerFloat("player_pos_z");

        // Conversão apenas para exibição visual na tela (minimapa)
        public static float ToDisplayX(float rawX) => (rawX / 10f) + 400f;
        public static float ToDisplayY(float rawY) => rawY / 10f;
        public static float ToDisplayZ(float rawZ) => (rawZ / 10f) + 550f;



        // --- PROPRIEDADES REATIVAS (GET LÊ MEMÓRIA / SET DISPARA AÇÃO) ---
        public uint TargetId
        {
            get => ReadPlayerUInt("player_target_id");
            set
            {
                if (value == TargetId) return;
                var confirmNode = _mem.GetNode("player_confirm_target_id");
                var vars = new Dictionary<string, uint>
                {
                    { "TARGET_ID", value },
                    { "OPCODE_TARGET", 0x02u },
                    { "OFF_PLAYER_CONFIRMTARGETID", confirmNode?.GetLocalOffsetsUInt().Length > 0 ? confirmNode.GetLocalOffsetsUInt()[0] : 0u }
                };
                _asm.RunScript(_mem, @"Scripts\SelectTarget.asm", vars);
            }
        }

        public float TargetX
        {
            get => X;
            set => MoveTo(value, Y, Z);
        }

        public float TargetZ
        {
            get => Z;
            set => MoveTo(X, Y, value);
        }

        // --- AÇÕES VIA ASM ---
        public void MoveTo(float rawX, float rawY, float rawZ, uint flyMode = 0)
        {
            var vars = new Dictionary<string, uint>
            {
                { "INT_X", unchecked((uint)Convert.ToInt32(rawX)) },
                { "INT_Y", unchecked((uint)Convert.ToInt32(rawY)) },
                { "INT_Z", unchecked((uint)Convert.ToInt32(rawZ)) },
                { "FLY_MODE", flyMode }
            };
            _asm.RunScript(_mem, @"Scripts\MoveTo.asm", vars);
        }

        public void SetFlyAlt(int altitude)
        {
            if (altitude < 0) altitude = 0;
            if (altitude > 69) altitude = 69;

            var vars = new Dictionary<string, uint> { { "FLY_ALT", (uint)altitude } };
            _asm.RunScript(_mem, @"Scripts\SetFlyAlt.asm", vars);
        }

        public void MoveToFly(float rawX, float rawZ, int altitude)
        {
            MoveTo(rawX, 0f, rawZ, flyMode: 60);
            SetFlyAlt(altitude);
        }

        public void CastSkill(uint skillId)
        {
            var vars = new Dictionary<string, uint> { { "SKILL_ID", skillId } };
            _asm.RunScript(_mem, @"Scripts\CastSkill.asm", vars);
        }

        public void AutoAttack()
        {
            _asm.RunScript(_mem, @"Scripts\AutoAttack.asm");
        }

        public void PickupItem(uint itemServerId, bool isMaterial = false)
        {
            var vars = new Dictionary<string, uint>
            {
                { "ITEM_SERVER_ID", itemServerId },
                { "IS_MAT", isMaterial ? 1u : 0u }
            };
            _asm.RunScript(_mem, @"Scripts\PickupItem.asm", vars);
        }
    }
}