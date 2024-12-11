using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;

using CCL.GTAIV;

using CustomInventoryIV;
using CustomInventoryIV.Base;
using CustomInventoryIV.Inventories;

using PackAndCarry.Classes;
using PackAndCarry.Classes.Json;

using IVSDKDotNet;
using IVSDKDotNet.Enums;
using static IVSDKDotNet.Native.Natives;

namespace PackAndCarry
{
    public class Main : Script
    {

        #region DllImports
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        #endregion

        #region Variables
        // Lists
        private InventoryPool inventoryPool;
        private Queue<Action> mainThreadQueue;
        private Dictionary<int, ImTexture> loadedWeaponTextures;
        private Dictionary<uint, List<SuseSlots>> playerSuseSlots;
        private Dictionary<string, float> predefinedItemWeights;

        // Inventory stuff
        private BasicInventory playerInventory;
        private Stopwatch inventoryKeyWatch;
        private bool wasInventoryOpenedViaController;
        public int InventoryOpenTimeInMS = 150;

        // Pickup stuff
        private bool blockPlayerAbilityToCollectPickup;
        private Vector3 lastPickupPosition;

        // Interpolation
        private float timeScaleInterpolationValue;
        private float lastTimeScaleValue;

        private float moveAnimSpeedInterpolationValue;
        private float lastMoveAnimSpeedValue;

        // Other
        private ConstructorInfo imTextureCtor;

        private int playerPedHandle;
        private int lastPlayerWeapon;
        private uint lastLoadedEpisode;

        private bool wasCursorPosSet;
        private bool wasROM614Displayed;
        private bool wasROM613Displayed;
        #endregion

        #region Methods
        private void UnloadTextures()
        {
            foreach (KeyValuePair<int, ImTexture> item in loadedWeaponTextures)
            {
                item.Value.Release();
            }
            loadedWeaponTextures.Clear();
        }
        private void LoadTextures()
        {
            if (loadedWeaponTextures.Count != 0)
            {
                // Destroy loaded textures if changing episode
                if (lastLoadedEpisode != IVGame.CurrentEpisodeMenu)
                {
                    UnloadTextures();
                }
            }

            // Set episode
            lastLoadedEpisode = IVGame.CurrentEpisodeMenu;

            string path = string.Format("{0}\\Icons\\Weapons\\{1}", ScriptResourceFolder, lastLoadedEpisode);

            // Create textures for the current episode
            string[] files = Directory.GetFiles(path, "*.dds", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                string fileName = Path.GetFileName(file);

                if (int.TryParse(fileName.Split('.')[0], out int result))
                {
                    // Create texture
                    if (ImGuiIV.CreateTextureFromFile(string.Format("{0}\\{1}", path, fileName), out ImTexture texture, out eResult r))
                    {
                        loadedWeaponTextures.Add(result, texture);
                    }
                    else
                    {
                        IVGame.Console.PrintError(string.Format("Failed to create texture {0}! Result: {1}", fileName, r));
                    }
                }
            }
        }
        private void LoadPlayerSuseSlots()
        {
            try
            {
                string fileName = string.Format("{0}\\Data\\playerSuseSlots.json", ScriptResourceFolder);

                if (!File.Exists(fileName))
                {
                    Logging.LogWarning("Could not find playerSuseSlots.json file! Some features might not be available.");
                    return;
                }

                if (playerSuseSlots != null)
                    playerSuseSlots.Clear();

                playerSuseSlots = Helper.ConvertJsonStringToObject<Dictionary<uint, List<SuseSlots>>>(File.ReadAllText(fileName));

                if (playerSuseSlots.Count == 0)
                    Logging.LogWarning("Loaded 0 player suse slot(s)! Some features might not be available.");
                else
                    Logging.Log("Loaded {0} player suse slot(s)!", playerSuseSlots.Count);
            }
            catch (Exception ex)
            {
                Logging.LogError("An error occured while trying to load player suse slots! Details: {0}", ex);
            }
        }
        private void LoadPredefiniedItemWeights()
        {
            try
            {
                string fileName = string.Format("{0}\\Data\\predefinedItemWeights.json", ScriptResourceFolder);

                if (!File.Exists(fileName))
                {
                    Logging.LogWarning("Could not find predefinedItemWeights.json file! Some features might not be available.");
                    return;
                }

                if (predefinedItemWeights != null)
                    predefinedItemWeights.Clear();

                predefinedItemWeights = Helper.ConvertJsonStringToObject<Dictionary<string, float>>(File.ReadAllText(fileName));

                if (predefinedItemWeights.Count == 0)
                    Logging.LogWarning("Loaded 0 predefined item weight(s)! Some features might not be available.");
                else
                    Logging.Log("Loaded {0} predefined item weight(s)!", predefinedItemWeights.Count);
            }
            catch (Exception ex)
            {
                Logging.LogError("An error occured while trying to load predefined item weights! Details: {0}", ex);
            }
        }

