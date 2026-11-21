using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Full_Bot
{
    public class CombatTargetValidator
    {
        private readonly PlayerCore _player;
        private readonly PlayerParty _party;
        private readonly MemoryProcess _mem;

        public CombatTargetValidator(PlayerCore player, PlayerParty party, MemoryProcess mem = null)
        {
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _party = party;
            _mem = mem;
        }

        public bool IsMobEngagedByOutsidePlayer(WorldEntityInfo mob, out uint mobCurrentTargetId, out string reason)
        {
            mobCurrentTargetId = 0;
            reason = string.Empty;

            if (mob == null || mob.ServerId == 0)
            {
                reason = "Mob inválido";
                return true;
            }

            mobCurrentTargetId = TryReadMobTargetId(mob);
            if (mobCurrentTargetId == 0)
                return false;

            if (mobCurrentTargetId == _player.Id)
                return false;

            var partyMemberIds = new HashSet<uint>();
            if (_party != null)
            {
                try
                {
                    foreach (var member in _party.GetMembers() ?? Enumerable.Empty<PartyMemberInfo>())
                    {
                        if (member != null && member.Id != 0)
                            partyMemberIds.Add(member.Id);
                    }
                }
                catch
                {
                    // Ignora falha de leitura da PT
                }
            }

            if (partyMemberIds.Contains(mobCurrentTargetId))
                return false;

            // IDs de jogador costumam ter bit 31 zerado (0x80000000). Se o alvo do mob for outro jogador fora da PT, ignora.
            bool looksLikeOtherPlayer = (mobCurrentTargetId & 0x80000000u) == 0u;
            if (looksLikeOtherPlayer)
            {
                reason = $"Mob focado em outro jogador (ID {mobCurrentTargetId}) fora do grupo";
                return true;
            }

            return false;
        }

        private uint TryReadMobTargetId(WorldEntityInfo mob)
        {
            if (_mem == null || mob == null)
                return 0;

            uint mobAddr = ResolveEntityAddress(mob);
            if (mobAddr == 0 || mobAddr < 0x00010000u || mobAddr > 0x7FFFFFFFu)
                return 0;

            var node = _mem.GetNode("mob_target_id");
            var local = node?.GetLocalOffsetsUInt();
            if (local != null && local.Length > 0 && local[0] != 0)
            {
                return _mem.ReadUInt(mobAddr + local[0]);
            }

            return 0;
        }

        private static uint ResolveEntityAddress(WorldEntityInfo mob)
        {
            var type = mob.GetType();
            var prop = type.GetProperty("Address", BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetProperty("StructAddress", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(uint))
                return (uint)prop.GetValue(mob, null);

            var field = type.GetField("Address", BindingFlags.Public | BindingFlags.Instance)
                     ?? type.GetField("StructAddress", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(uint))
                return (uint)field.GetValue(mob);

            return 0u;
        }
    }
}