using System.Collections.Generic;

namespace Full_Bot
{
    public class FriendInfo
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public uint Level { get; set; }
        public uint ClassId { get; set; }
    }

    public class PlayerFriends
    {
        private const uint MaxFriendsSafety = 200;

        private readonly MemoryProcess _mem;
        private readonly PlayerCore _player;

        public PlayerFriends(MemoryProcess mem, PlayerCore player)
        {
            _mem = mem;
            _player = player;
        }

        public List<FriendInfo> GetFriends()
        {
            var list = new List<FriendInfo>();
            uint pBase = _player.Base;
            if (pBase == 0) return list;

            uint friendsBaseOff = _mem.GetFirstLocalOffset("friends_base", 0x14E4);
            uint friendsBase = _mem.ReadUInt(pBase + friendsBaseOff);
            if (friendsBase == 0) return list;

            uint countOff = _mem.GetFirstLocalOffset("friend_count", 0x20);
            uint count = _mem.ReadUInt(friendsBase + countOff);

            var arrayNode = _mem.GetNode("friend_array");
            var arrayOffs = arrayNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x0C, 0x00, 0x14 };
            uint arrayPtr = _mem.ResolvePointer(friendsBase + arrayOffs[0], ConfigManager.SkipFirst(arrayOffs));
            if (arrayPtr == 0 || count == 0 || count > MaxFriendsSafety) return list;

            var slotNode = _mem.GetNode("friend_slot_ptr-index-mul-4");

            uint idOff = _mem.GetFirstLocalOffset("friend_id", 0x00);
            uint classOff = _mem.GetFirstLocalOffset("friend_class", 0x04);
            uint lvlOff = _mem.GetFirstLocalOffset("friend_level", 0x10);
            var nameNode = _mem.GetNode("friend_name");
            var nameOffs = nameNode?.GetLocalOffsetsUInt() ?? new uint[] { 0x18, 0x00 };

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode != null ? slotNode.EvaluateFormula(i) : (i * 4u);
                uint fPtr = _mem.ReadUInt(arrayPtr + delta);
                if (fPtr == 0) continue;

                uint namePtr = _mem.ReadUInt(fPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0; n++)
                    namePtr = _mem.ReadUInt(namePtr + nameOffs[n]);

                list.Add(new FriendInfo
                {
                    Id = _mem.ReadUInt(fPtr + idOff),
                    ClassId = _mem.ReadUInt(fPtr + classOff),
                    Level = _mem.ReadUInt(fPtr + lvlOff),
                    Name = _mem.ReadUnicodeString(namePtr, 32)
                });
            }
            return list;
        }
    }
}