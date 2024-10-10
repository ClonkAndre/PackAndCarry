using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using CCL.GTAIV;

using CustomInventoryIV;
using CustomInventoryIV.Base;
using CustomInventoryIV.Inventories;

using PackAndCarry.Classes;

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
        private Dictionary<int, CITexture> loadedWeaponTextures;

        // Inventory stuff
        private BasicInventory playerInventory;
        private Stopwatch inventoryKeyWatch;
        private bool wasInventoryOpenedViaController;
        public int InventoryOpenTimeInMS = 150;

        // Pickup stuff
        private bool blockPlayerAbilityToCollectPickup;
        private Vector3 lastPickupPosition;

        // Other
        private int playerPedHandle;
        private int lastPlayerWeapon;
        private uint lastLoadedEpisode;

        private float timeScaleInterpolationValue;
        private bool wasCursorPosSet;
        #endregion

        #region Methods
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

        private void HandleTimescaleInterpolation()
        {
            if (IVNetwork.IsNetworkGameRunning())
                return;

            if (playerInventory.IsVisible)
            {
                timeScaleInterpolationValue = timeScaleInterpolationValue + 0.02f;

                if (timeScaleInterpolationValue > 1.0f)
                    timeScaleInterpolationValue = 1.0f;
            }
            else
            {
                timeScaleInterpolationValue = timeScaleInterpolationValue - 0.03f;

                if (timeScaleInterpolationValue < 0.0f)
                    timeScaleInterpolationValue = 0.0f;
            }

            IVTimer.TimeScale = Lerp(1.0f, 0.25f, timeScaleInterpolationValue);
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

        private void LoadTextures()
        {
            if (loadedWeaponTextures.Count != 0)
            {
                // Destroy loaded textures if changing episode
                if (lastLoadedEpisode != IVGame.CurrentEpisodeMenu)
                {
                    foreach (KeyValuePair<int, CITexture> item in loadedWeaponTextures)
                    {
                        IntPtr texture = item.Value.GetTexture();
                        ImGuiIV.ReleaseTexture(ref texture);
                    }
                    loadedWeaponTextures.Clear();
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
                    if (ImGuiIV.CreateTextureFromFile(string.Format("{0}\\{1}", path, fileName), out IntPtr txtPtr, out int w, out int h, out eResult r))
                    {
                        loadedWeaponTextures.Add(result, new CITexture(txtPtr, new Size(w, h)));
                    }
                    else
                    {
                        IVGame.Console.PrintError(string.Format("Failed to create texture {0}! Result: {1}", fileName, r));
                    }
                }
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
        #endregion

        #region Events
        private void BasicInventory_OnItemDraggedOut(BasicInventory sender, BasicInventoryItem item, int itemIndex)
        {
            if (sender.Name == "TestInventory")
            {
                DropItem(sender, item);
            }
        }
        private void BasicInventory_OnItemDraggedToNewSlot(BasicInventory sender, BasicInventoryItem item, int oldIndex, int newIndex)
        {
            IVGame.Console.PrintWarning(string.Format("Item {0} was dragged from slot {1} to {2}", item.TopLeftText, oldIndex, newIndex));
        }
        private void BasicInventory_OnPopupItemClick(BasicInventory sender, BasicInventoryItem item, string popupItemName)
        {
            if (sender.Name == "TestInventory")
            {
                if (popupItemName == "Drop")
                {
                    DropItem(sender, item);
                }
            }
        }
        private void BasicInventory_OnItemClick(BasicInventory sender, BasicInventoryItem item, int itemIndex)
        {
            if (item.Tags.ContainsKey("IS_GAME_WEAPON"))
            {
                int weaponType = Convert.ToInt32(item.Tags["WeaponType"]);
                lastPlayerWeapon = weaponType;
                SET_CURRENT_CHAR_WEAPON(playerPedHandle, weaponType, false);
            }

            if (wasInventoryOpenedViaController)
                CloseInventory();

            //if (itemIndex == 0)
            //{
            //    sender.Resize(12);
            //}
            //else if (itemIndex == 1)
            //{
            //    List<BasicInventoryItem> leftBehindItems = sender.Resize(8);

            //    if (leftBehindItems != null)
            //    {
            //        IVGame.Console.PrintError(string.Format("There are {0} left behind items:", leftBehindItems.Count));
            //        for (int i = 0; i < leftBehindItems.Count; i++)
            //        {
            //            BasicInventoryItem leftBehindItem = leftBehindItems[i];
            //            IVGame.Console.PrintWarning(leftBehindItem.TopLeftText);
            //        }
            //    }
            //    else
            //    {
            //        IVGame.Console.PrintError("There where no left behind items.");
            //    }
            //}
        }
        private void BasicInventory_OnInventoryResized(BasicInventory target, List<BasicInventoryItem> leftBehindItems)
        {
            if (leftBehindItems != null)
            {
                for (int i = 0; i < leftBehindItems.Count; i++)
                {
                    BasicInventoryItem item = leftBehindItems[i];

                    if (item.Tags.ContainsKey("IS_GAME_WEAPON"))
                        DropItem(target, item, i * 0.15f);
                }
            }
        }
        #endregion

        #region Constructor
        public Main()
        {
            // Lists
            loadedWeaponTextures = new Dictionary<int, CITexture>(32);

            // Other
            inventoryKeyWatch = new Stopwatch();

            // IV-SDK .NET stuff
            Uninitialize += Main_Uninitialize;
            Initialized += Main_Initialized;
            GameLoad += Main_GameLoad;
            OnImGuiRendering += Main_OnImGuiRendering;
            ScriptCommandReceived += Main_ScriptCommandReceived;
            Tick += Main_Tick;
        }
        #endregion

        private void Main_Uninitialize(object sender, EventArgs e)
        {
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

                            item.Tags.Add("IS_CUSTOM_ITEM", null);
                            item.Tags.Add("OWNER_SCRIPT_ID", fromScript.ID); // TODO: NEED TO ADD A WAY FOR SCRIPTS TO CHANGE THEIR ID

                            return item.ID;
                        }
                    case "REMOVE_ITEM_FROM_INVENTORY":
                        {
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

                    case "ADD_TAG_TO_ITEM":
                        {
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

                    case "ADD_POPUP_ITEM_TO_ITEM":
                        {
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

                    case "ADD_ICON_TO_ITEM":
                        {
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
                            item.Icon = new CITexture(texture, new Size(width, height));

                            return true;
                        }
                    case "REMOVE_ICON_FROM_ITEM":
                        {
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

                    case "SET_ITEM_NOT_REMOVED_FROM_INVENTORY_ON_MOD_UNLOAD":
                        {
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
                    case "SET_ITEM_TOOLTIP":
                        {
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

                    // Events (TODO)
                    case "SUBSCRIBE_TO_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM":
                        {
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
                            string tag = "WANTS_ON_POPUP_ITEM_CLICK_EVENTS";

                            if (item.Tags.ContainsKey(tag))
                                return false;

                            // Add tag to item
                            item.Tags.Add(tag, fromScript.ID); // TODO: NEED TO ADD A WAY FOR SCRIPTS TO CHANGE THEIR ID

                            return true;
                        }
                    case "UNSUBSCRIBE_FROM_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM":
                        {
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
                            string tag = "WANTS_ON_POPUP_ITEM_CLICK_EVENTS";

                            if (!item.Tags.ContainsKey(tag))
                                return false;

                            // Remove tag from item
                            item.Tags.Remove(tag);

                            return true;
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

        private void Main_Tick(object sender, EventArgs e)
        {
            if (ModSettings.DisableInMP && IVNetwork.IsNetworkGameRunning())
                return;

            // Get player stuff
            IVPed playerPed = IVPed.FromUIntPtr(IVPlayerInfo.FindThePlayerPed());
            playerPedHandle = playerPed.GetHandle();

            // Load weapon textures if not loaded yet
            if (loadedWeaponTextures.Count == 0)
                LoadTextures();

            // Handle inventory open/closing and quick weapon switch to last weapon or fist
            bool wantsToOpenViaController = NativeControls.IsUsingJoypad() && NativeControls.IsControllerButtonPressed(0, ControllerButton.BUTTON_BUMPER_LEFT);
            if (IsKeyPressed(Keys.Tab) || wantsToOpenViaController)
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
            HandleTimescaleInterpolation();
            HandlePreventPlayerToCollectPickup();

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

                    item.PopupMenuItems.Add("Drop");
                    item.TopLeftText = NativeGame.GetCommonWeaponName(type);

                    if (loadedWeaponTextures.ContainsKey(weaponType))
                        item.Icon = loadedWeaponTextures[weaponType];

                    playerInventory.AddItem(item);
                }
            }
        }

    }
}