        private void DisplayCustomMissionIntroductionText()
        {
            // Show custom ROM6_14 help message
            if (IS_THIS_HELP_MESSAGE_BEING_DISPLAYED("ROM6_14"))
            {
                if (!wasROM614Displayed)
                {
                    string originalStr = GET_STRING_FROM_TEXT_FILE("ROM6_14");

                    switch ((eLanguage)GET_CURRENT_LANGUAGE())
                    {
                        case eLanguage.LANGUAGE_AMERICAN:
                            NativeGame.DisplayCustomHelpMessage(originalStr + " Press ~INPUT_FRONTEND_LEGEND~ to quickly switch to your last equipped weapon. Hold ~INPUT_FRONTEND_LEGEND~ to open up the inventory.", true);
                            break;
                        case eLanguage.LANGUAGE_GERMAN:
                            NativeGame.DisplayCustomHelpMessage(originalStr + " Drücke ~INPUT_FRONTEND_LEGEND~, um schnell zu der zuletzt ausgerüsteten Waffe zu wechseln. Halte ~INPUT_FRONTEND_LEGEND~ gedrückt, um das Inventar zu öffnen.", true);
                            break;
                        case eLanguage.LANGUAGE_ITALIAN:
                            NativeGame.DisplayCustomHelpMessage(originalStr + " Premi ~INPUT_FRONTEND_LEGEND~ per passare rapidamente all'ultima arma equipaggiata. Tieni premuto ~INPUT_FRONTEND_LEGEND~ per aprire l'inventario.", true);
                            break;
                        case eLanguage.LANGUAGE_SPANISH:
                            NativeGame.DisplayCustomHelpMessage(originalStr + " Presiona ~INPUT_FRONTEND_LEGEND~ para cambiar rápidamente a la última arma equipada. Mantén presionado ~INPUT_FRONTEND_LEGEND~ para abrir el inventario.", true);
                            break;
                    }

                    wasROM614Displayed = true;
                }
            }
            else
            {
                wasROM614Displayed = false;
            }

            // Show custom ROM6_13 help message
            if (IS_THIS_HELP_MESSAGE_BEING_DISPLAYED("ROM6_13"))
            {
                if (!wasROM613Displayed)
                {
                    string originalStr = GET_STRING_FROM_TEXT_FILE("ROM6_13");

                    switch ((eLanguage)GET_CURRENT_LANGUAGE())
                    {
                        case eLanguage.LANGUAGE_AMERICAN:
                            NativeGame.DisplayCustomHelpMessage(originalStr.Replace("weapon inventory", "inventory"));
                            break;
                        case eLanguage.LANGUAGE_GERMAN:
                            NativeGame.DisplayCustomHelpMessage(originalStr.Replace("Waffeninventar", "Inventar"));
                            break;
                        case eLanguage.LANGUAGE_ITALIAN:
                            NativeGame.DisplayCustomHelpMessage(originalStr.Replace(" delle armi", ""));
                            break;
                        case eLanguage.LANGUAGE_SPANISH:
                            NativeGame.DisplayCustomHelpMessage(originalStr.Replace(" de armas", ""));
                            break;
                    }

                    wasROM613Displayed = true;
                }
            }
            else
            {
                wasROM613Displayed = false;
            }
        }

        private void OpenInventory(bool wasOpenedUsingController)
        {
            playerInventory.IsVisible = true;

            if (wasOpenedUsingController)
                wasInventoryOpenedViaController = true;
        }
        private void CloseInventory()
        {
            playerInventory.IsVisible = false;
            wasInventoryOpenedViaController = false;
        }

