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
                Modules.LoadForCharacter(charName, force: true);
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
                else if (role == WhoAmIRole.Support && b.CurrentState == BotState.Moving && b.Player.HP > 0)
                {
                    bool lowHp = b.Player.HPMax > 0 &&
                                 (b.Player.HP * 100f / b.Player.HPMax) <= b.Modules.WhoAmI.Config.Skills.SupportFleeHpPercent;
                    bool fleeingThreat = string.Equals(b.Modules.WhoAmI.Config.GroupRole.ToString(), "Support", StringComparison.Ordinal) &&
                                         (b.StatusText ?? string.Empty).IndexOf("fugindo", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (lowHp || fleeingThreat)
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

            string finalStatus = dec.Status;
            BotState finalState = dec.DesiredState;

            if (dec.SpecializedSkillId > 0)
            {
                if (dec.SpecializedSkillTargetId != 0 && Player.TargetId != dec.SpecializedSkillTargetId)
                    Player.TargetId = dec.SpecializedSkillTargetId;

                Player.CastSkill(dec.SpecializedSkillId);
                CurrentState = finalState;
                StatusText = finalStatus;
                return true;
            }

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