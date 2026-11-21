using System.Collections.Generic;

namespace Full_Bot
{
    public enum LootAreaMode
    {
        SomenteMoedas = 0,
        Tudo = 1,
        MoedasEMattsLista = 2
    }

    public class MoveConfig
    {
        // 1.1 Navegação e voo (MoveMode: 0=andando, 1=nadando, 2=voando)
        public bool UseFlightForLongDistance { get; set; } = true;
        public float FlyMinDistance { get; set; } = 60f;
        public int FlyAltitude { get; set; } = 25;
        public float ArrivalRadius { get; set; } = 5f;

        // 1.2 Desvio de mobs e passagem entre mobs
        public bool AvoidMobsOnGround { get; set; } = true;
        public float MobAvoidRadius { get; set; } = 7.5f;
        public float MinGapBetweenMobsToPass { get; set; } = 5.5f;

        // 1.3 Rota de matts (estrutura base para evolução posterior)
        public bool RecordMattRouteEnabled { get; set; } = false;
        public float RouteSampleIntervalSeconds { get; set; } = 5f;
        public float RouteDuplicateRadius { get; set; } = 4f;
        public List<uint> AllowedMattItemIds { get; set; } = new List<uint>();

        // 1.4 Área de mobada / auto farm básico (somente X, Z e raio de permanência)
        public bool HasFarmArea { get; set; } = false;
        public float FarmCenterX { get; set; } = 0f;
        public float FarmCenterZ { get; set; } = 0f;
        public float FarmRadius { get; set; } = 40f;
        public LootAreaMode AreaLootMode { get; set; } = LootAreaMode.SomenteMoedas;
        public List<uint> AllowedLootIds { get; set; } = new List<uint>();
        public bool IncludeMattsInAreaLoot { get; set; } = true;

        // 1.5 Anti-travamento (preso no lugar -> círculo / voo para soltar)
        public bool AntiStuckEnabled { get; set; } = true;
        public float StuckCheckSeconds { get; set; } = 4.0f;
        public float StuckMinMoveDistance { get; set; } = 1.5f;
        public float UnstuckOrbitRadius { get; set; } = 5.0f;
        public bool UnstuckAllowFlight { get; set; } = true;
    }
}