        private void ResizeInventoryBasedOnClothes()
        {
            if (!ModSettings.AllowExtraSlots)
                return;
            if (playerInventory.IsVisible)
                return;

            uint drawable = GET_CHAR_DRAWABLE_VARIATION(playerPedHandle, 3);

            if (drawable == 0)
            {
                playerInventory.Resize(ModSettings.DefaultCapacity);
                return;
            }

            // Try get player suse slots list for current episode
            if (playerSuseSlots.TryGetValue(GET_CURRENT_EPISODE(), out List<SuseSlots> slots))
            {
                if (slots == null)
                    return;

                // Try get suse slots for current suse drawable
                SuseSlots suseSlot = slots.Where(x => x.Index == drawable).FirstOrDefault();

                if (suseSlot == null)
                    return;

                // Check if inventory already has the new capacity
                int newCapacity = ModSettings.DefaultCapacity + suseSlot.AdditionalSlots;

                if (playerInventory.Capacity != newCapacity)
                    playerInventory.Resize(newCapacity);
            }
        }
        private void CheckForInvalidScriptItems()
        {
            // Checks if the scripts that added their own items to the inventory are still running

            BasicInventoryItem[] items = playerInventory.GetItems();

            for (int i = 0; i < items.Length; i++)
            {
                BasicInventoryItem item = items[i];

                if (item == null)
                    continue;
                if (!item.Tags.ContainsKey("IS_CUSTOM_ITEM"))
                    continue;
                if (item.Tags.ContainsKey("DO_NOT_REMOVE_ITEM_WHEN_MOD_THAT_ADDED_IT_UNLOADS"))
                    continue;

                Guid ownerScriptId = (Guid)item.Tags["IS_CUSTOM_ITEM"];

                // Check if owner script is still running
                if (!IsScriptRunning(ownerScriptId))
                {
                    Logging.LogDebug("Removing custom item {0} because its owner script is no longer running.", item.ID, ownerScriptId);
                    playerInventory.RemoveItem(item);
                }
            }
        }
        private void HandleTimescaleInterpolation()
        {
            if (!ModSettings.SlowDownTimeWhenInventoryIsOpen)
                return;
            if (IVNetwork.IsNetworkGameRunning())
                return;

            if (playerInventory.IsVisible)
            {
                timeScaleInterpolationValue = timeScaleInterpolationValue + 0.02f;

                if (timeScaleInterpolationValue > 1.0f)
                {
                    timeScaleInterpolationValue = 1.0f;
                }
                else
                {
                    float newTimeScale = IVTimer.TimeScale - ModSettings.SlowDownTimeAmount;

                    if (newTimeScale > 0.0f)
                    {
                        lastTimeScaleValue = newTimeScale;
                        IVTimer.TimeScale = Lerp(1.0f, newTimeScale, timeScaleInterpolationValue);
                    }
                }
            }
            else
            {
                timeScaleInterpolationValue = timeScaleInterpolationValue - 0.03f;

                if (timeScaleInterpolationValue < 0.0f)
                {
                    timeScaleInterpolationValue = 0.0f;
                }
                else
                {
                    IVTimer.TimeScale = Lerp(1.0f, lastTimeScaleValue, timeScaleInterpolationValue);
                }
            }
        }
        private void HandlePreventPlayerToCollectPickup()
        {
            if (!blockPlayerAbilityToCollectPickup)
                return;

            // Get the player position
            GET_CHAR_COORDINATES(playerPedHandle, out Vector3 pos);

            if (Vector3.Distance(pos, lastPickupPosition) > 2.5f)
            {
                DISABLE_LOCAL_PLAYER_PICKUPS(false);
                blockPlayerAbilityToCollectPickup = false;
            }
            else
            {
                DISABLE_LOCAL_PLAYER_PICKUPS(true);
            }
        }
        private void HandlePlayerImpacts(IVPed playerPed)
        {
            if (!ModSettings.PlayerIsExhaustedMoreQuicklyBasedOnInventoryWeight && !ModSettings.PlayerMovementSpeedDecreasedByInventoryWeight)
                return;

            // Check if player is pressing the sprint key
            if (NativeControls.IsGameKeyPressed(0, GameKey.Sprint))
            {
                float weight = GetTotalInventoryWeight(playerInventory);

                if (weight <= 0f)
                    return;

                // Calculate how much the move anim speed will be decreased
                float moveAnimSpeedDecrease = Math.Max(0.0f, (1.0f - Math.Min(1.0f, weight / 1000f)) * ModSettings.MovementSpeedDecreaseMultiplier);
                lastMoveAnimSpeedValue = moveAnimSpeedDecrease;

                // Handle exhaustion
                if (ModSettings.PlayerIsExhaustedMoreQuicklyBasedOnInventoryWeight && !(ModSettings.DisableExhaustionInMP && IVNetwork.IsNetworkGameRunning()))
                {
                    IVPlayerInfo playerInfo = IVPlayerInfo.GetPlayerInfo(GET_PLAYER_ID());

                    // Calculate how long the player will be exhausted for
                    float minStamina = -((15f / moveAnimSpeedDecrease) * 10f);

                    // Decrease stamina faster depending on the weight of the inventory
                    float stamina = playerInfo.Stamina;

                    if (!(stamina < minStamina))
                        playerInfo.Stamina -= weight / 100;
                }

                // Handle move anim speed impact
                if (ModSettings.PlayerMovementSpeedDecreasedByInventoryWeight && !(ModSettings.DisableMovementImpactInMP && IVNetwork.IsNetworkGameRunning()))
                {
                    // Interpolate move anim speed
                    moveAnimSpeedInterpolationValue = moveAnimSpeedInterpolationValue + 0.01f;

                    if (moveAnimSpeedInterpolationValue > 1.0f)
                    {
                        moveAnimSpeedInterpolationValue = 1.0f;
                    }
                    else
                    {
                        // Set new move anim speed
                        playerPed.PedMoveBlendOnFoot.MoveAnimSpeed = Lerp(1.0f, moveAnimSpeedDecrease, moveAnimSpeedInterpolationValue);
                    }
                }

                //ShowSubtitleMessage("Raw Weight: {0}, moveAnimSpeedDecrease: {1}, Stamina: {2}, MinStamina: {3}", weight, moveAnimSpeedDecrease, stamina, minStamina);
            }
            else
            {
                // Interpolate move anim speed
                moveAnimSpeedInterpolationValue = moveAnimSpeedInterpolationValue - 0.01f;

                if (moveAnimSpeedInterpolationValue < 0.0f)
                {
                    moveAnimSpeedInterpolationValue = 0.0f;
                }
                else
                {
                    // Set new move anim speed
                    playerPed.PedMoveBlendOnFoot.MoveAnimSpeed = Lerp(1.0f, lastMoveAnimSpeedValue, moveAnimSpeedInterpolationValue);
                }
            }
        }

        private void DropItem(BasicInventory inventory, BasicInventoryItem item, float range = 0.0F)
        {
            if (inventory == null)
                return;
            if (item == null)
                return;

            int weaponType = Convert.ToInt32(item.Tags["WeaponType"]);

            GET_AMMO_IN_CHAR_WEAPON(playerPedHandle, weaponType, out int ammo);

            REMOVE_WEAPON_FROM_CHAR(playerPedHandle, weaponType);

            GET_CHAR_COORDINATES(playerPedHandle, out Vector3 pos);

            if (range != 0.0F)
                pos = pos.Around(range);

            // Disable ability for local player to any pickups
            DISABLE_LOCAL_PLAYER_PICKUPS(true);
            blockPlayerAbilityToCollectPickup = true;
            lastPickupPosition = pos;

            // Creates a weapon pickup at the players location
            CreateWeaponPickupAtPosition(pos, weaponType, ammo);

            // Removes the item out of the inventory
            inventory.RemoveItem(item);
        }

