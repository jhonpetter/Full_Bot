using System;
using System.Linq;

namespace Full_Bot
{
    public partial class BotQuestRunner
    {
        private const float ArrivalThreshold = 4.0f;

        private readonly PlayerCore _player;
        private readonly TasksQuestActive _questActive;
        private readonly PlayerInventory _inventory;
        private readonly WorldEntities _world;
        private readonly TasksLoader _tasksLoader;
        private readonly BotCombat _combat;

        public BotQuestRunner(
            PlayerCore player,
            TasksQuestActive questActive,
            PlayerInventory inventory,
            WorldEntities world,
            TasksLoader tasksLoader,
            BotCombat combat)
        {
            _player = player;
            _questActive = questActive;
            _inventory = inventory;
            _world = world;
            _tasksLoader = tasksLoader;
            _combat = combat;
        }

        public void Tick()
        {
            var step = _tasksLoader.CurrentStep;
            if (step == null) return;

            switch (step.Type)
            {
                case TaskStepType.MoverParaPonto:
                    HandleMoveStep(step);
                    break;

                case TaskStepType.InteragirNpc:
                case TaskStepType.EntregarQuest:
                    HandleNpcStep(step);
                    break;

                case TaskStepType.FarmarMobPorId:
                    HandleFarmMobStep(step);
                    break;

                case TaskStepType.ColetarMatPorId:
                    HandleGatherStep(step);
                    break;
            }
        }

        private void HandleMoveStep(QuestRouteStep step)
        {
            float dist = Distance3D(_player.X, _player.Y, _player.Z, step.X, step.Y, step.Z);
            if (dist <= ArrivalThreshold)
            {
                _tasksLoader.CompleteCurrentStep();
                return;
            }

            if (step.FlyAltitude > 0)
                _player.MoveToFly(step.X, step.Z, step.FlyAltitude);
            else
                _player.MoveTo(step.X, step.Y, step.Z);
        }

        private void HandleNpcStep(QuestRouteStep step)
        {
            var npc = _world.GetMobsAndNpcs()
                .FirstOrDefault(e => e.TemplateId == step.NpcTemplateId);

            if (npc == null)
            {
                HandleMoveStep(step);
                return;
            }

            if (npc.Distance > ArrivalThreshold)
            {
                _player.MoveTo(npc.X, npc.Y, npc.Z);
                return;
            }

            if (_player.TargetId != npc.ServerId)
                _player.TargetId = npc.ServerId;

            _tasksLoader.CompleteCurrentStep();
        }

        private void HandleFarmMobStep(QuestRouteStep step)
        {
            if (step.QuestId > 0 && step.RequiredCount > 0)
            {
                var active = _questActive.GetActiveQuests().FirstOrDefault(q => q.QuestId == step.QuestId);
                if (active != null)
                {
                    int killed = active.Targets.Sum(t => (int)t.MobsKilled);
                    if (killed >= step.RequiredCount)
                    {
                        _tasksLoader.CompleteCurrentStep();
                        return;
                    }
                }
            }

            var mob = _combat.FindNearestTarget(step.TargetIds, 60f);
            if (mob == null)
            {
                HandleMoveStep(step);
                return;
            }

            if (mob.Distance > 6f)
            {
                _player.MoveTo(mob.X, mob.Y, mob.Z);
                return;
            }

            _combat.AttackTarget(mob.ServerId);
        }

        private void HandleGatherStep(QuestRouteStep step)
        {
            uint targetItemId = step.TargetIds.FirstOrDefault();
            if (targetItemId > 0 && step.RequiredCount > 0)
            {
                var questItems = _inventory.GetQuestItems();
                long currentCount = questItems.Where(i => i.ItemId == targetItemId).Sum(i => (long)i.Amount);
                if (currentCount >= step.RequiredCount)
                {
                    _tasksLoader.CompleteCurrentStep();
                    return;
                }
            }

            var drop = _world.GetGroundDrops()
                .Where(d => step.TargetIds.Count == 0 || step.TargetIds.Contains(d.ItemId))
                .OrderBy(d => d.Distance)
                .FirstOrDefault();

            if (drop == null)
            {
                HandleMoveStep(step);
                return;
            }

            if (drop.Distance > ArrivalThreshold)
            {
                _player.MoveTo(drop.X, drop.Y, drop.Z);
                return;
            }

            _player.PickupItem(drop.ServerId, isMaterial: true);
        }

        private static float Distance3D(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float dz = z2 - z1;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}