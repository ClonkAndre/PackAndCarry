using IVSDKDotNet;

namespace PackAndCarry.Classes
{
    internal class ModSettings
    {

        #region Variables
        // General
        public static bool DisableInMP;
        public static bool UseCustomGXTs;

        // Inventory
        public static int DefaultCapacity;
        public static bool AllowExtraSlots;

        // Time
        public static bool SlowDownTimeWhenInventoryIsOpen;
        public static float SlowDownTimeAmount;

        // Exhaustion
        public static bool DisableExhaustionInMP;
        public static bool PlayerIsExhaustedMoreQuicklyBasedOnInventoryWeight;

        // Movement
        public static bool DisableMovementImpactInMP;
        public static bool PlayerMovementSpeedDecreasedByInventoryWeight;
        public static float MovementSpeedDecreaseMultiplier;
        #endregion

        public static void Load(SettingsFile settings)
        {
            // General
            DisableInMP = settings.GetBoolean("General", "DisableInMP", false);
            UseCustomGXTs = settings.GetBoolean("General", "UseCustomGXTs", true);

            // Inventory
            DefaultCapacity = settings.GetInteger("Inventory", "DefaultCapacity", 10);
            AllowExtraSlots = settings.GetBoolean("Inventory", "AllowExtraSlots", true);

            // Time
            SlowDownTimeWhenInventoryIsOpen = settings.GetBoolean("Time", "SlowDownTimeWhenInventoryIsOpen", true);
            SlowDownTimeAmount = settings.GetFloat("Time", "SlowDownTimeAmount", 0.25f);

            // Exhaustion
            DisableExhaustionInMP = settings.GetBoolean("Exhaustion", "DisableInMP", true);
            PlayerIsExhaustedMoreQuicklyBasedOnInventoryWeight = settings.GetBoolean("Exhaustion", "PlayerIsExhaustedMoreQuicklyBasedOnInventoryWeight", false);

            // Movement
            DisableMovementImpactInMP = settings.GetBoolean("Movement", "DisableInMP", true);
            PlayerMovementSpeedDecreasedByInventoryWeight = settings.GetBoolean("Movement", "PlayerMovementSpeedDecreasedByInventoryWeight", false);
            MovementSpeedDecreaseMultiplier = settings.GetFloat("Movement", "DecreaseMultiplier", 1.0f);
        }

    }
}
