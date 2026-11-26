using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
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
        public float Distance { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
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
            if (!MemoryProcess.IsValidPointer(entityPtr))
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

        public List<WorldEntityInfo> GetMobsAndNpcs()
        {
            var list = new List<WorldEntityInfo>();
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
            uint distOff = _mem.GetNodeOffset("mob_distance");
            uint xOff = _mem.GetNodeOffset("mob_pos_x");
            uint yOff = _mem.GetNodeOffset("mob_pos_y");
            uint zOff = _mem.GetNodeOffset("mob_pos_z");

            for (uint i = 0u; i < count; i++)
            {
                uint mPtr = _mem.ReadUInt(arrayPtr + slotNode.EvaluateFormula(i));
                if (!MemoryProcess.IsValidPointer(mPtr)) continue;

                uint templateId = _mem.ReadUInt(mPtr + templateIdOff);
                uint entityType = _mem.ReadUInt(mPtr + typeOff);

                list.Add(new WorldEntityInfo
                {
                    ServerId = _mem.ReadUInt(mPtr + serverIdOff),
                    TemplateId = templateId,
                    Name = ReadEntityName(mPtr, "mob_name", 32, templateId, entityType, useFallback: true),
                    Level = _mem.ReadUInt(mPtr + lvlOff),
                    HP = _mem.ReadUInt(mPtr + hpOff),
                    HPMax = _mem.ReadUInt(mPtr + hpMaxOff),
                    Type = entityType,
                    Distance = _mem.ReadFloat(mPtr + distOff),
                    X = _mem.ReadFloat(mPtr + xOff),
                    Y = _mem.ReadFloat(mPtr + yOff),
                    Z = _mem.ReadFloat(mPtr + zOff)
                });
            }

            return list;
        }

        public List<WorldEntityInfo> GetNearbyPlayers()
        {
            var list = new List<WorldEntityInfo>();
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
                uint pPtr = _mem.ReadUInt(arrayPtr + slotNode.EvaluateFormula(i));
                if (!MemoryProcess.IsValidPointer(pPtr)) continue;

                uint pid = _mem.ReadUInt(pPtr + idOff);
                float px = _mem.ReadFloat(pPtr + xOff);
                float py = _mem.ReadFloat(pPtr + yOff);
                float pz = _mem.ReadFloat(pPtr + zOff);
                uint hp = _mem.ReadUInt(pPtr + hpOff);
                uint hpMax = _mem.ReadUInt(pPtr + hpMaxOff);

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
                    Level = _mem.ReadUInt(pPtr + lvlOff),
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
            if (!MemoryProcess.IsValidPointer(memberPtr))
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
                ushort iconId = _mem.ReadUShort(buffArrayPtr + delta + iconLocalOff);
                if (iconId == 0)
                    continue;

                buffs.Add(iconId);
            }

            return buffs;
        }

        public List<FriendEntityInfo> GetFriends()
        {
            var list = new List<FriendEntityInfo>();
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
                uint fPtr = _mem.ReadUInt(arrayPtr + slotNode.EvaluateFormula(i));
                if (!MemoryProcess.IsValidPointer(fPtr)) continue;

                uint fid = _mem.ReadUInt(fPtr + idOff);
                if (fid == 0u) continue;

                list.Add(new FriendEntityInfo
                {
                    Id = fid,
                    Name = ReadEntityName(fPtr, "friend_name", 32, useFallback: false),
                    ClassId = _mem.ReadUInt(fPtr + classOff),
                    Level = _mem.ReadUInt(fPtr + lvlOff)
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
                uint nodePtr = _mem.ReadUInt(arrayPtr + slotNode.EvaluateFormula(i));
                if (!MemoryProcess.IsValidPointer(nodePtr)) continue;

                uint itemPtr = _mem.ReadUInt(nodePtr + itemPtrOff);
                if (!MemoryProcess.IsValidPointer(itemPtr)) continue;

                list.Add(new DropItemInfo
                {
                    ServerId = _mem.ReadUInt(itemPtr + serverIdOff),
                    ItemId = _mem.ReadUInt(itemPtr + itemIdOff),
                    Name = ReadEntityName(itemPtr, "drop_name", 48, useFallback: false),
                    Type = _mem.ReadUInt(itemPtr + typeOff),
                    Distance = _mem.ReadFloat(itemPtr + distOff),
                    X = _mem.ReadFloat(itemPtr + xOff),
                    Y = _mem.ReadFloat(itemPtr + yOff),
                    Z = _mem.ReadFloat(itemPtr + zOff)
                });
            }

            return list;
        }
    }
}