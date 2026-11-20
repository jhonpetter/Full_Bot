using System.Collections.Generic;

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

    public class WorldEntities
    {
        private const uint MaxMobsSafety = 500;
        private const uint MaxPlayersSafety = 300;
        private const uint MaxDropsSafety = 1000;

        private readonly MemoryProcess _mem;

        public WorldEntities(MemoryProcess mem)
        {
            _mem = mem;
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

        public List<WorldEntityInfo> GetMobsAndNpcs()
        {
            var list = new List<WorldEntityInfo>();
            uint wBase = WorldBase;
            if (wBase == 0) return list;

            uint mobBaseOff = _mem.GetFirstLocalOffset("world_mobs_npc_base", 0x24);
            uint mobBase = _mem.ReadUInt(wBase + mobBaseOff);
            if (mobBase == 0) return list;

            uint countOff = _mem.GetFirstLocalOffset("world_mob_count", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("world_mob_array", 0x5C);

            uint count = _mem.ReadUInt(mobBase + countOff);
            uint arrayPtr = _mem.ReadUInt(mobBase + arrayOff);
            if (arrayPtr == 0 || count == 0 || count > MaxMobsSafety) return list;

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
                if (mPtr == 0) continue;

                uint namePtr = _mem.ReadUInt(mPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0; n++)
                    namePtr = _mem.ReadUInt(namePtr + nameOffs[n]);

                list.Add(new WorldEntityInfo
                {
                    ServerId = _mem.ReadUInt(mPtr + serverIdOff),
                    TemplateId = _mem.ReadUInt(mPtr + templateIdOff),
                    Name = _mem.ReadUnicodeString(namePtr, 32),
                    Level = _mem.ReadUInt(mPtr + lvlOff),
                    HP = _mem.ReadUInt(mPtr + hpOff),
                    HPMax = _mem.ReadUInt(mPtr + hpMaxOff),
                    Type = _mem.ReadUInt(mPtr + typeOff),
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
            if (wBase == 0) return list;

            uint pplBaseOff = _mem.GetFirstLocalOffset("world_ppl_base", 0x20);
            uint pplBase = _mem.ReadUInt(wBase + pplBaseOff);
            if (pplBase == 0) return list;

            uint countOff = _mem.GetFirstLocalOffset("world_ppl_count", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("world_ppl_array", 0x98);

            uint count = _mem.ReadUInt(pplBase + countOff);
            uint arrayPtr = _mem.ReadUInt(pplBase + arrayOff);
            if (arrayPtr == 0 || count == 0 || count > MaxPlayersSafety) return list;

            var slotNode = _mem.GetNode("world_ppl_ptr-index-mul-4");
            var nameNode = _mem.GetNode("ppl_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x6E0, 0x00 };

            uint idOff = _mem.GetFirstLocalOffset("ppl_id", 0x4B8);
            uint lvlOff = _mem.GetFirstLocalOffset("ppl_level", 0x4C4);
            uint hpOff = _mem.GetFirstLocalOffset("ppl_hp", 0x4CC);
            uint mpOff = _mem.GetFirstLocalOffset("ppl_mp", 0x4D0);
            uint xOff = _mem.GetFirstLocalOffset("ppl_pos_x", 0x3C);
            uint yOff = _mem.GetFirstLocalOffset("ppl_pos_y", 0x40);
            uint zOff = _mem.GetFirstLocalOffset("ppl_pos_z", 0x44);

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint pPtr = _mem.ReadUInt(arrayPtr + delta);
                if (pPtr == 0) continue;

                uint namePtr = _mem.ReadUInt(pPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0; n++)
                    namePtr = _mem.ReadUInt(namePtr + nameOffs[n]);

                list.Add(new WorldEntityInfo
                {
                    ServerId = _mem.ReadUInt(pPtr + idOff),
                    Name = _mem.ReadUnicodeString(namePtr, 32),
                    Level = _mem.ReadUInt(pPtr + lvlOff),
                    HP = _mem.ReadUInt(pPtr + hpOff),
                    HPMax = _mem.ReadUInt(pPtr + mpOff),
                    X = _mem.ReadFloat(pPtr + xOff),
                    Y = _mem.ReadFloat(pPtr + yOff),
                    Z = _mem.ReadFloat(pPtr + zOff)
                });
            }
            return list;
        }

        public List<DropItemInfo> GetGroundDrops()
        {
            var list = new List<DropItemInfo>();
            uint wBase = WorldBase;
            if (wBase == 0) return list;

            uint dropBaseOff = _mem.GetFirstLocalOffset("world_drop_base", 0x28);
            uint dropBase = _mem.ReadUInt(wBase + dropBaseOff);
            if (dropBase == 0) return list;

            uint countOff = _mem.GetFirstLocalOffset("world_drop_count", 0x14);
            uint arraySizeOff = _mem.GetFirstLocalOffset("world_drop_array_sizeof", 0x18);
            uint arrayOff = _mem.GetFirstLocalOffset("world_drop_array", 0x1C);

            uint count = _mem.ReadUInt(dropBase + countOff);
            uint arraySize = _mem.ReadUInt(dropBase + arraySizeOff);
            uint arrayPtr = _mem.ReadUInt(dropBase + arrayOff);
            if (arrayPtr == 0 || count == 0 || arraySize == 0 || arraySize > MaxDropsSafety) return list;

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
                if (nodePtr == 0) continue;

                uint itemPtr = _mem.ReadUInt(nodePtr + itemPtrOff);
                if (itemPtr == 0) continue;

                uint namePtr = _mem.ReadUInt(itemPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0; n++)
                    namePtr = _mem.ReadUInt(namePtr + nameOffs[n]);

                list.Add(new DropItemInfo
                {
                    ServerId = _mem.ReadUInt(itemPtr + serverIdOff),
                    ItemId = _mem.ReadUInt(itemPtr + itemIdOff),
                    Name = _mem.ReadUnicodeString(namePtr, 48),
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