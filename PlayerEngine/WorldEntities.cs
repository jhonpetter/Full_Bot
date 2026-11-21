using System;
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
        private const uint MinValidUserPtr = 0x00010000u;
        private const uint MaxValidUserPtr = 0x7FFFFFFFu;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _selfPlayer;

        public WorldEntities(MemoryProcess mem, PlayerCore selfPlayer = null)
        {
            _mem = mem;
            _selfPlayer = selfPlayer;
        }

        private uint WorldBase
        {
            get
            {
                var worldNode = _mem.GetNode("world_base");
                if (worldNode != null)
                {
                    var offs = worldNode.GetOffsetsUInt();
                    if (offs.Length > 0)
                        return _mem.ResolvePointer(_mem.GetNodeBaseAddress(worldNode), offs);
                }

                var baseNode = _mem.GetNode("base_address") ?? _mem.GetNode("base_ptr_raw");
                uint gameRunOff = _mem.GetFirstLocalOffset("struct_base", 0x1C);
                uint worldOff = _mem.GetFirstLocalOffset("world_base", 0x18);
                return _mem.ResolvePointer(_mem.GetNodeBaseAddress(baseNode), gameRunOff, worldOff);
            }
        }

        private uint PlayerStructBase
        {
            get
            {
                var pNode = _mem.GetNode("player_struct");
                if (pNode != null)
                {
                    var offs = pNode.GetOffsetsUInt();
                    if (offs.Length > 0)
                        return _mem.ResolvePointer(_mem.GetNodeBaseAddress(pNode), offs);
                }

                var baseNode = _mem.GetNode("base_address") ?? _mem.GetNode("base_ptr_raw");
                uint gameRunOff = _mem.GetFirstLocalOffset("struct_base", 0x1C);
                uint playerOff = _mem.GetFirstLocalOffset("player_struct", 0x2C);
                return _mem.ResolvePointer(_mem.GetNodeBaseAddress(baseNode), gameRunOff, playerOff);
            }
        }

        private static bool IsLikelyValidPointer(uint ptr)
        {
            return ptr >= MinValidUserPtr && ptr <= MaxValidUserPtr;
        }

        private string ReadEntityName(uint entityPtr, uint[] nameOffs, int maxChars, uint templateId = 0, uint entityType = 0, bool useFallback = false)
        {
            if (!IsLikelyValidPointer(entityPtr) || nameOffs == null || nameOffs.Length == 0)
                return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

            uint current = _mem.ReadUInt(entityPtr + nameOffs[0]);

            if (nameOffs.Length == 1)
            {
                if (!IsLikelyValidPointer(current))
                    return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

                return CleanEntityString(_mem.ReadUnicodeString(current, maxChars), templateId, entityType, useFallback);
            }

            for (int n = 1; n < nameOffs.Length - 1; n++)
            {
                if (!IsLikelyValidPointer(current))
                    return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

                current = _mem.ReadUInt(current + nameOffs[n]);
            }

            if (!IsLikelyValidPointer(current))
                return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

            uint lastOffset = nameOffs[nameOffs.Length - 1];
            uint strAddr = (lastOffset == 0) ? current : _mem.ReadUInt(current + lastOffset);

            if (!IsLikelyValidPointer(strAddr))
                return useFallback ? FormatEntityFallbackName(templateId, entityType) : string.Empty;

            return CleanEntityString(_mem.ReadUnicodeString(strAddr, maxChars), templateId, entityType, useFallback);
        }

        private static string CleanEntityString(string raw, uint templateId, uint entityType, bool useFallback)
        {
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
            uint wBase = WorldBase;
            if (!IsLikelyValidPointer(wBase)) return list;

            uint mobBaseOff = _mem.GetFirstLocalOffset("world_mobs_npc_base", 0x24);
            uint mobBase = _mem.ReadUInt(wBase + mobBaseOff);
            if (!IsLikelyValidPointer(mobBase)) return list;

            uint countOff = _mem.GetFirstLocalOffset("world_mob_count", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("world_mob_array", 0x5C);

            uint count = _mem.ReadUInt(mobBase + countOff);
            uint arrayPtr = _mem.ReadUInt(mobBase + arrayOff);
            if (!IsLikelyValidPointer(arrayPtr) || count == 0 || count > MaxMobsSafety) return list;

            var slotNode = _mem.GetNode("world_mob_ptr-index-mul-4");
            var nameNode = _mem.GetNode("mob_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x254, 0x00 };

            uint serverIdOff = _mem.GetFirstLocalOffset("mob_server_id", 0x114);
            uint templateIdOff = _mem.GetFirstLocalOffset("mob_id", 0x118);
            uint lvlOff = _mem.GetFirstLocalOffset("mob_level", 0x120);
            uint hpOff = _mem.GetFirstLocalOffset("mob_hp", 0x128);
            uint hpMaxOff = _mem.GetFirstLocalOffset("mob_hp_max", 0x174);
            uint typeOff = _mem.GetFirstLocalOffset("mob_type", 0xB4);
            uint distOff = _mem.GetFirstLocalOffset("mob_distance", 0x27C);
            uint xOff = _mem.GetFirstLocalOffset("mob_pos_x", 0x3C);
            uint yOff = _mem.GetFirstLocalOffset("mob_pos_y", 0x40);
            uint zOff = _mem.GetFirstLocalOffset("mob_pos_z", 0x44);

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint mPtr = _mem.ReadUInt(arrayPtr + delta);
                if (!IsLikelyValidPointer(mPtr)) continue;

                uint templateId = _mem.ReadUInt(mPtr + templateIdOff);
                uint entityType = _mem.ReadUInt(mPtr + typeOff);

                list.Add(new WorldEntityInfo
                {
                    ServerId = _mem.ReadUInt(mPtr + serverIdOff),
                    TemplateId = templateId,
                    Name = ReadEntityName(mPtr, nameOffs, 32, templateId, entityType, useFallback: true),
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
            uint wBase = WorldBase;
            if (!IsLikelyValidPointer(wBase)) return list;

            uint pplBaseOff = _mem.GetFirstLocalOffset("world_ppl_base", 0x20);
            uint pplBase = _mem.ReadUInt(wBase + pplBaseOff);
            if (!IsLikelyValidPointer(pplBase)) return list;

            uint countOff = _mem.GetFirstLocalOffset("world_ppl_count", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("world_ppl_array", 0x98);

            uint count = _mem.ReadUInt(pplBase + countOff);
            uint arrayPtr = _mem.ReadUInt(pplBase + arrayOff);
            if (!IsLikelyValidPointer(arrayPtr) || count == 0 || count > MaxPlayersSafety) return list;

            var slotNode = _mem.GetNode("world_ppl_ptr-index-mul-4");
            var nameNode = _mem.GetNode("ppl_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x6E0, 0x00 };

            uint idOff = _mem.GetFirstLocalOffset("ppl_id", 0x4B8);
            uint lvlOff = _mem.GetFirstLocalOffset("ppl_level", 0x4C4);
            uint hpOff = _mem.GetFirstLocalOffset("ppl_hp", 0x4CC);
            uint hpMaxOff = _mem.GetFirstLocalOffset("ppl_hp_max", 0x518);
            uint xOff = _mem.GetFirstLocalOffset("ppl_pos_x", 0x3C);
            uint yOff = _mem.GetFirstLocalOffset("ppl_pos_y", 0x40);
            uint zOff = _mem.GetFirstLocalOffset("ppl_pos_z", 0x44);

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint pPtr = _mem.ReadUInt(arrayPtr + delta);
                if (!IsLikelyValidPointer(pPtr)) continue;

                uint pid = _mem.ReadUInt(pPtr + idOff);
                float px = _mem.ReadFloat(pPtr + xOff);
                float py = _mem.ReadFloat(pPtr + yOff);
                float pz = _mem.ReadFloat(pPtr + zOff);
                uint hp = _mem.ReadUInt(pPtr + hpOff);
                uint hpMax = _mem.ReadUInt(pPtr + hpMaxOff);

                float dist = 0f;
                if (_selfPlayer != null && _selfPlayer.Id != 0 && pid != _selfPlayer.Id)
                    dist = MoveObstacleAvoidance.Dist2D(_selfPlayer.X, _selfPlayer.Z, px, pz);

                list.Add(new WorldEntityInfo
                {
                    ServerId = pid,
                    Name = ReadEntityName(pPtr, nameOffs, 32, useFallback: false),
                    Level = _mem.ReadUInt(pPtr + lvlOff),
                    HP = hp,
                    HPMax = hpMax > 0 ? hpMax : hp,
                    Type = 1,
                    Distance = dist,
                    X = px,
                    Y = py,
                    Z = pz
                });
            }
            return list;
        }

        /// <summary>
        /// Lê os IDs de ícone de buff ativos de um membro da party a partir de party_member_buff_array (0x58)
        /// e party_member_buff_icon_id-index-mul-12 (passo de 12 bytes, vtWord).
        /// </summary>
        public List<uint> GetPartyMemberBuffIconIds(uint memberPtr)
        {
            var buffs = new List<uint>();
            if (!IsLikelyValidPointer(memberPtr))
                return buffs;

            uint buffArrayOff = _mem.GetFirstLocalOffset("party_member_buff_array", 0x58);
            uint buffArrayPtr = _mem.ReadUInt(memberPtr + buffArrayOff);
            if (!IsLikelyValidPointer(buffArrayPtr))
                return buffs;

            var iconNode = _mem.GetNode("party_member_buff_icon_id-index-mul-12");
            uint iconLocalOff = iconNode != null ? _mem.GetFirstLocalOffset("party_member_buff_icon_id-index-mul-12", 0x0) : 0x0;

            for (int i = 0; i < MaxPartyBuffSlotsSafety; i++)
            {
                uint delta = iconNode != null ? iconNode.EvaluateFormula((uint)i) : ((uint)i * 12u);
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
            uint pStruct = PlayerStructBase;
            if (!IsLikelyValidPointer(pStruct)) return list;

            uint friendsBaseOff = _mem.GetFirstLocalOffset("friends_base", 0x14E4);
            uint friendsBase = _mem.ReadUInt(pStruct + friendsBaseOff);
            if (!IsLikelyValidPointer(friendsBase)) return list;

            var countNode = _mem.GetNode("friend_count");
            uint count = 0;
            if (countNode != null)
            {
                var cOffs = countNode.GetLocalOffsetsUInt();
                if (cOffs.Length > 0)
                    count = _mem.ReadUInt(_mem.ResolvePointer(friendsBase, cOffs));
            }
            if (count == 0)
                count = _mem.ReadUInt(friendsBase + 0x20);

            if (count == 0 || count > MaxFriendsSafety) return list;

            var arrNode = _mem.GetNode("friend_array");
            uint arrayPtr = 0;
            if (arrNode != null)
            {
                var aOffs = arrNode.GetLocalOffsetsUInt();
                if (aOffs.Length > 0)
                    arrayPtr = _mem.ResolvePointer(friendsBase, aOffs);
            }
            if (!IsLikelyValidPointer(arrayPtr))
                arrayPtr = _mem.ResolvePointer(friendsBase, 0xC, 0x0, 0x14);

            if (!IsLikelyValidPointer(arrayPtr)) return list;

            var slotNode = _mem.GetNode("friend_slot_ptr-index-mul-4");
            var nameNode = _mem.GetNode("friend_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x18, 0x00 };

            uint idOff = _mem.GetFirstLocalOffset("friend_id", 0x00);
            uint classOff = _mem.GetFirstLocalOffset("friend_class", 0x04);
            uint lvlOff = _mem.GetFirstLocalOffset("friend_level", 0x10);

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint fPtr = _mem.ReadUInt(arrayPtr + delta);
                if (!IsLikelyValidPointer(fPtr)) continue;

                uint fid = _mem.ReadUInt(fPtr + idOff);
                if (fid == 0) continue;

                list.Add(new FriendEntityInfo
                {
                    Id = fid,
                    Name = ReadEntityName(fPtr, nameOffs, 32, useFallback: false),
                    ClassId = _mem.ReadUInt(fPtr + classOff),
                    Level = _mem.ReadUInt(fPtr + lvlOff)
                });
            }

            return list;
        }

        public string ResolvePartyLeaderName(uint leaderId)
        {
            if (leaderId == 0)
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
            uint wBase = WorldBase;
            if (!IsLikelyValidPointer(wBase)) return list;

            uint dropBaseOff = _mem.GetFirstLocalOffset("world_drop_base", 0x28);
            uint dropBase = _mem.ReadUInt(wBase + dropBaseOff);
            if (!IsLikelyValidPointer(dropBase)) return list;

            uint countOff = _mem.GetFirstLocalOffset("world_drop_count", 0x14);
            uint arraySizeOff = _mem.GetFirstLocalOffset("world_drop_array_sizeof", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("world_drop_array", 0x1C);

            uint count = _mem.ReadUInt(dropBase + countOff);
            uint arraySize = _mem.ReadUInt(dropBase + arraySizeOff);
            uint arrayPtr = _mem.ReadUInt(dropBase + arrayOff);
            if (!IsLikelyValidPointer(arrayPtr) || count == 0 || arraySize == 0 || arraySize > MaxDropsSafety) return list;

            var slotNode = _mem.GetNode("world_drop_slot_ptr-index-mul-4");
            uint itemPtrOff = _mem.GetFirstLocalOffset("world_drop_item_ptr", 0x04);

            var nameNode = _mem.GetNode("drop_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x16C, 0x00 };

            uint serverIdOff = _mem.GetFirstLocalOffset("drop_server_id", 0x110);
            uint itemIdOff = _mem.GetFirstLocalOffset("drop_id", 0x114);
            uint typeOff = _mem.GetFirstLocalOffset("drop_type", 0x150);
            uint distOff = _mem.GetFirstLocalOffset("drop_distance", 0x160);
            uint xOff = _mem.GetFirstLocalOffset("drop_pos_x", 0x3C);
            uint yOff = _mem.GetFirstLocalOffset("drop_pos_y", 0x40);
            uint zOff = _mem.GetFirstLocalOffset("drop_pos_z", 0x44);

            for (uint i = 0; i < arraySize; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint nodePtr = _mem.ReadUInt(arrayPtr + delta);
                if (!IsLikelyValidPointer(nodePtr)) continue;

                uint itemPtr = _mem.ReadUInt(nodePtr + itemPtrOff);
                if (!IsLikelyValidPointer(itemPtr)) continue;

                list.Add(new DropItemInfo
                {
                    ServerId = _mem.ReadUInt(itemPtr + serverIdOff),
                    ItemId = _mem.ReadUInt(itemPtr + itemIdOff),
                    Name = ReadEntityName(itemPtr, nameOffs, 48, useFallback: false),
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