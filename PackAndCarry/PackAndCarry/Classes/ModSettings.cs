using System.Numerics;

using IVSDKDotNet;

namespace PackAndCarry.Classes
{
    internal class ModSettings
    {

        #region Variables
        // General
        public static bool DisableInMP;

        // Inventory
        public static int DefaultCapacity;
        #endregion

        public static void Load(SettingsFile settings)
        {
            // General
            DisableInMP = settings.GetBoolean("General", "DisableInMP", false);

            // Inventory
            DefaultCapacity = settings.GetInteger("Inventory", "DefaultCapacity", 10);
        }

    }
}
