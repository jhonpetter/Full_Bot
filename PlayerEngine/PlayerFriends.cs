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
            if (!MemoryProcess.IsValidPointer(pBase)) return list;

            uint friendsBase = _mem.FollowPointerChain(pBase, _mem.GetNode("friends_base")?.GetLocalOffsetsUInt());
            if (!MemoryProcess.IsValidPointer(friendsBase)) return list;

            uint count = _mem.ReadUInt(friendsBase + _mem.GetNodeOffset("friend_count"));
            if (count == 0u || count > MaxFriendsSafety) return list;

            uint arrayPtr = _mem.FollowPointerChain(friendsBase, _mem.GetNode("friend_array")?.GetLocalOffsetsUInt());
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

                list.Add(new FriendInfo
                {
                    Id = _mem.ReadUInt(fPtr + idOff),
                    ClassId = _mem.ReadUInt(fPtr + classOff),
                    Level = _mem.ReadUInt(fPtr + lvlOff),
                    Name = _mem.ReadUnicodeStringByChain(fPtr, "friend_name", 32)
                });
            }

            return list;
        }
    }
}