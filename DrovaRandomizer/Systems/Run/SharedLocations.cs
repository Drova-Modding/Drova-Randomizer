using DrovaRandomizer.Data;
using Drova_Modding_API.Access;
using Drova_Modding_API.Systems.Coop;
using Drova_Modding_API.Systems.Networking;

namespace DrovaRandomizer.Systems.Run
{
    /// <summary>
    /// Under co-op shared loot, a world loot or shop location one player checks is checked for everyone, and
    /// its item stays with the finder. Quests and milestones stay per player.
    /// </summary>
    public sealed class SharedLocations : IRandoSystem
    {
        private const ushort MessageId = 180;

        private static bool _wasShared;

        /// <summary>True while the co-op host shares loot.</summary>
        public static bool Enabled => CoopAccess.IsMultiplayer && CoopAccess.Supports(CoopCapability.Loot);

        private struct LocationChecked : INetMessage
        {
            public string Run;
            public string Key;

            public void Write(INetWriter w)
            {
                w.Put(Run);
                w.Put(Key);
            }

            public void Read(INetReader r)
            {
                Run = r.GetString();
                Key = r.GetString();
            }
        }

        void IRandoSystem.Initialize(HarmonyLib.Harmony harmony)
        {
            NetworkAccess.RegisterWithId<LocationChecked>(MessageId, OnLocationChecked);
            CoopAccess.OnSessionChanged += () => _wasShared = false;
        }

        void IRandoSystem.Update()
        {
            bool shared = Enabled && Randomizer.Active;
            if (shared && !_wasShared)
            {
                foreach (string key in Randomizer.State.Checked)
                {
                    Send(LocationTable.Get(key));
                }
            }
            _wasShared = shared;
        }

        /// <summary>Tells the others that this player checked a location.</summary>
        internal static void Send(LocationDef? location)
        {
            if (Enabled && Randomizer.Active && IsLoot(location))
            {
                NetworkAccess.SendToAll(new LocationChecked { Run = Randomizer.RunKey, Key = location!.Key });
            }
        }

        private static bool IsLoot(LocationDef? location)
        {
            return location?.Category is LocationCategory.Chest or LocationCategory.Container or LocationCategory.Critter
                or LocationCategory.Resource or LocationCategory.Cache or LocationCategory.Pickup or LocationCategory.Mugging
                or LocationCategory.Trader;
        }

        private static void OnLocationChecked(INetPeer peer, LocationChecked message)
        {
            if (!Enabled || !Randomizer.Active || message.Run != Randomizer.RunKey || !IsLoot(LocationTable.Get(message.Key)))
            {
                return;
            }
            if (Randomizer.CheckForPartner(message.Key) && NetworkAccess.Role == NetRole.Host)
            {
                NetworkAccess.SendToAll(message);
            }
        }
    }
}
