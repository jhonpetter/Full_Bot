using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Full_Bot
{
    public enum TaskStepType
    {
        MoverParaPonto,
        InteragirNpc,
        FarmarMobPorId,
        ColetarMatPorId,
        EntregarQuest
    }

    public class QuestRouteStep
    {
        public TaskStepType Type { get; set; }
        public string Description { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public int FlyAltitude { get; set; } = 0;

        public uint QuestId { get; set; }
        public uint NpcTemplateId { get; set; }
        public List<uint> TargetIds { get; set; } = new List<uint>();
        public int RequiredCount { get; set; } = 0;
    }

    public class TasksLoader
    {
        public Queue<QuestRouteStep> CurrentRoute { get; private set; } = new Queue<QuestRouteStep>();

        public bool HasPendingSteps => CurrentRoute.Count > 0;
        public QuestRouteStep CurrentStep => CurrentRoute.Count > 0 ? CurrentRoute.Peek() : null;

        public void EnqueueStep(QuestRouteStep step)
        {
            if (step != null)
                CurrentRoute.Enqueue(step);
        }

        public void CompleteCurrentStep()
        {
            if (CurrentRoute.Count > 0)
                CurrentRoute.Dequeue();
        }

        public void ClearRoute()
        {
            CurrentRoute.Clear();
        }

        public void LoadRouteFromJson(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var steps = JsonSerializer.Deserialize<List<QuestRouteStep>>(File.ReadAllText(filePath), options);

            CurrentRoute.Clear();
            if (steps != null)
            {
                foreach (var s in steps)
                {
                    if (s != null)
                        CurrentRoute.Enqueue(s);
                }
            }
        }
    }
}