        private void BeginInvokeItemEventForSubscriber(BasicInventoryItem targetItem, string eventTag, string scriptCommand, object[] args)
        {
            try
            {
                Guid targetScriptId = (Guid)targetItem.Tags[eventTag];

                if (targetScriptId == Guid.Empty)
                    return;

                Guid itemId = targetItem.ID;

                mainThreadQueue.Enqueue(() =>
                {
                    if (SendScriptCommand(targetScriptId, scriptCommand, args, out object result))
                    {
                        Logging.LogDebug("'{0}' was sent to script '{1}' for item '{2}'.", scriptCommand, targetScriptId, itemId);
                    }
                    else
                    {
                        Logging.LogDebug("Could not send '{0}' command to script '{1}'!", scriptCommand, targetScriptId);
                    }
                });
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to send a script command to another script which wishes to receive event notifications. Details: {0}", ex);
            }
        }
        #endregion

        #region Functions
        private static float Lerp(float a, float b, float t)
        {
            // Clamp t between 0 and 1
            t = Math.Max(0.0f, Math.Min(1.0f, t));

            return a + (b - a) * t;
        }

        private int CreateWeaponPickupAtPosition(Vector3 pos, int weaponType, int ammo)
        {
            GET_WEAPONTYPE_MODEL(weaponType, out uint model);

            Vector3 spawnPos = NativeWorld.GetGroundPosition(pos) + new Vector3(0f, 0f, 0.05f);
            CREATE_PICKUP_ROTATE(model, (uint)ePickupType.PICKUP_TYPE_WEAPON, (uint)ammo, spawnPos, new Vector3(90f, 0f, GENERATE_RANDOM_FLOAT_IN_RANGE(0f, 90f)), out int pickup);

            // Add pickup to current interior the player is in
            GET_KEY_FOR_CHAR_IN_ROOM(playerPedHandle, out uint key);

            if (key != 0)
                ADD_PICKUP_TO_INTERIOR_ROOM_BY_KEY(pickup, key);
            
            // DOES_PICKUP_EXIST
            // REMOVE_PICKUP

            return pickup;
        }

        private BasicInventory FindInventory(Guid id)
        {
            // Find target inventory
            InventoryBase inventoryBase = inventoryPool.Get(id);

            if (inventoryBase == null)
                return null;

            // Convert inventory base to basic inventory
            return (BasicInventory)inventoryBase;
        }

        private BasicInventoryItem[] GetAllItemsWhichGotThisTag(BasicInventory inventory, string tag)
        {
            BasicInventoryItem[] items = inventory.GetItems();
            return items.Where(x => x.Tags.ContainsKey(tag)).ToArray();
        }
        public float GetTotalInventoryWeight(BasicInventory inventory)
        {
            BasicInventoryItem[] items = GetAllItemsWhichGotThisTag(inventory, "WEIGHT");

            float weight = 0.0f;

            for (int i = 0; i < items.Length; i++)
                weight += Convert.ToSingle(items[i].Tags["WEIGHT"]);

            return weight;
        }

        private bool SubscribeToItemEvent(Guid inventoryId, Guid itemId, Guid targetScriptId, string eventTag)
        {
            // Find target inventory
            BasicInventory inventory = FindInventory(inventoryId);

            if (inventory == null)
                return false;

            // Find target item
            BasicInventoryItem item = inventory.GetItem(itemId);

            if (item == null)
                return false;

            // Check if tag is already present
            if (item.Tags.ContainsKey(eventTag))
                return false;

            // Add tag to item
            item.Tags.Add(eventTag, targetScriptId);

            return true;
        }
        private bool UnsubscribeFromItemEvent(Guid inventoryId, Guid itemId, string eventTag)
        {
            // Find target inventory
            BasicInventory inventory = FindInventory(inventoryId);

            if (inventory == null)
                return false;

            // Find target item
            BasicInventoryItem item = inventory.GetItem(itemId);

            if (item == null)
                return false;

            // Check if tag is present
            if (!item.Tags.ContainsKey(eventTag))
                return false;

            // Remove tag from item
            return item.Tags.Remove(eventTag);
        }
        #endregion

