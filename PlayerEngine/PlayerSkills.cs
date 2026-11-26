using System;
using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public enum SkillCategory
    {
        Unknown = 0,
        Attack = 1,
        Buff = 2,
        Heal = 3,
        Jump = 4,
        Passive = 5
    }

    public enum SkillTargetScope
    {
        SelfOnly = 0,
        PartyMember = 1,
        AnyTarget = 2
    }

    public class SkillInfo
    {
        public uint Id { get; set; }
        public uint Level { get; set; }
        public uint Cooldown { get; set; }
        public uint MaxCooldown { get; set; }
        public byte Type { get; set; }
        public bool IsActive { get; set; }
        public bool IsLearned { get; set; }
        public float Range { get; set; }
        public SkillCategory Category { get; set; }
        public SkillTargetScope TargetScope { get; set; }
        public bool IsMagic { get; set; }
        public bool IsReady => Cooldown == 0u;
        public bool IsBuff => Category == SkillCategory.Buff;
        public bool IsBuffSkill => Category == SkillCategory.Buff || Category == SkillCategory.Heal;
        public bool RequiresSelfOnly => TargetScope == SkillTargetScope.SelfOnly;
    }

    public class PlayerSkills
    {
        private const uint MaxSkillsSafety = 250;

        private readonly MemoryProcess _mem;
        private readonly AsmRunner _asm;
        private readonly PlayerCore _player;

        public PlayerSkills(MemoryProcess mem, PlayerCore player, AsmRunner asm = null)
        {
            _mem = mem;
            _player = player;
            _asm = asm;
        }

        public uint Count
        {
            get
            {
                uint pBase = _player.Base;
                if (!MemoryProcess.IsValidPointer(pBase)) return 0u;

                return _mem.ReadUIntFromNodeAtBase(pBase, "skill_count");
            }
        }

        public List<SkillInfo> GetLearnedSkills()
        {
            var list = new List<SkillInfo>();
            uint pBase = _player.Base;
            if (!MemoryProcess.IsValidPointer(pBase)) return list;

            uint count = Count;
            if (count == 0u || count > MaxSkillsSafety) return list;

            uint arrayPtr = _mem.ReadUIntFromNodeAtBase(pBase, "skill_array_base");
            if (!MemoryProcess.IsValidPointer(arrayPtr)) return list;

            var slotNode = _mem.GetNode("skill_slot_ptr-index-mul-4");
            if (slotNode == null) return list;

            uint idOff = _mem.GetNodeOffset("skill_id");
            uint lvlOff = GetFirstAvailableOffset("skill_lvl", "skill_level");
            uint cdOff = _mem.GetNodeOffset("skill_cooldown");
            uint maxCdOff = _mem.GetNodeOffset("skill_await");
            uint activeOff = _mem.GetNodeOffset("skill_active");
            uint learnedOff = _mem.GetNodeOffset("skill_learned");

            for (uint i = 0u; i < count; i++)
            {
                uint sPtr = _mem.ReadUInt(unchecked(arrayPtr + slotNode.EvaluateFormula(i)));
                if (!MemoryProcess.IsValidPointer(sPtr)) continue;

                uint id = _mem.ReadUInt(unchecked(sPtr + idOff));
                byte rawType = ReadSkillTypeFromNode(sPtr);
                bool isActive = activeOff > 0u && _mem.ReadByte(unchecked(sPtr + activeOff)) != 0;
                bool isLearned = learnedOff == 0u || _mem.ReadByte(unchecked(sPtr + learnedOff)) != 0;

                ClassifyFromCtNodes(sPtr, rawType, out SkillCategory cat, out SkillTargetScope scope, out bool isMagic);

                list.Add(new SkillInfo
                {
                    Id = id,
                    Level = lvlOff > 0u ? _mem.ReadUInt(unchecked(sPtr + lvlOff)) : 0u,
                    Cooldown = _mem.ReadUInt(unchecked(sPtr + cdOff)),
                    MaxCooldown = maxCdOff > 0u ? _mem.ReadUInt(unchecked(sPtr + maxCdOff)) : 0u,
                    Type = rawType,
                    IsActive = isActive,
                    IsLearned = isLearned,
                    Range = ReadSkillPrayRangeFromCt(sPtr),
                    Category = cat,
                    TargetScope = scope,
                    IsMagic = isMagic
                });
            }

            return list;
        }

        public SkillInfo GetSkillById(uint skillId)
        {
            if (skillId == 0u) return null;
            return GetLearnedSkills().FirstOrDefault(s => s != null && s.Id == skillId);
        }

        public float GetSkillRangeById(uint skillId)
        {
            var skill = GetSkillById(skillId);
            return skill != null ? skill.Range : 0f;
        }

        public bool IsSkillReady(uint skillId) => GetLearnedSkills().Any(s => s.Id == skillId && s.IsReady);

        private uint GetFirstAvailableOffset(params string[] keys)
        {
            if (keys == null) return 0u;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                var node = _mem.GetNode(key);
                if (node != null)
                {
                    uint off = _mem.GetNodeOffset(key);
                    if (off > 0u) return off;
                }
            }
            return 0u;
        }

        private byte ReadSkillTypeFromNode(uint cecSkillPtr)
        {
            var typeNode = _mem.GetNode("skill_type");
            if (typeNode == null) return 0;

            uint addr = ResolvePointerChainFromSkill(cecSkillPtr, typeNode, dereferenceFinal: false);
            return addr != 0u ? _mem.ReadByte(addr) : (byte)0;
        }

        private uint ResolvePointerChainFromSkill(uint cecSkillPtr, CtEntryNode targetNode, bool dereferenceFinal)
        {
            if (!MemoryProcess.IsValidPointer(cecSkillPtr) || targetNode == null)
                return 0u;

            var slotNode = _mem.GetNode("skill_slot_ptr-index-mul-4");
            var path = new List<CtEntryNode>();
            var cur = targetNode;

            while (cur != null && cur != slotNode)
            {
                path.Add(cur);
                cur = cur.Parent;
            }

            path.Reverse();

            uint ptr = cecSkillPtr;
            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i];
                var offsets = node.GetLocalOffsetsUInt();

                if (offsets != null && offsets.Length > 0)
                {
                    for (int j = 0; j < offsets.Length; j++)
                    {
                        uint nextAddr = unchecked(ptr + offsets[j]);
                        bool isLastStep = (i == path.Count - 1) && (j == offsets.Length - 1);

                        if (isLastStep && !dereferenceFinal)
                            return nextAddr;

                        ptr = _mem.ReadUInt(nextAddr);
                        if (!MemoryProcess.IsValidPointer(ptr) && !isLastStep)
                            return 0u;
                    }
                }
                else if (node.HasOwnAddressDisplacement)
                {
                    uint nextAddr = unchecked(ptr + node.OwnAddressDisplacement);
                    bool isLastStep = (i == path.Count - 1);

                    if (isLastStep && !dereferenceFinal)
                        return nextAddr;

                    ptr = _mem.ReadUInt(nextAddr);
                    if (!MemoryProcess.IsValidPointer(ptr) && !isLastStep)
                        return 0u;
                }
            }

            return ptr;
        }

        private uint ReadUIntFromSkillSubNode(uint cecSkillPtr, string nodeKey)
        {
            var node = _mem.GetNode(nodeKey);
            if (node == null) return 0u;

            uint addr = ResolvePointerChainFromSkill(cecSkillPtr, node, dereferenceFinal: false);
            if (addr == 0u) return 0u;

            string vType = (node.VariableType ?? string.Empty).Trim().ToLowerInvariant();
            if (vType.Contains("2 bytes") || vType.Contains("word") || vType.Contains("short"))
                return _mem.ReadUInt(addr) & 0xFFFFu;

            if (vType.Contains("byte"))
                return _mem.ReadByte(addr);

            return _mem.ReadUInt(addr);
        }

        private void ClassifyFromCtNodes(
            uint cecSkillPtr,
            byte fallbackType,
            out SkillCategory category,
            out SkillTargetScope scope,
            out bool isMagic)
        {
            category = SkillCategory.Attack;
            scope = SkillTargetScope.AnyTarget;
            isMagic = false;

            if (!MemoryProcess.IsValidPointer(cecSkillPtr))
            {
                MapFallbackType(fallbackType, out category, out scope, out isMagic);
                return;
            }

            var stubTypeNode = _mem.GetNode("skill_internal_stub_type");
            var stubRangeTypeNode = _mem.GetNode("skill_internal_stub_range_type");
            var stubChiGainNode = _mem.GetNode("skill_internal_stub_chi_gain");

            if (stubTypeNode == null && stubRangeTypeNode == null && stubChiGainNode == null)
            {
                MapFallbackType(fallbackType, out category, out scope, out isMagic);
                return;
            }

            uint skillType = ReadUIntFromSkillSubNode(cecSkillPtr, "skill_internal_stub_type");
            uint rangeType = ReadUIntFromSkillSubNode(cecSkillPtr, "skill_internal_stub_range_type");
            uint chiGain = ReadUIntFromSkillSubNode(cecSkillPtr, "skill_internal_stub_chi_gain");

            isMagic = skillType == 1u || skillType == 2u || rangeType == 1u;

            if (skillType == 2u)
            {
                category = chiGain == 0u ? SkillCategory.Buff : SkillCategory.Heal;
            }
            else if (skillType == 8u)
            {
                category = SkillCategory.Jump;
                scope = SkillTargetScope.SelfOnly;
                return;
            }
            else if (rangeType == 3u && chiGain == 0u)
            {
                category = SkillCategory.Passive;
                scope = SkillTargetScope.SelfOnly;
                return;
            }
            else
            {
                category = SkillCategory.Attack;
            }

            if (rangeType == 2u || rangeType == 5u)
                scope = SkillTargetScope.SelfOnly;
            else if (rangeType == 4u)
                scope = SkillTargetScope.PartyMember;
            else
                scope = SkillTargetScope.AnyTarget;
        }

        private static void MapFallbackType(byte rawType, out SkillCategory category, out SkillTargetScope scope, out bool isMagic)
        {
            isMagic = rawType == 1 || rawType == 2;
            scope = rawType == 5 ? SkillTargetScope.SelfOnly : SkillTargetScope.AnyTarget;

            if (rawType == 2)
                category = SkillCategory.Buff;
            else if (rawType == 5)
                category = SkillCategory.Passive;
            else
                category = SkillCategory.Attack;
        }

        public string LastRangeDiagnostic { get; private set; } = string.Empty;

        private float ReadSkillPrayRangeFromCt(uint cecSkillPtr)
        {
            LastRangeDiagnostic = string.Empty;

            if (_mem == null || _asm == null || !MemoryProcess.IsValidPointer(cecSkillPtr))
            {
                LastRangeDiagnostic = "Memória/Asm inválido ou ponteiro da skill inválido.";
                return 0f;
            }

            var internalNode = _mem.GetNode("skill_internal");
            var stubNode = _mem.GetNode("skill_internal_stub");
            var getRangeFuncNode = _mem.GetNode("skill_internal_stub_function_getRange");

            if (internalNode == null || stubNode == null || getRangeFuncNode == null)
            {
                LastRangeDiagnostic = $"Nós ausentes no CT (internal={internalNode != null}, stub={stubNode != null}, getRange={getRangeFuncNode != null}).";
                return 0f;
            }

            uint skillInternal = ResolvePointerChainFromSkill(cecSkillPtr, internalNode, dereferenceFinal: true);
            uint skillStub = ResolvePointerChainFromSkill(cecSkillPtr, stubNode, dereferenceFinal: true);
            uint funcGetPrayRangeAddr = ResolvePointerChainFromSkill(cecSkillPtr, getRangeFuncNode, dereferenceFinal: false);
            uint funcGetPrayRange = funcGetPrayRangeAddr != 0u ? _mem.ReadUInt(funcGetPrayRangeAddr) : 0u;

            if (!MemoryProcess.IsValidPointer(skillInternal) ||
                !MemoryProcess.IsValidPointer(skillStub) ||
                !MemoryProcess.IsValidPointer(funcGetPrayRange))
            {
                LastRangeDiagnostic = $"Ponteiro inválido (internal=0x{skillInternal:X}, stub=0x{skillStub:X}, fnAddr=0x{funcGetPrayRangeAddr:X}, fn=0x{funcGetPrayRange:X}).";
                return 0f;
            }

            const int resultOffset = 128;

            try
            {
                uint rawBits = _asm.RunRawAssembly(_mem, allocAddr =>
                {
                    uint resultAddr = unchecked(allocAddr + (uint)resultOffset);
                    return string.Join(Environment.NewLine, new[]
                    {
                        "use32",
                        $"org 0x{allocAddr:X}",
                        "xor eax, eax",
                        $"mov [0x{resultAddr:X}], eax",
                        $"mov ecx, 0x{skillStub:X}",
                        $"push 0x{skillInternal:X}",
                        $"mov eax, 0x{funcGetPrayRange:X}",
                        "call eax",
                        $"fstp dword [0x{resultAddr:X}]",
                        "ret"
                    });
                }, allocSize: 256, readUIntOffset: resultOffset);

                byte[] bytes = BitConverter.GetBytes(rawBits);
                float range = BitConverter.ToSingle(bytes, 0);
                if (float.IsNaN(range) || float.IsInfinity(range) || range < 0f)
                {
                    LastRangeDiagnostic = $"Retorno float inválido (rawBits=0x{rawBits:X}, range={range}).";
                    return 0f;
                }

                LastRangeDiagnostic = $"OK (fn=0x{funcGetPrayRange:X}, range={range:F2}).";
                return range;
            }
            catch (Exception ex)
            {
                LastRangeDiagnostic = $"Exceção ao executar getRange (fn=0x{funcGetPrayRange:X}): {ex.Message}";
                return 0f;
            }
        }
    }
}