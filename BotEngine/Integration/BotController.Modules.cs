using System;

namespace Full_Bot
{
    public partial class BotController
    {
        public BotEngineModules Modules { get; private set; }
        public Move MovementEngine { get; private set; }

        public bool IsInDungeon { get; set; } = false;

        internal void InitializeNewModules()
        {
            MovementEngine = new Move(Player, World);
            Modules = new BotEngineModules(Player, Party, Buffs, Skills, World, Memory, MovementEngine);

            string charName = Player?.Name;
            if (!string.IsNullOrWhiteSpace(charName) && !charName.StartsWith("PID:"))
            {
                Modules.LoadForCharacter(charName);
            }
        }

        internal bool RunIntegratedModulesStep()
        {
            if (Modules == null || Player == null || Player.Base == 0)
                return false;

            string charName = Player.Name;
            if (!string.IsNullOrWhiteSpace(charName) && !charName.StartsWith("PID:"))
            {
                Modules.LoadForCharacter(charName);
            }

            WorldEntityInfo currentTarget = null;
            if (World != null && Player.TargetId != 0)
            {
                var entities = World.GetMobsAndNpcs();
                if (entities != null)
                {
                    for (int i = 0; i < entities.Count; i++)
                    {
                        if (entities[i] != null && entities[i].ServerId == Player.TargetId)
                        {
                            currentTarget = entities[i];
                            break;
                        }
                    }
                }
            }

            bool killingMob = CurrentState == BotState.Combat;
            bool tankEngaged = false;
            bool assistantEngaged = false;
            bool supportDanger = false;
            uint supportThreatMob = 0;

            var snap = BotHub.GetSnapshot();
            for (int i = 0; i < snap.Count; i++)
            {
                var b = snap[i];
                if (b?.Player == null || b.Modules?.WhoAmI?.Config == null)
                    continue;

                var role = b.Modules.WhoAmI.Config.GroupRole;
                if (role == WhoAmIRole.Tank && b.CurrentState == BotState.Combat)
                    tankEngaged = true;
                else if (role == WhoAmIRole.Assistant && b.CurrentState == BotState.Combat)
                    assistantEngaged = true;
                else if (role == WhoAmIRole.Support && b.CurrentState == BotState.Moving && b.Player.HP > 0 && b.Player.HPMax > 0)
                {
                    float hpPct = b.Player.HP * 100f / b.Player.HPMax;
                    if (hpPct <= b.Modules.WhoAmI.Config.Skills.SupportFleeHpPercent)
                    {
                        supportDanger = true;
                        supportThreatMob = b.Player.TargetId;
                    }
                }
            }

            var dec = Modules.EvaluateStep(
                IsInDungeon,
                currentTarget,
                killingMob,
                tankEngaged,
                assistantEngaged,
                supportDanger,
                supportThreatMob);

            if (!dec.Handled)
                return false;

            if (dec.SpecializedSkillId > 0 && Player != null)
            {
                if (dec.SpecializedSkillTargetId != 0 && Player.TargetId != dec.SpecializedSkillTargetId)
                    Player.TargetId = dec.SpecializedSkillTargetId;

                Player.CastSkill(dec.SpecializedSkillId);
            }

            string finalStatus = dec.Status;
            BotState finalState = dec.DesiredState;

            if (dec.HasMoveGoal)
            {
                if (Modules.ExecuteMovement(dec.UseFlight, dec.MoveX, dec.MoveY, dec.MoveZ, out BotState moveState, out string moveStatus))
                {
                    finalState = moveState;
                    if (!string.IsNullOrWhiteSpace(moveStatus))
                        finalStatus = $"{dec.Status} | {moveStatus}";
                }
            }

            CurrentState = finalState;
            StatusText = finalStatus;
            return true;
        }
    }
}