        #region Events
        private void BasicInventory_OnItemDraggedOut(BasicInventory sender, BasicInventoryItem item, int itemIndex)
        {
            if (item.Tags.ContainsKey("IS_GAME_WEAPON"))
            {
                DropItem(sender, item);
            }

            // Raise API "PAC_ON_ITEM_DRAGGED_OUT" event for subscriber
            if (item.Tags.ContainsKey("WANTS_ON_ITEM_DRAGGED_OUT_EVENTS"))
                BeginInvokeItemEventForSubscriber(item, "WANTS_ON_ITEM_DRAGGED_OUT_EVENTS", "PAC_ON_ITEM_DRAGGED_OUT", new object[] { sender.ID, item.ID, itemIndex });
        }
        private void BasicInventory_OnItemDraggedToNewSlot(BasicInventory sender, BasicInventoryItem item, int oldIndex, int newIndex)
        {
            //IVGame.Console.PrintWarning(string.Format("Item {0} was dragged from slot {1} to {2}", item.TopLeftText, oldIndex, newIndex));

            // Raise API "PAC_ON_ITEM_DRAGGED_TO_NEW_SLOT" event for subscriber
            if (item.Tags.ContainsKey("WANTS_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENTS"))
                BeginInvokeItemEventForSubscriber(item, "WANTS_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENTS", "PAC_ON_ITEM_DRAGGED_TO_NEW_SLOT", new object[] { sender.ID, item.ID, oldIndex, newIndex });
        }
        private void BasicInventory_OnPopupItemClick(BasicInventory sender, BasicInventoryItem item, string popupItemName)
        {
            if (item.Tags.ContainsKey("IS_GAME_WEAPON"))
            {
                if (popupItemName == "Drop")
                {
                    DropItem(sender, item);
                }
            }

            // Raise API "PAC_ON_POPUP_ITEM_CLICKED" event for subscriber
            if (item.Tags.ContainsKey("WANTS_ON_POPUP_ITEM_CLICK_EVENTS"))
                BeginInvokeItemEventForSubscriber(item, "WANTS_ON_POPUP_ITEM_CLICK_EVENTS", "PAC_ON_POPUP_ITEM_CLICKED", new object[] { sender.ID, item.ID, popupItemName });
        }
        private void BasicInventory_OnItemClick(BasicInventory sender, BasicInventoryItem item, int itemIndex)
        {
            if (item.Tags.ContainsKey("IS_GAME_WEAPON"))
            {
                int weaponType = Convert.ToInt32(item.Tags["WeaponType"]);
                lastPlayerWeapon = weaponType;
                SET_CURRENT_CHAR_WEAPON(playerPedHandle, weaponType, false);
            }

            // Raise API "PAC_ON_ITEM_CLICKED" event for subscriber
            if (item.Tags.ContainsKey("WANTS_ON_ITEM_CLICK_EVENTS"))
                BeginInvokeItemEventForSubscriber(item, "WANTS_ON_ITEM_CLICK_EVENTS", "PAC_ON_ITEM_CLICKED", new object[] { sender.ID, item.ID, itemIndex });

            // Close inventory when inventory was opened using a controller
            if (wasInventoryOpenedViaController)
                CloseInventory();
        }
        private void BasicInventory_OnInventoryResized(BasicInventory target, List<BasicInventoryItem> leftBehindItems)
        {
            // Process any left behind items
            if (leftBehindItems != null)
            {
                for (int i = 0; i < leftBehindItems.Count; i++)
                {
                    BasicInventoryItem item = leftBehindItems[i];

                    if (item.Tags.ContainsKey("IS_GAME_WEAPON"))
                        DropItem(target, item, i * GENERATE_RANDOM_FLOAT_IN_RANGE(0.10f, 0.15f));
                }
            }
        }
        #endregion

        #region Constructor
        public Main()
        {
            // Lists
            mainThreadQueue = new Queue<Action>();
            loadedWeaponTextures = new Dictionary<int, ImTexture>(32);
            playerSuseSlots = new Dictionary<uint, List<SuseSlots>>();
            predefinedItemWeights = new Dictionary<string, float>();

            // Other
            inventoryKeyWatch = new Stopwatch();

            // Find constructor of the ImTexture class
            // This is a little hack until the ImTexture class exposes its constructor, or if it provides a way of creating a new instance of it
            imTextureCtor = typeof(ImTexture).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, Type.DefaultBinder, new Type[] { typeof(IntPtr), typeof(Size) }, Array.Empty<ParameterModifier>());
            Logging.LogDebug("Found ImTexture Constructor: {0}", imTextureCtor != null);

            // IV-SDK .NET stuff
            ForceNoAbort = true;
            Uninitialize += Main_Uninitialize;
            Initialized += Main_Initialized;
            GameLoad += Main_GameLoad;
            OnImGuiRendering += Main_OnImGuiRendering;
            ScriptCommandReceived += Main_ScriptCommandReceived;
            ProcessPad += Main_ProcessPad;
            Tick += Main_Tick;
        }
        #endregion

        private void Main_Uninitialize(object sender, EventArgs e)
        {
            IVTimer.TimeScale = 1.0f;

            UnloadTextures();

            if (loadedWeaponTextures != null)
            {
                loadedWeaponTextures.Clear();
                loadedWeaponTextures = null;
            }
            if (inventoryPool != null)
            {
                inventoryPool.Clear();
                inventoryPool = null;
            }
            if (playerInventory != null)
            {
                playerInventory.Clear();
                playerInventory = null;
            }
        }
        private void Main_Initialized(object sender, EventArgs e)
        {
            ModSettings.Load(Settings);
            LoadPlayerSuseSlots();
            LoadPredefiniedItemWeights();

            // Create inventory pool
            inventoryPool = new InventoryPool();

            // Create the inventory
            playerInventory = new BasicInventory("PlayerInventory", ModSettings.DefaultCapacity);
            playerInventory.OnItemDraggedOut += BasicInventory_OnItemDraggedOut;
            playerInventory.OnPopupItemClick += BasicInventory_OnPopupItemClick;
            playerInventory.OnItemClick += BasicInventory_OnItemClick;
            playerInventory.OnInventoryResized += BasicInventory_OnInventoryResized;
            playerInventory.OnItemDraggedToNewSlot += BasicInventory_OnItemDraggedToNewSlot;
            playerInventory.ItemSize = new Vector2(128f, 100f);

            inventoryPool.Add(playerInventory);
        }

        private void Main_GameLoad(object sender, EventArgs e)
        {
            if (playerInventory != null)
                playerInventory.Clear();

            LoadTextures();
        }

        private void Main_OnImGuiRendering(IntPtr devicePtr, ImGuiIV_DrawingContext ctx)
        {
            if (inventoryPool != null)
                inventoryPool.ProcessDrawing(ctx);
        }

