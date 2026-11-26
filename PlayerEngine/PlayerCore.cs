using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Full_Bot
{
    public partial class PlayerCore
    {
        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;

        private PlayerBuffs _buffs;
        private PlayerFriends _friends;
        private PlayerInventory _inventory;
        private PartyCore _partyCore;
        private PlayerPets _pets;
        private PlayerSkills _skills;
        private WorldEntities _world;
        private TasksQuestActive _activeQuests;
        private TasksQuestNews _newsQuests;

        public PlayerCore(MemoryProcess mem, AsmRunner asm)
        {
            _mem = mem ?? throw new ArgumentNullException(nameof(mem));
            _asm = asm ?? throw new ArgumentNullException(nameof(asm));
        }

        public void AttachModules(
            PlayerBuffs buffs = null,
            PlayerFriends friends = null,
            PlayerInventory inventory = null,
            PartyCore partyCore = null,
            PlayerPets pets = null,
            PlayerSkills skills = null,
            WorldEntities world = null,
            TasksQuestActive activeQuests = null,
            TasksQuestNews newsQuests = null)
        {
            _buffs = buffs ?? _buffs;
            _friends = friends ?? _friends;
            _inventory = inventory ?? _inventory;
            _partyCore = partyCore ?? _partyCore;
            _pets = pets ?? _pets;
            _skills = skills ?? _skills;
            _world = world ?? _world;
            _activeQuests = activeQuests ?? _activeQuests;
            _newsQuests = newsQuests ?? _newsQuests;
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
                if (baseAddr == 0u) return 0u;

                uint gameRunOff = _mem.GetFirstLocalOffset("struct_base");
                uint hostPlayerOff = _mem.GetFirstLocalOffset("player_struct");
                if (gameRunOff == 0u && hostPlayerOff == 0u) return 0u;

                return _mem.ResolvePointer(baseAddr, gameRunOff, hostPlayerOff);
            }
        }

        // --- ATRIBUTOS VITAIS ---
        public string Name
        {
            get
            {
                int pid = _mem.Process != null ? _mem.Process.Id : 0;
                uint pBase = Base;
                if (pBase == 0) return pid > 0 ? $"PID: {pid} (Deslogado)" : "Deslogado";

                string n = _mem.ReadUnicodeStringByNode("player_name", 32);
                if (string.IsNullOrWhiteSpace(n))
                    return pid > 0 ? $"PID: {pid}" : "Sem Nome";

                return n;
            }
        }

        public string CurrentRegionName => _mem.ReadUnicodeStringByNode("current_location", 64);

        public uint Id => _mem.ReadUIntFromNodeAtBase(Base, "player_id");
        public uint Level => _mem.ReadUIntFromNodeAtBase(Base, "player_level");
        public uint HP => _mem.ReadUIntFromNodeAtBase(Base, "player_hp");
        public uint HPMax => _mem.ReadUIntFromNodeAtBase(Base, "player_hp_max");
        public uint MP => _mem.ReadUIntFromNodeAtBase(Base, "player_mp");
        public uint MPMax => _mem.ReadUIntFromNodeAtBase(Base, "player_mp_max");
        public uint DefFisica => _mem.ReadUIntFromNodeAtBase(Base, "player_phys_def");
        public ushort Idle => _mem.ReadUShortFromNodeAtBase(Base, "player_idle");
        public ushort ActionsCount => _mem.ReadUShortFromNodeAtBase(Base, "player_actions_count");
        public uint LoggedTime => _mem.ReadUIntFromNodeAtBase(Base, "player_logged_time");
        public uint MoveMode => _mem.ReadUIntFromNodeAtBase(Base, "player_move_mode");
        public float SpeedWalk => _mem.ReadFloatFromNodeAtBase(Base, "player_speed_walk");
        public float SpeedSwim => _mem.ReadFloatFromNodeAtBase(Base, "player_speed_swim");
        public float SpeedFly => _mem.ReadFloatFromNodeAtBase(Base, "player_speed_fly");

        // --- COORDENADAS NATIVAS BRUTAS ---
        public float X => _mem.ReadFloatFromNodeAtBase(Base, "player_pos_x");
        public float Y => _mem.ReadFloatFromNodeAtBase(Base, "player_pos_y");
        public float Z => _mem.ReadFloatFromNodeAtBase(Base, "player_pos_z");

        public static float ToDisplayX(float rawX) => (rawX / 10f) + 400f;
        public static float ToDisplayY(float rawY) => rawY / 10f;
        public static float ToDisplayZ(float rawZ) => (rawZ / 10f) + 550f;

        // --- ACESSO UNIFICADO AOS MÓDULOS DO PLAYER ---
        public List<ActiveQuestInfo> GetActiveQuests()
            => _activeQuests?.GetActiveQuestTree() ?? new List<ActiveQuestInfo>();

        public List<ActiveQuestInfo> GetAllActiveQuestsFlat()
            => _activeQuests?.GetActiveQuests() ?? new List<ActiveQuestInfo>();

        public List<AvailableQuestInfo> GetNewsQuests(int maxEntries = 60)
            => _newsQuests?.GetAvailableQuests(maxEntries) ?? new List<AvailableQuestInfo>();

        public List<FriendInfo> GetFriends()
            => _friends?.GetFriends() ?? new List<FriendInfo>();

        public bool IsInParty()
            => _partyCore != null && _partyCore.IsInParty();

        public List<PartyMemberInfo> GetPartyMembers()
            => _partyCore?.GetMembers() ?? new List<PartyMemberInfo>();

        public bool IsPetSummoned => _pets != null && _pets.IsPetSummoned;

        public List<PetSlotInfo> GetPets()
            => _pets?.GetPetSlots() ?? new List<PetSlotInfo>();

        public List<InventoryItemInfo> GetBagItems()
            => _inventory?.GetBagItems() ?? new List<InventoryItemInfo>();

        public List<InventoryItemInfo> GetEquippedItems()
            => _inventory?.GetEquippedItems() ?? new List<InventoryItemInfo>();

        public List<InventoryItemInfo> GetQuestItems()
            => _inventory?.GetQuestItems() ?? new List<InventoryItemInfo>();

        public List<BuffInfo> GetBuffs()
            => _buffs?.GetActiveBuffs() ?? new List<BuffInfo>();

        public List<SkillInfo> GetSkills()
            => _skills?.GetLearnedSkills() ?? new List<SkillInfo>();

        // --- ENTIDADES AO REDOR ---
        public List<WorldEntityInfo> GetNearbyPlayers()
        {
            var list = _world?.GetNearbyPlayers() ?? new List<WorldEntityInfo>();
            uint selfId = Id;
            return selfId == 0u ? list : list.Where(p => p.ServerId != selfId).ToList();
        }

        public List<WorldEntityInfo> GetNearbyNpcs()
            => (_world?.GetMobsAndNpcs() ?? new List<WorldEntityInfo>())
                .Where(e => e.Type == 7)
                .ToList();

        public List<WorldEntityInfo> GetNearbyMobs()
            => (_world?.GetMobsAndNpcs() ?? new List<WorldEntityInfo>())
                .Where(e => e.Type == 6)
                .ToList();

        public List<DropItemInfo> GetNearbyMatts()
            => _world?.GetGroundDrops() ?? new List<DropItemInfo>();

        // --- PROPRIEDADES REATIVAS (GET LÊ MEMÓRIA / SET DISPARA AÇÃO) ---
        public uint TargetId
        {
            get => _mem.ReadUIntFromNodeAtBase(Base, "player_target_id");
            set => SelectTarget(value);
        }

        public static string FormatHex32(uint value)
        {
            return "0x" + value.ToString("X8", CultureInfo.InvariantCulture);
        }

        public void SelectTarget(uint serverId)
        {
            if (serverId == 0) return;

            var confirmNode = _mem.GetNode("player_confirm_target_id");
            uint confirmOff = (confirmNode != null && confirmNode.GetLocalOffsetsUInt().Length > 0)
                ? confirmNode.GetLocalOffsetsUInt()[0]
                : 0u;

            uint hexAsUint = uint.Parse(serverId.ToString("X8", CultureInfo.InvariantCulture), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            var vars = new Dictionary<string, uint>
            {
                { "TARGET_ID", hexAsUint },
                { "OPCODE_TARGET", 0x02u },
                { "OFF_PLAYER_CONFIRMTARGETID", confirmOff }
            };
            _asm.RunScript(_mem, @"Scripts\SelectTarget.asm", vars);
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