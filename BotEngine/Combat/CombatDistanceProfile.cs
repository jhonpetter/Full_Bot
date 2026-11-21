using System;

namespace Full_Bot
{
    public enum CombatArchetype
    {
        Fisico = 0,
        MagicoArqueiro = 1
    }

    public class CombatDistanceProfile
    {
        public CombatArchetype Archetype { get; set; } = CombatArchetype.Fisico;

        // Faixa ideal de engajamento/uso de skills (ex.: 3-4m físico / 6-15m mágico ou arqueiro)
        public float MinEngagementRange { get; set; } = 1.0f;
        public float PreferredMaxRange { get; set; } = 3.5f;
        public float ApproachStopDistance { get; set; } = 3.0f;

        // Gancho para expansão futura com offsets lidos da memória do client
        public uint? RangeOffsetOverride { get; set; } = null;

        public virtual float ResolveEffectiveMaxRange(MemoryProcess mem = null)
        {
            if (RangeOffsetOverride.HasValue && mem != null)
            {
                return Math.Max(MinEngagementRange, PreferredMaxRange);
            }

            return Math.Max(MinEngagementRange, PreferredMaxRange);
        }

        public virtual float ResolveApproachDistance(MemoryProcess mem = null)
        {
            float maxR = ResolveEffectiveMaxRange(mem);
            return Math.Min(maxR, Math.Max(MinEngagementRange, ApproachStopDistance));
        }

        public static CombatDistanceProfile CreatePreset(CombatArchetype archetype)
        {
            if (archetype == CombatArchetype.MagicoArqueiro)
            {
                return new CombatDistanceProfile
                {
                    Archetype = CombatArchetype.MagicoArqueiro,
                    MinEngagementRange = 6.0f,
                    PreferredMaxRange = 15.0f,
                    ApproachStopDistance = 12.0f
                };
            }

            return new CombatDistanceProfile
            {
                Archetype = CombatArchetype.Fisico,
                MinEngagementRange = 1.0f,
                PreferredMaxRange = 4.0f,
                ApproachStopDistance = 3.2f
            };
        }
    }
}