        private object Main_ScriptCommandReceived(Script fromScript, object[] args, string command)
        {
            try
            {
                switch (command)
                {
                    // Inventory
                    case "GET_PLAYER_INVENTORY_ID":
                        {
                            if (playerInventory != null)
                                return playerInventory.ID;

                            return Guid.Empty;
                        }

                    case "GET_AMOUNT_OF_FREE_SLOTS_IN_INVENTORY":
                        {
                            if (args == null)
                                return -1;

                            Guid inventoryId = (Guid)args[0];

                            // Find target inventory
                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return 0;

                            return inventory.GetAmountOfFreeSlots();
                        }

                    // Item
                    case "ADD_NEW_ITEM_TO_INVENTORY":
                        {
                            if (args == null)
                                return Guid.Empty;

                            Guid inventoryId = (Guid)args[0];

                            // Find target inventory
                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return Guid.Empty;

                            // Check if there are any free slots in the inventory for the new item
                            if (inventory.GetAmountOfFreeSlots() == 0)
                                return Guid.Empty;

                            // Get item details
                            uint hash =                 Convert.ToUInt32(args[1]);
                            string buttonText =         Convert.ToString(args[2]);
                            string topLeftText =        Convert.ToString(args[3]);
                            string topRightText =       Convert.ToString(args[4]);
                            string bottomLeftText =     Convert.ToString(args[5]);
                            string bottomRightText =    Convert.ToString(args[6]);

                            // Create new item
                            BasicInventoryItem item = new BasicInventoryItem(hash);
                            item.ButtonText =       buttonText;
                            item.TopLeftText =      topLeftText;
                            item.TopRightText =     topRightText;
                            item.BottomLeftText =   bottomLeftText;
                            item.BottomRightText =  bottomRightText;

                            // Add item to inventory
                            if (!inventory.AddItem(item))
                            {
                                item = null;
                                return Guid.Empty;
                            }

                            item.Tags.Add("IS_CUSTOM_ITEM", fromScript.ID);
                            //item.Tags.Add("OWNER_SCRIPT_ID", fromScript.ID);

                            return item.ID;
                        }
                    case "REMOVE_ITEM_FROM_INVENTORY":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Try remove target item
                            return inventory.RemoveItem((Guid)args[1]);
                        }
                    case "DOES_ITEM_EXISTS_IN_INVENTORY":
                        {
                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Check if item exists in inventory
                            return inventory.ContainsItem((Guid)args[1]);
                        }

                    // Tag
                    case "ADD_TAG_TO_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Check if tag is already present
                            string tag = Convert.ToString(args[2]);

                            if (item.Tags.ContainsKey(tag))
                                return false;

                            // Add tag to item
                            item.Tags.Add(tag, args[3]);

                            return true;
                        }
                    case "REMOVE_TAG_FROM_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Check if tag is present
                            string tag = Convert.ToString(args[2]);

                            if (!item.Tags.ContainsKey(tag))
                                return false;

