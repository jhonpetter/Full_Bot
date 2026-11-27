using Full_Bot;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public enum MobStatusType : uint
    {
        Unknown = 0,
        Idle = 1,
        PhysicalAttack = 2,
        MagicAttack = 3,
        Dead = 4,
        Moving = 5
    }

    public class NpcServicesInfo
    {
        public uint NpcId { get; set; }
        public uint TemplateId { get; set; }
        public uint Sell { get; set; }
        public uint Buy { get; set; }
        public uint Repair { get; set; }
        public uint Install { get; set; }
        public uint Uninstall { get; set; }
        public uint Storage { get; set; }
        public uint Skill { get; set; }
        public uint Make { get; set; }
        public uint Teleport { get; set; }

        public bool HasSell => Sell != 0u;
        public bool HasBuy => Buy != 0u;
        public bool HasRepair => Repair != 0u;
        public bool HasInstall => Install != 0u;
        public bool HasUninstall => Uninstall != 0u;
        public bool HasStorage => Storage != 0u;
        public bool HasSkill => Skill != 0u;
        public bool HasMake => Make != 0u;
        public bool HasTeleport => Teleport != 0u;

        public bool HasAnyService =>
            HasSell || HasBuy || HasRepair || HasInstall || HasUninstall ||
            HasStorage || HasSkill || HasMake || HasTeleport;
    }

    public class WorldEntityInfo
    {
        public uint ServerId { get; set; }
        public uint TemplateId { get; set; }
        public string Name { get; set; }
        public uint Level { get; set; }
        public uint HP { get; set; }
        public uint HPMax { get; set; }
        public uint MP { get; set; }
        public uint MPMax { get; set; }
        public uint Type { get; set; }
        public uint MoveMode { get; set; }
        public uint Status { get; set; }
        public float Distance { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public NpcServicesInfo NpcServices { get; set; }

        public bool IsDead => Status == (uint)MobStatusType.Dead;
        public bool IsMoving => Status == (uint)MobStatusType.Moving;
        public bool IsNpc => Type == 7u;
    }

    public class DropItemInfo
    {
        public uint ServerId { get; set; }
        public uint ItemId { get; set; }
        public string Name { get; set; }
        public uint Type { get; set; }
        public float Distance { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public class FriendEntityInfo
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public uint Level { get; set; }
        public uint ClassId { get; set; }
    }

    public class WorldEntities
    {
        private const uint MaxMobsSafety = 500;
        private const uint MaxPlayersSafety = 300;
        private const uint MaxDropsSafety = 1000;
        private const uint MaxFriendsSafety = 300;
        private const int MaxPartyBuffSlotsSafety = 64;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _selfPlayer;

        public WorldEntities(MemoryProcess mem, PlayerCore selfPlayer = null)
        {
            _mem = mem;
            _selfPlayer = selfPlayer;
        }

        private string ReadEntityName(uint entityPtr, string nameNodeKey, int maxChars, uint templateId = 0, uint entityType = 0, bool useFallback = false)
        {
            if (_mem == null || !MemoryProcess.IsValidPointer(entityPtr))
                return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

            string raw = _mem.ReadUnicodeStringByChain(entityPtr, nameNodeKey, maxChars);
            if (string.IsNullOrWhiteSpace(raw))
                return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

            string cleaned = new string(raw.Where(ch => !char.IsControl(ch) && ch != '\uFFFD').ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

            return cleaned;
        }

        private static string FormatEntityFallbackName(uint templateId, uint entityType)
        {
            string prefix = entityType == 7 ? "NPC" : "Mob";
            return templateId > 0 ? $"{prefix} #{templateId}" : prefix;
        }

        private NpcServicesInfo ReadNpcServices(uint mobPtr, uint essenceOff, uint entityType)
        {
            if (_mem == null || entityType != 7u || essenceOff == 0u || !MemoryProcess.IsValidPointer(mobPtr))
                return null;

            uint essencePtr = _mem.ReadUInt(unchecked(mobPtr + essenceOff));
            if (!MemoryProcess.IsValidPointer(essencePtr))
                return null;

            uint npcIdOff = _mem.GetNodeOffset("mob_essence_npc_id");
            uint tplIdOff = _mem.GetNodeOffset("mob_essence_template_id");
            uint sellOff = _mem.GetNodeOffset("mob_essence_sell_service");
            uint buyOff = _mem.GetNodeOffset("mob_essence_buy_service");
            uint repairOff = _mem.GetNodeOffset("mob_essence_repair_service");
            uint installOff = _mem.GetNodeOffset("mob_essence_install_service");
            uint uninstallOff = _mem.GetNodeOffset("mob_essence_uninstall_service");
            uint storageOff = _mem.GetNodeOffset("mob_essence_storage_service");
            uint skillOff = _mem.GetNodeOffset("mob_essence_skill_service");
            uint makeOff = _mem.GetNodeOffset("mob_essence_make_service");
            uint teleportOff = _mem.GetNodeOffset("mob_essence_teleport_service");

            return new NpcServicesInfo
            {
                NpcId = npcIdOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + npcIdOff)) : 0u,
                TemplateId = tplIdOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + tplIdOff)) : 0u,
                Sell = sellOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + sellOff)) : 0u,
                Buy = buyOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + buyOff)) : 0u,
                Repair = repairOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + repairOff)) : 0u,
                Install = installOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + installOff)) : 0u,
                Uninstall = uninstallOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + uninstallOff)) : 0u,
                Storage = storageOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + storageOff)) : 0u,
                Skill = skillOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + skillOff)) : 0u,
                Make = makeOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + makeOff)) : 0u,
                Teleport = teleportOff > 0u ? _mem.ReadUInt(unchecked(essencePtr + teleportOff)) : 0u
            };
        }

        public List<WorldEntityInfo> GetMobsAndNpcs()
        {
            var list = new List<WorldEntityInfo>();
            if (_mem == null) return list;

            uint wBase = _mem.ResolveStructBase("world_base");
            if (!MemoryProcess.IsValidPointer(wBase)) return list;

            uint mobBase = _mem.ReadUIntFromNodeAtBase(wBase, "world_mobs_npc_base");
            if (!MemoryProcess.IsValidPointer(mobBase)) return list;

            uint count = _mem.ReadUIntFromNodeAtBase(mobBase, "world_mob_count");
            uint arrayPtr = _mem.ReadUIntFromNodeAtBase(mobBase, "world_mob_array");
            if (!MemoryProcess.IsValidPointer(arrayPtr) || count == 0u || count > MaxMobsSafety) return list;

            var slotNode = _mem.GetNode("world_mob_ptr-index-mul-4");
            if (slotNode == null) return list;

            uint serverIdOff = _mem.GetNodeOffset("mob_server_id");
            uint templateIdOff = _mem.GetNodeOffset("mob_id");
            uint lvlOff = _mem.GetNodeOffset("mob_level");
            uint hpOff = _mem.GetNodeOffset("mob_hp");
            uint hpMaxOff = _mem.GetNodeOffset("mob_hp_max");
            uint typeOff = _mem.GetNodeOffset("mob_type");
            uint moveModeOff = _mem.GetNodeOffset("mob_move_mode");
            uint statusOff = _mem.GetNodeOffset("mob_status");
            uint essenceOff = _mem.GetNodeOffset("mob_essence");
            uint distOff = _mem.GetNodeOffset("mob_distance");
            uint xOff = _mem.GetNodeOffset("mob_pos_x");
            uint yOff = _mem.GetNodeOffset("mob_pos_y");
            uint zOff = _mem.GetNodeOffset("mob_pos_z");

            for (uint i = 0u; i < count; i++)
            {
                uint mPtr = _mem.ReadUInt(unchecked(arrayPtr + slotNode.EvaluateFormula(i)));
                if (!MemoryProcess.IsValidPointer(mPtr)) continue;

                uint serverId = serverIdOff > 0u ? _mem.ReadUInt(unchecked(mPtr + serverIdOff)) : 0u;
                uint templateId = templateIdOff > 0u ? _mem.ReadUInt(unchecked(mPtr + templateIdOff)) : 0u;
                uint entityType = typeOff > 0u ? _mem.ReadUInt(unchecked(mPtr + typeOff)) : 0u;

                float x = xOff > 0u ? _mem.ReadFloat(unchecked(mPtr + xOff)) : 0f;
                float y = yOff > 0u ? _mem.ReadFloat(unchecked(mPtr + yOff)) : 0f;
                float z = zOff > 0u ? _mem.ReadFloat(unchecked(mPtr + zOff)) : 0f;

                float dist = distOff > 0u ? _mem.ReadFloat(unchecked(mPtr + distOff)) : 0f;
                if (dist <= 0.01f && _selfPlayer != null)
                {
                    float dx = x - _selfPlayer.X;
                    float dz = z - _selfPlayer.Z;
                    dist = (float)System.Math.Sqrt((dx * dx) + (dz * dz));
                }

                list.Add(new WorldEntityInfo
                {
                    ServerId = serverId,
                    TemplateId = templateId,
                    Name = ReadEntityName(mPtr, "mob_name", 32, templateId, entityType, useFallback: true),
                    Level = lvlOff > 0u ? _mem.ReadUInt(unchecked(mPtr + lvlOff)) : 0u,
                    HP = hpOff > 0u ? _mem.ReadUInt(unchecked(mPtr + hpOff)) : 0u,
                    HPMax = hpMaxOff > 0u ? _mem.ReadUInt(unchecked(mPtr + hpMaxOff)) : 0u,
                    Type = entityType,
                    MoveMode = moveModeOff > 0u ? _mem.ReadUInt(unchecked(mPtr + moveModeOff)) : 0u,
                    Status = statusOff > 0u ? _mem.ReadUInt(unchecked(mPtr + statusOff)) : 0u,
                    Distance = dist,
                    X = x,
                    Y = y,
                    Z = z,
                    NpcServices = ReadNpcServices(mPtr, essenceOff, entityType)
                });
            }

            return list;
        }

        public List<WorldEntityInfo> GetNearbyPlayers()
        {
            var list = new List<WorldEntityInfo>();
            if (_mem == null) return list;

            uint wBase = _mem.ResolveStructBase("world_base");
            if (!MemoryProcess.IsValidPointer(wBase)) return list;

            uint pplBase = _mem.ReadUIntFromNodeAtBase(wBase, "world_ppl_base");
            if (!MemoryProcess.IsValidPointer(pplBase)) return list;

            uint count = _mem.ReadUIntFromNodeAtBase(pplBase, "world_ppl_count");
            uint arrayPtr = _mem.ReadUIntFromNodeAtBase(pplBase, "world_ppl_array");
            if (!MemoryProcess.IsValidPointer(arrayPtr) || count == 0u || count > MaxPlayersSafety) return list;

            var slotNode = _mem.GetNode("world_ppl_ptr-index-mul-4");
            if (slotNode == null) return list;

            uint idOff = _mem.GetNodeOffset("ppl_id");
            uint lvlOff = _mem.GetNodeOffset("ppl_level");
            uint hpOff = _mem.GetNodeOffset("ppl_hp");
            uint hpMaxOff = _mem.GetNodeOffset("ppl_hp_max");
            uint xOff = _mem.GetNodeOffset("ppl_pos_x");
            uint yOff = _mem.GetNodeOffset("ppl_pos_y");
            uint zOff = _mem.GetNodeOffset("ppl_pos_z");

            for (uint i = 0u; i < count; i++)
            {
                uint pPtr = _mem.ReadUInt(unchecked(arrayPtr + slotNode.EvaluateFormula(i)));
                if (!MemoryProcess.IsValidPointer(pPtr)) continue;

                uint pid = idOff > 0u ? _mem.ReadUInt(unchecked(pPtr + idOff)) : 0u;
                float px = xOff > 0u ? _mem.ReadFloat(unchecked(pPtr + xOff)) : 0f;
                float py = yOff > 0u ? _mem.ReadFloat(unchecked(pPtr + yOff)) : 0f;
                float pz = zOff > 0u ? _mem.ReadFloat(unchecked(pPtr + zOff)) : 0f;
                uint hp = hpOff > 0u ? _mem.ReadUInt(unchecked(pPtr + hpOff)) : 0u;
                uint hpMax = hpMaxOff > 0u ? _mem.ReadUInt(unchecked(pPtr + hpMaxOff)) : 0u;

                float dist = 0f;
                if (_selfPlayer != null && _selfPlayer.Id != 0 && pid != _selfPlayer.Id)
                {
                    float dx = px - _selfPlayer.X;
                    float dz = pz - _selfPlayer.Z;
                    dist = (float)System.Math.Sqrt((dx * dx) + (dz * dz));
                }

                list.Add(new WorldEntityInfo
                {
                    ServerId = pid,
                    Name = ReadEntityName(pPtr, "ppl_name", 32, useFallback: false),
                    Level = lvlOff > 0u ? _mem.ReadUInt(unchecked(pPtr + lvlOff)) : 0u,
                    HP = hp,
                    HPMax = hpMax > 0u ? hpMax : hp,
                    Type = 1,
                    Distance = dist,
                    X = px,
                    Y = py,
                    Z = pz
                });
            }

            return list;
        }

        public List<uint> GetPartyMemberBuffIconIds(uint memberPtr)
        {
            var buffs = new List<uint>();
            if (_mem == null || !MemoryProcess.IsValidPointer(memberPtr))
                return buffs;

            uint buffArrayPtr = _mem.ReadUIntFromNodeAtBase(memberPtr, "party_member_buff_array");
            if (!MemoryProcess.IsValidPointer(buffArrayPtr))
                return buffs;

            var iconNode = _mem.GetNode("party_member_buff_icon_id-index-mul-12");
            if (iconNode == null)
                return buffs;

            uint iconLocalOff = _mem.GetNodeOffset("party_member_buff_icon_id-index-mul-12");

            for (int i = 0; i < MaxPartyBuffSlotsSafety; i++)
            {
                uint delta = iconNode.EvaluateFormula((uint)i);
                ushort iconId = _mem.ReadUShort(unchecked(buffArrayPtr + delta + iconLocalOff));
                if (iconId == 0)
                    continue;

                buffs.Add(iconId);
            }

            return buffs;
        }

        public List<FriendEntityInfo> GetFriends()
        {
            var list = new List<FriendEntityInfo>();
            if (_mem == null) return list;

            uint pStruct = _mem.ResolveStructBase("player_struct");
            if (!MemoryProcess.IsValidPointer(pStruct)) return list;

            uint friendsBase = _mem.ReadUIntFromNodeAtBase(pStruct, "friends_base");
            if (!MemoryProcess.IsValidPointer(friendsBase)) return list;

            var cOffs = _mem.GetNode("friend_count")?.GetLocalOffsetsUInt();
            if (cOffs == null || cOffs.Length == 0) return list;

            uint count = _mem.ReadUInt(_mem.ResolvePointer(friendsBase, cOffs));
            if (count == 0u || count > MaxFriendsSafety) return list;

            var aOffs = _mem.GetNode("friend_array")?.GetLocalOffsetsUInt();
            if (aOffs == null || aOffs.Length == 0) return list;

            uint arrayPtr = _mem.ResolvePointer(friendsBase, aOffs);
            if (!MemoryProcess.IsValidPointer(arrayPtr)) return list;

            var slotNode = _mem.GetNode("friend_slot_ptr-index-mul-4");
            if (slotNode == null) return list;

            uint idOff = _mem.GetNodeOffset("friend_id");
            uint classOff = _mem.GetNodeOffset("friend_class");
            uint lvlOff = _mem.GetNodeOffset("friend_level");

            for (uint i = 0u; i < count; i++)
            {
                uint fPtr = _mem.ReadUInt(unchecked(arrayPtr + slotNode.EvaluateFormula(i)));
                if (!MemoryProcess.IsValidPointer(fPtr)) continue;

                uint fid = idOff > 0u ? _mem.ReadUInt(unchecked(fPtr + idOff)) : 0u;
                if (fid == 0u) continue;

                list.Add(new FriendEntityInfo
                {
                    Id = fid,
                    Name = ReadEntityName(fPtr, "friend_name", 32, useFallback: false),
                    ClassId = classOff > 0u ? _mem.ReadUInt(unchecked(fPtr + classOff)) : 0u,
                    Level = lvlOff > 0u ? _mem.ReadUInt(unchecked(fPtr + lvlOff)) : 0u
                });
            }

            return list;
        }

        public string ResolvePartyLeaderName(uint leaderId)
        {
            if (leaderId == 0u)
                return "ID:0";

            if (_selfPlayer != null && _selfPlayer.Id == leaderId && !string.IsNullOrWhiteSpace(_selfPlayer.Name))
                return _selfPlayer.Name;

            var friend = GetFriends().FirstOrDefault(f => f != null && f.Id == leaderId && !string.IsNullOrWhiteSpace(f.Name));
            if (friend != null)
                return friend.Name;

            var nearbyPlayer = GetNearbyPlayers().FirstOrDefault(p => p != null && p.ServerId == leaderId && !string.IsNullOrWhiteSpace(p.Name));
            if (nearbyPlayer != null)
                return nearbyPlayer.Name;

            return $"ID:{leaderId}";
        }

        public List<DropItemInfo> GetGroundDrops()
        {
            var list = new List<DropItemInfo>();
            if (_mem == null) return list;

            uint wBase = _mem.ResolveStructBase("world_base");
            if (!MemoryProcess.IsValidPointer(wBase)) return list;

            uint dropBase = _mem.ReadUIntFromNodeAtBase(wBase, "world_drop_base");
            if (!MemoryProcess.IsValidPointer(dropBase)) return list;

            uint count = _mem.ReadUIntFromNodeAtBase(dropBase, "world_drop_count");
            uint arraySize = _mem.ReadUIntFromNodeAtBase(dropBase, "world_drop_array_sizeof");
            uint arrayPtr = _mem.ReadUIntFromNodeAtBase(dropBase, "world_drop_array");
            if (!MemoryProcess.IsValidPointer(arrayPtr) || count == 0u || arraySize == 0u || arraySize > MaxDropsSafety) return list;

            var slotNode = _mem.GetNode("world_drop_slot_ptr-index-mul-4");
            if (slotNode == null) return list;

            uint itemPtrOff = _mem.GetNodeOffset("world_drop_item_ptr");

            uint serverIdOff = _mem.GetNodeOffset("drop_server_id");
            uint itemIdOff = _mem.GetNodeOffset("drop_id");
            uint typeOff = _mem.GetNodeOffset("drop_type");
            uint distOff = _mem.GetNodeOffset("drop_distance");
            uint xOff = _mem.GetNodeOffset("drop_pos_x");
            uint yOff = _mem.GetNodeOffset("drop_pos_y");
            uint zOff = _mem.GetNodeOffset("drop_pos_z");

            for (uint i = 0u; i < arraySize; i++)
            {
                uint nodePtr = _mem.ReadUInt(unchecked(arrayPtr + slotNode.EvaluateFormula(i)));
                if (!MemoryProcess.IsValidPointer(nodePtr)) continue;

                uint itemPtr = itemPtrOff > 0u ? _mem.ReadUInt(unchecked(nodePtr + itemPtrOff)) : 0u;
                if (!MemoryProcess.IsValidPointer(itemPtr)) continue;

                list.Add(new DropItemInfo
                {
                    ServerId = serverIdOff > 0u ? _mem.ReadUInt(unchecked(itemPtr + serverIdOff)) : 0u,
                    ItemId = itemIdOff > 0u ? _mem.ReadUInt(unchecked(itemPtr + itemIdOff)) : 0u,
                    Name = ReadEntityName(itemPtr, "drop_name", 48, useFallback: false),
                    Type = typeOff > 0u ? _mem.ReadUInt(unchecked(itemPtr + typeOff)) : 0u,
                    Distance = distOff > 0u ? _mem.ReadFloat(unchecked(itemPtr + distOff)) : 0f,
                    X = xOff > 0u ? _mem.ReadFloat(unchecked(itemPtr + xOff)) : 0f,
                    Y = yOff > 0u ? _mem.ReadFloat(unchecked(itemPtr + yOff)) : 0f,
                    Z = zOff > 0u ? _mem.ReadFloat(unchecked(itemPtr + zOff)) : 0f
                });
            }

            return list;
        }
    }
}