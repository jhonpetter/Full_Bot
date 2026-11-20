using System.Collections.Generic;
using System.Linq;

namespace Full_Bot
{
    public class GatherModule
    {
        private readonly PlayerCore _player;
        private readonly WorldEntities _world;

        public LootFilterMode Mode { get; set; } = LootFilterMode.SomenteMoedas;
        public float LootRadius { get; set; } = 6.0f; // Apenas drops ao lado do personagem (6 metros)
        public List<uint> CollectItemIds { get; } = new List<uint>();

        public GatherModule(PlayerCore player, WorldEntities world)
        {
            _player = player;
            _world = world;
        }

        public bool TryLoot(out string status)
        {
            status = string.Empty;
            if (Mode == LootFilterMode.Nenhum) return false;

            // Filtra apenas o que está bem próximo (<= LootRadius) e NUNCA pega materiais de cavar (Type == 2) no loot pós-combate
            var nearbyDrops = _world.GetGroundDrops()
                .Where(d => d.Distance <= LootRadius && d.Type != 2);

            DropItemInfo targetDrop = null;

            switch (Mode)
            {
                case LootFilterMode.SomenteMoedas:
                    targetDrop = nearbyDrops.Where(d => d.Type == 3).OrderBy(d => d.Distance).FirstOrDefault();
                    break;

                case LootFilterMode.ListaEspecifica:
                    if (CollectItemIds.Count == 0) return false;
                    targetDrop = nearbyDrops.Where(d => CollectItemIds.Contains(d.ItemId)).OrderBy(d => d.Distance).FirstOrDefault();
                    break;

                case LootFilterMode.TodosNoRaio:
                    // Pega qualquer drop de mob (itens + moedas) que esteja ao lado do char
                    targetDrop = nearbyDrops.OrderBy(d => d.Distance).FirstOrDefault();
                    break;
            }

            if (targetDrop == null) return false;

            // Se estiver a mais de 3.5m dá um passo curto, senão já coleta direto sem andar
            if (targetDrop.Distance > 3.5f)
            {
                _player.MoveTo(targetDrop.X, targetDrop.Y, targetDrop.Z);
                status = $"Pegando drop próximo: {targetDrop.Name}";
            }
            else
            {
                _player.PickupItem(targetDrop.ServerId, isMaterial: false);
                status = $"Coletando: {targetDrop.Name}";
            }
            return true;
        }
    }
}