                            // Try remove tag from item
                            return item.Tags.Remove(tag);
                        }
                    case "DOES_ITEM_HAVE_TAG":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Check if tag is present
                            return item.Tags.ContainsKey(Convert.ToString(args[2]));
                        }

                    // Popup Item
                    case "ADD_POPUP_ITEM_TO_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Check if popup item is already present
                            string popupItem = Convert.ToString(args[2]);

                            if (item.PopupMenuItems.Contains(popupItem))
                                return false;

                            // Add popup item to item
                            item.PopupMenuItems.Add(popupItem);

                            return true;
                        }
                    case "REMOVE_POPUP_ITEM_FROM_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Check if popup item is already present
                            string popupItem = Convert.ToString(args[2]);

                            if (!item.PopupMenuItems.Contains(popupItem))
                                return false;

                            // Try remove popup item from item
                            return item.PopupMenuItems.Remove(popupItem);
                        }
                    case "DOES_ITEM_HAVE_POPUP_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Check if popup item is present
                            return item.PopupMenuItems.Contains(Convert.ToString(args[2]));
                        }

                    // Icon
                    case "ADD_ICON_TO_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Get icon details
                            IntPtr texture =    (IntPtr)args[2];
                            int width =         Convert.ToInt32(args[3]);
                            int height =        Convert.ToInt32(args[4]);

                            // Set icon
                            object rawObj = imTextureCtor.Invoke(new object[] { texture, new Size(width, height) });

                            if (rawObj == null)
                                return false;

                            item.Icon = (ImTexture)rawObj;

                            return true;
                        }
                    case "REMOVE_ICON_FROM_ITEM":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Set icon
                            item.Icon = null;

                            return true;
                        }

                    // Tooltip
                    case "SET_ITEM_TOOLTIP":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            // Set tooltip
                            item.ButtonTooltip = Convert.ToString(args[2]);

                            return true;
                        }

                    // Flags
                    case "SET_ITEM_NOT_REMOVED_FROM_INVENTORY_ON_MOD_UNLOAD":
                        {
                            if (args == null)
                                return false;

                            // Find target inventory
                            Guid inventoryId = (Guid)args[0];

                            BasicInventory inventory = FindInventory(inventoryId);

                            if (inventory == null)
                                return false;

                            // Find target item
                            Guid itemId = (Guid)args[1];

                            BasicInventoryItem item = inventory.GetItem(itemId);

                            if (item == null)
                                return false;

                            bool set = Convert.ToBoolean(args[2]);

                            // Check if tag is already present
                            string tag = "DO_NOT_REMOVE_ITEM_WHEN_MOD_THAT_ADDED_IT_UNLOADS";

                            if (set)
                            {
                                if (!item.Tags.ContainsKey(tag))
                                    item.Tags.Add(tag, null);
                            }
                            else
                            {
                                if (item.Tags.ContainsKey(tag))
                                    item.Tags.Remove(tag);
                            }

                            return true;
                        }

                    // Events
                    case "SUBSCRIBE_TO_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return SubscribeToItemEvent(inventoryId, itemId, fromScript.ID, "WANTS_ON_ITEM_DRAGGED_OUT_EVENTS");
                        }
                    case "UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return UnsubscribeFromItemEvent(inventoryId, itemId, "WANTS_ON_ITEM_DRAGGED_OUT_EVENTS");
                        }

                    case "SUBSCRIBE_TO_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return SubscribeToItemEvent(inventoryId, itemId, fromScript.ID, "WANTS_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENTS");
                        }
                    case "UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return UnsubscribeFromItemEvent(inventoryId, itemId, "WANTS_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENTS");
                        }

                    case "SUBSCRIBE_TO_ON_ITEM_CLICK_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return SubscribeToItemEvent(inventoryId, itemId, fromScript.ID, "WANTS_ON_ITEM_CLICK_EVENTS");
                        }
                    case "UNSUBSCRIBE_FROM_ON_ITEM_CLICK_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return UnsubscribeFromItemEvent(inventoryId, itemId, "WANTS_ON_ITEM_CLICK_EVENTS");
                        }

                    case "SUBSCRIBE_TO_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return SubscribeToItemEvent(inventoryId, itemId, fromScript.ID, "WANTS_ON_POPUP_ITEM_CLICK_EVENTS");
                        }
                    case "UNSUBSCRIBE_FROM_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM":
                        {
                            if (args == null)
                                return false;

                            Guid inventoryId = (Guid)args[0];
                            Guid itemId = (Guid)args[1];

                            return UnsubscribeFromItemEvent(inventoryId, itemId, "WANTS_ON_POPUP_ITEM_CLICK_EVENTS");
                        }

                }
            }
            catch (Exception ex)
            {
                string senderScriptName =   fromScript != null ? fromScript.GetName() : "UNKNOWN";
                string targetCommand =      command != null ? command : "UNKNOWN";

                Logging.LogDebug("Command '{0}' which was sent by script '{1}' caused an exception! Details: {2}", targetCommand, senderScriptName, ex);
            }

            return null;
        }

        private void Main_ProcessPad(UIntPtr padPtr)
        {
            //if (playerInventory.IsVisible)
            //{
            //    IVPad.FromUIntPtr(padPtr).Values[(int)ePadControls.INPUT_ATTACK].CurrentValue = 0;
            //}
        }

        private void Main_Tick(object sender, EventArgs e)
        {
            if (ModSettings.DisableInMP && IVNetwork.IsNetworkGameRunning())
                return;
            
            // Process queue
            while (mainThreadQueue.Count != 0)
                mainThreadQueue.Dequeue()?.Invoke();

            // Display custom tutorials
            DisplayCustomMissionIntroductionText();

            // Get player stuff
            IVPed playerPed = IVPed.FromUIntPtr(IVPlayerInfo.FindThePlayerPed());
            playerPedHandle = playerPed.GetHandle();

            // Load weapon textures if not loaded yet
            if (loadedWeaponTextures.Count == 0)
                LoadTextures();

            // Handle inventory open/closing and quick weapon switch to last weapon or fist
            bool wantsToOpenViaController = NativeControls.IsUsingJoypad() && NativeControls.IsControllerButtonPressed(0, ControllerButton.BUTTON_BUMPER_LEFT);
            if ((IsKeyPressed(Keys.Tab) || wantsToOpenViaController) && !playerPed.IsInVehicle())
            {
                if (inventoryKeyWatch.IsRunning)
                {
                    if (inventoryKeyWatch.ElapsedMilliseconds > InventoryOpenTimeInMS)
                    {
                        if (!playerInventory.IsVisible)
                        {
                            if (!wasCursorPosSet)
                            {
                                GET_SCREEN_RESOLUTION(out int x, out int y);
                                SetCursorPos(x / 2, y / 2);

                                wasCursorPosSet = true;
                            }

                            OpenInventory(wantsToOpenViaController);
                        }
                    }
                }
                else
                {
                    inventoryKeyWatch.Start();
                }
            }
            else
            {
                if (inventoryKeyWatch.IsRunning)
                {
                    if (inventoryKeyWatch.ElapsedMilliseconds < InventoryOpenTimeInMS)
                    {
                        // Switch to last weapon or fist
                        GET_CURRENT_CHAR_WEAPON(playerPedHandle, out int currentWeapon);

                        if ((eWeaponType)currentWeapon == eWeaponType.WEAPON_UNARMED)
                        {
                            if (HAS_CHAR_GOT_WEAPON(playerPedHandle, lastPlayerWeapon))
                                SET_CURRENT_CHAR_WEAPON(playerPedHandle, lastPlayerWeapon, false);
                        }
                        else
                        {
                            lastPlayerWeapon = currentWeapon;
                            SET_CURRENT_CHAR_WEAPON(playerPedHandle, (int)eWeaponType.WEAPON_UNARMED, false);
                        }
                    }

                    inventoryKeyWatch.Reset();

                    if (!wasInventoryOpenedViaController)
                        playerInventory.IsVisible = false;
                }
            }

            // Do stuff when inventory is open or not
            if (playerInventory.IsVisible)
            {
                // Do stuff only when inventory was opened via controller
                if (wasInventoryOpenedViaController)
                {
                    if (!playerInventory.IsAnyItemFocused)
                    {
                        if (ImGuiIV.IsKeyDown(eImGuiKey.ImGuiKey_GamepadFaceRight))
                            CloseInventory();
                    }
                    else
                    {
                        if (ImGuiIV.IsKeyDown(eImGuiKey.ImGuiKey_GamepadFaceUp))
                        {
                            BasicInventoryItem item = playerInventory.GetFocusedItem();

                            if (item != null)
                                DropItem(playerInventory, item);
                        }
                    }
                }

                // Position the inventory at the players head
                GET_PED_BONE_POSITION(playerPedHandle, (uint)eBone.BONE_HEAD, Vector3.Zero, out Vector3 headPos);
                playerInventory.PositionAtWorldCoordinate(headPos);
            }
            else
            {
                // Reset some states
                wasCursorPosSet = false;
            }

            // Handle some other stuff
            ResizeInventoryBasedOnClothes();
            CheckForInvalidScriptItems();
            HandleTimescaleInterpolation();
            HandlePreventPlayerToCollectPickup();
            HandlePlayerImpacts(playerPed);

            // Update all inventory items
            BasicInventoryItem[] items = playerInventory.GetItems();
            for (int i = 0; i < items.Length; i++)
            {
                BasicInventoryItem item = items[i];

                // Check if this item is a included weapon
                if (!item.Tags.ContainsKey("IS_GAME_WEAPON"))
                    continue;

                // Get what kind of weapon this item is supposed to be
                int weaponType = Convert.ToInt32(item.Tags["WeaponType"]);

                // Check if player still has this weapon
                if (!HAS_CHAR_GOT_WEAPON(playerPedHandle, weaponType))
                {
                    // Remove the item
                    playerInventory.RemoveItem(item);
                    continue;
                }

                GET_AMMO_IN_CHAR_WEAPON(playerPedHandle, weaponType, out int ammo);

                // Check if ammo in weapon is not zero
                if (ammo == 0)
                {
                    // Remove the item
                    playerInventory.RemoveItem(item);
                    continue;
                }

                // Update the item
                switch ((eWeaponType)weaponType)
                {
                    case eWeaponType.WEAPON_BASEBALLBAT:
                    case eWeaponType.WEAPON_KNIFE:
                    case eWeaponType.WEAPON_POOLCUE:
                        item.BottomLeftText = "1x";
                        break;

                    case eWeaponType.WEAPON_GRENADE:
                    case eWeaponType.WEAPON_MOLOTOV:
                    case eWeaponType.WEAPON_EPISODIC_8:
                    case eWeaponType.WEAPON_EPISODIC_16:
                        item.BottomLeftText = string.Format("{0}x", ammo);
                        break;

                    case eWeaponType.WEAPON_RLAUNCHER:
                        item.BottomLeftText = string.Format("{0} Rockets", ammo);
                        break;

                    default:
                        item.BottomLeftText = string.Format("{0} Bullets", ammo);
                        break;
                }
            }

            // Automatically add vanilla weapons to inventory
            for (int i = 0; i < playerPed.WeaponData.Weapons.Length; i++)
            {
                GET_CHAR_WEAPON_IN_SLOT(playerPedHandle, i, out int weaponType, out int ammo0, out int ammo1);

                eWeaponType type = (eWeaponType)weaponType;

                // Ignore some weapon types
                switch (type)
                {
                    case eWeaponType.WEAPON_UNARMED:
                    case eWeaponType.WEAPON_WEAPONTYPE_LAST_WEAPONTYPE:
                    case eWeaponType.WEAPON_ARMOUR:
                    case eWeaponType.WEAPON_RAMMEDBYCAR:
                    case eWeaponType.WEAPON_RUNOVERBYCAR:
                    case eWeaponType.WEAPON_EXPLOSION:
                    case eWeaponType.WEAPON_UZI_DRIVEBY:
                    case eWeaponType.WEAPON_DROWNING:
                    case eWeaponType.WEAPON_FALL:
                    case eWeaponType.WEAPON_UNIDENTIFIED:
                    case eWeaponType.WEAPON_ANYMELEE:
                    case eWeaponType.WEAPON_ANYWEAPON:
                        continue;
                }

                uint weaponTypeHash = RAGE.AtStringHash(type.ToString());

                // Add item if it wasn't added yet
                if (!playerInventory.ContainsItem(weaponTypeHash) && ammo0 != 0)
                {
                    BasicInventoryItem item = new BasicInventoryItem(weaponTypeHash);
                    item.Tags.Add("IS_GAME_WEAPON", null);
                    item.Tags.Add("WeaponType", weaponType);

                    string weaponName = NativeGame.GetCommonWeaponName(type);

                    item.PopupMenuItems.Add("Drop");
                    item.TopLeftText = weaponName;

                    if (loadedWeaponTextures.ContainsKey(weaponType))
                        item.Icon = loadedWeaponTextures[weaponType];

                    if (predefinedItemWeights.ContainsKey(weaponName))
                    {
                        float weight = predefinedItemWeights[weaponName];
                        item.Tags.Add("WEIGHT", weight);
                        item.BottomRightText = weight.ToString() + "lb";
                        item.BottomRightColor = Color.FromArgb(100, item.BottomRightColor);
                    }

                    playerInventory.AddItem(item);
                }
            }
        }

    }
}
