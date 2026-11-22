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

        private uint GetNodeOffset(string key)
        {
            var node = _mem.GetNode(key);
            if (node == null) return 0u;

            var local = node.GetLocalOffsetsUInt();
            if (local != null && local.Length > 0)
                return local[0];

            return node.HasOwnAddressDisplacement ? node.OwnAddressDisplacement : 0u;
        }

        public List<FriendInfo> GetFriends()
        {
            var list = new List<FriendInfo>();
            uint pBase = _player.Base;
            if (pBase == 0u) return list;

            var friendsBaseOffs = _mem.GetNode("friends_base")?.GetLocalOffsetsUInt();
            if (friendsBaseOffs == null || friendsBaseOffs.Length == 0) return list;

            uint friendsBase = _mem.ReadUInt(pBase + friendsBaseOffs[0]);
            for (int i = 1; i < friendsBaseOffs.Length && friendsBase != 0u; i++)
                friendsBase = _mem.ReadUInt(friendsBase + friendsBaseOffs[i]);

            if (friendsBase == 0u) return list;

            uint countOff = GetNodeOffset("friend_count");
            uint count = _mem.ReadUInt(friendsBase + countOff);
            if (count == 0u || count > MaxFriendsSafety) return list;

            var arrayOffs = _mem.GetNode("friend_array")?.GetLocalOffsetsUInt();
            if (arrayOffs == null || arrayOffs.Length == 0) return list;

            uint arrayPtr = _mem.ReadUInt(friendsBase + arrayOffs[0]);
            for (int a = 1; a < arrayOffs.Length && arrayPtr != 0u; a++)
                arrayPtr = _mem.ReadUInt(arrayPtr + arrayOffs[a]);

            if (arrayPtr == 0u) return list;

            var slotNode = _mem.GetNode("friend_slot_ptr-index-mul-4");
            if (slotNode == null) return list;

            uint idOff = GetNodeOffset("friend_id");
            uint classOff = GetNodeOffset("friend_class");
            uint lvlOff = GetNodeOffset("friend_level");
            var nameOffs = _mem.GetNode("friend_name")?.GetLocalOffsetsUInt();
            if (nameOffs == null || nameOffs.Length == 0) return list;

            for (uint i = 0; i < count; i++)
            {
                uint delta = slotNode.EvaluateFormula(i);
                uint fPtr = _mem.ReadUInt(arrayPtr + delta);
                if (fPtr == 0u) continue;

                uint namePtr = _mem.ReadUInt(fPtr + nameOffs[0]);
                for (int n = 1; n < nameOffs.Length && namePtr != 0u; n++)
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