using DrovaRandomizer.Data;
using DrovaRandomizer.Systems.Generation;
using Drova_Modding_API.Access;
using MelonLoader;
using UnityEngine;

namespace DrovaRandomizer.Systems.World
{
    /// <summary>
    /// Entrance randomizer: rewires which cave each cave mouth leads into. Links stay bidirectional
    /// pairs, so the gate the player arrives at always leads back. Each gate gets the authored arrival
    /// anchor of the vanilla gate that pointed at its new destination.
    /// </summary>
    public sealed class TeleporterShuffler : IRandoSystem
    {
        void IRandoSystem.Configure(RandoSeed? seed)
        {
            try
            {
                TeleporterAccess.SetDestinationOverrides(BuildDestinations(seed?.Teleporters ?? new Dictionary<string, string>()));
            }
            catch (Exception e)
            {
                MelonLogger.Error("TeleporterShuffler.Configure failed: " + e);
            }
        }

        private static Dictionary<string, TeleporterAccess.GateDestination> BuildDestinations(
            Dictionary<string, string> mouthToInterior)
        {
            Dictionary<string, TeleporterAccess.GateDestination> destinations = new();
            foreach (KeyValuePair<string, TeleporterGate> entry in TeleporterTable.Generated)
            {
                destinations[entry.Key] = VanillaDestination(entry.Key);
            }

            int rewired = 0;
            foreach (KeyValuePair<string, string> pair in mouthToInterior)
            {
                string mouth = pair.Key;
                string interior = pair.Value;
                TeleporterGate mouthGate = TeleporterTable.Generated[mouth];
                TeleporterGate interiorGate = TeleporterTable.Generated[interior];

                destinations[mouth] = VanillaDestination(interiorGate.Partner);
                destinations[interior] = VanillaDestination(mouthGate.Partner);
                if (mouth != interiorGate.Partner)
                {
                    rewired++;
                }
            }

            MelonLogger.Msg("Teleporter shuffle configured: " + mouthToInterior.Count + " links, " + rewired + " rewired.");
            return destinations;
        }

        private static TeleporterAccess.GateDestination VanillaDestination(string gateName)
        {
            TeleporterGate gate = TeleporterTable.Generated[gateName];
            return new TeleporterAccess.GateDestination
            {
                TargetPos = new Vector2(gate.TargetX, gate.TargetY),
                TargetMoveDir = new Vector2(gate.TargetDirX, gate.TargetDirY),
            };
        }
    }
}
