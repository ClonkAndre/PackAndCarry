using System;
using System.Drawing;

using IVSDKDotNet;

namespace PACApiTest
{
    public class Main : Script
    {

        #region Variables
        public bool MenuOpen;
        private ImTexture texture;

        // Inventory
        public Guid PlayerInventoryID;

        // Item
        private Guid addedItemID;
        private string itemHash;
        private string buttonText;
        private string topLeftText;
        private string topRightText;
        private string bottomLeftText;
        private string bottomRightText;

        // Tag
        private string tagToAdd;
        private string tagValue;

        // Popup Item
        private string popupToAdd;

        // Tooltip
        private string tooltipToSet;

        // Flags
        private bool set;
        #endregion

        #region Constructor
        public Main()
        {
            ScriptCommandReceived += Main_ScriptCommandReceived;
            OnFirstD3D9Frame += Main_OnFirstD3D9Frame;
            OnImGuiRendering += Main_OnImGuiRendering;
            Tick += Main_Tick;
        }
        #endregion

        private object Main_ScriptCommandReceived(Script fromScript, object[] args, string command)
        {
            switch (command)
            {
                case "PAC_ON_ITEM_DRAGGED_OUT":
                    {
                        Guid inventoryId = (Guid)args[0];
                        Guid itemId = (Guid)args[1];
                        int itemIndex = (int)args[2];

                        IVGame.Console.PrintWarningEx("Received PAC_ON_ITEM_DRAGGED_OUT script command from inventory {0} for item {1} at index {2}", inventoryId, itemId, itemIndex);
                    }
                    break;
                case "PAC_ON_ITEM_DRAGGED_TO_NEW_SLOT":
                    {
                        Guid inventoryId = (Guid)args[0];
                        Guid itemId = (Guid)args[1];
                        int oldIndex = (int)args[2];
                        int newIndex = (int)args[3];

                        IVGame.Console.PrintWarningEx("Received PAC_ON_ITEM_DRAGGED_TO_NEW_SLOT script command from inventory {0} for item {1}. Moved from slot {2} to {3}", inventoryId, itemId, oldIndex, newIndex);
                    }
                    break;
                case "PAC_ON_ITEM_CLICKED":
                    {
                        Guid inventoryId = (Guid)args[0];
                        Guid itemId = (Guid)args[1];
                        int itemIndex = (int)args[2];

                        IVGame.Console.PrintWarningEx("Received PAC_ON_ITEM_CLICKED script command from inventory {0} for item {1} at {2}", inventoryId, itemId, itemIndex);
                    }
                    break;
                case "PAC_ON_POPUP_ITEM_CLICKED":
                    {
                        Guid inventoryId = (Guid)args[0];
                        Guid itemId = (Guid)args[1];
                        string popupItemName = (string)args[2];

                        IVGame.Console.PrintWarningEx("Received PAC_ON_POPUP_ITEM_CLICKED script command from inventory {0} for item {1}. Clicked on {2}", inventoryId, itemId, popupItemName);
                    }
                    break;
            }

            return null;
        }

        private void Main_OnFirstD3D9Frame(IntPtr devicePtr)
        {
            ImGuiIV.CreateTextureFromMemory(Properties.Resources.crosshair, out texture, out IVSDKDotNet.Enums.eResult result);
        }
        private void Main_OnImGuiRendering(IntPtr devicePtr, ImGuiIV_DrawingContext ctx)
        {
            if (!MenuOpen)
                return;

            ImGuiIV.Begin("PAC API Test", ref MenuOpen);

            // Inventory
            ImGuiIV.TextDisabled("Inventory");

            if (ImGuiIV.Button("GET_PLAYER_INVENTORY_ID"))
            {
                if (SendScriptCommand("PackAndCarry", "GET_PLAYER_INVENTORY_ID", null, out object result))
                {
                    PlayerInventoryID = (Guid)result;
                    IVGame.Console.PrintEx("PlayerInventoryID: {0}", PlayerInventoryID);
                }
            }

            if (PlayerInventoryID == Guid.Empty)
            {
                ImGuiIV.TextColored(Color.Yellow, "Get the player inventory id first to be able to interact with it");
                return;
            }

            if (ImGuiIV.Button("GET_AMOUNT_OF_FREE_SLOTS_IN_INVENTORY"))
            {
                if (SendScriptCommand("PackAndCarry", "GET_AMOUNT_OF_FREE_SLOTS_IN_INVENTORY", new object[] { PlayerInventoryID }, out object result))
                {
                    IVGame.Console.PrintEx("Free slots in player inventory: {0}", Convert.ToInt32(result));
                }
            }

            // Item
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Item");

            if (ImGuiIV.TreeNode("ADD_NEW_ITEM_TO_INVENTORY"))
            {
                ImGuiIV.InputText("itemHash", ref itemHash);
                ImGuiIV.InputText("buttonText", ref buttonText);
                ImGuiIV.InputText("topLeftText", ref topLeftText);
                ImGuiIV.InputText("topRightText", ref topRightText);
                ImGuiIV.InputText("bottomLeftText", ref bottomLeftText);
                ImGuiIV.InputText("bottomRightText", ref bottomRightText);

                if (ImGuiIV.Button("ADD_NEW_ITEM_TO_INVENTORY"))
                {
                    object[] args = new object[] { PlayerInventoryID, Convert.ToUInt32(itemHash.Replace("0x", "")), buttonText, topLeftText, topRightText, bottomLeftText, bottomRightText };
                    if (SendScriptCommand("PackAndCarry", "ADD_NEW_ITEM_TO_INVENTORY", args, out object result))
                    {
                        addedItemID = (Guid)result;
                        IVGame.Console.PrintEx("Added item id: {0}", addedItemID);
                    }
                }

                ImGuiIV.TreePop();
            }

            if (addedItemID == Guid.Empty)
            {
                ImGuiIV.TextColored(Color.Yellow, "Add a new item to the player inventory first to be ablet to interact with it");
                return;
            }

            if (ImGuiIV.Button("REMOVE_ITEM_FROM_INVENTORY"))
            {
                if (SendScriptCommand("PackAndCarry", "REMOVE_ITEM_FROM_INVENTORY", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    bool res = Convert.ToBoolean(result);
                    IVGame.Console.PrintEx("REMOVE_ITEM_FROM_INVENTORY result: {0}", res);

                    if (res)
                        addedItemID = Guid.Empty;
                }
            }
            if (ImGuiIV.Button("DOES_ITEM_EXISTS_IN_INVENTORY"))
            {
                if (SendScriptCommand("PackAndCarry", "DOES_ITEM_EXISTS_IN_INVENTORY", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("DOES_ITEM_EXISTS_IN_INVENTORY result: {0}", Convert.ToBoolean(result));
                }
            }

            // Tag
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Tag");

            ImGuiIV.InputText("tagToAdd", ref tagToAdd);
            ImGuiIV.InputText("tagValue", ref tagValue);

            if (ImGuiIV.Button("ADD_TAG_TO_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "ADD_TAG_TO_ITEM", new object[] { PlayerInventoryID, addedItemID, tagToAdd, tagValue }, out object result))
                {
                    IVGame.Console.PrintEx("ADD_TAG_TO_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("REMOVE_TAG_FROM_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "REMOVE_TAG_FROM_ITEM", new object[] { PlayerInventoryID, addedItemID, tagToAdd }, out object result))
                {
                    IVGame.Console.PrintEx("REMOVE_TAG_FROM_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("DOES_ITEM_HAVE_TAG"))
            {
                if (SendScriptCommand("PackAndCarry", "DOES_ITEM_HAVE_TAG", new object[] { PlayerInventoryID, addedItemID, tagToAdd }, out object result))
                {
                    IVGame.Console.PrintEx("DOES_ITEM_HAVE_TAG result: {0}", Convert.ToBoolean(result));
                }
            }

            // Popup Item
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Popup Item");

            ImGuiIV.InputText("popupToAdd", ref popupToAdd);

            if (ImGuiIV.Button("ADD_POPUP_ITEM_TO_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "ADD_POPUP_ITEM_TO_ITEM", new object[] { PlayerInventoryID, addedItemID, popupToAdd }, out object result))
                {
                    IVGame.Console.PrintEx("ADD_POPUP_ITEM_TO_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("REMOVE_POPUP_ITEM_FROM_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "REMOVE_POPUP_ITEM_FROM_ITEM", new object[] { PlayerInventoryID, addedItemID, popupToAdd }, out object result))
                {
                    IVGame.Console.PrintEx("REMOVE_POPUP_ITEM_FROM_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("DOES_ITEM_HAVE_POPUP_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "DOES_ITEM_HAVE_POPUP_ITEM", new object[] { PlayerInventoryID, addedItemID, popupToAdd }, out object result))
                {
                    IVGame.Console.PrintEx("DOES_ITEM_HAVE_POPUP_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }

            // Icon
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Icon");

            if (ImGuiIV.Button("ADD_ICON_TO_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "ADD_ICON_TO_ITEM", new object[] { PlayerInventoryID, addedItemID, texture.GetTexture(), texture.GetWidth(), texture.GetHeight() }, out object result))
                {
                    IVGame.Console.PrintEx("ADD_ICON_TO_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("REMOVE_ICON_FROM_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "REMOVE_ICON_FROM_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("REMOVE_ICON_FROM_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }

            // Tooltip
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Tooltip");

            ImGuiIV.InputText("tooltipToSet", ref tooltipToSet);

            if (ImGuiIV.Button("SET_ITEM_TOOLTIP"))
            {
                if (SendScriptCommand("PackAndCarry", "SET_ITEM_TOOLTIP", new object[] { PlayerInventoryID, addedItemID, tooltipToSet }, out object result))
                {
                    IVGame.Console.PrintEx("SET_ITEM_TOOLTIP result: {0}", Convert.ToBoolean(result));
                }
            }

            // Flags
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Flags");

            ImGuiIV.CheckBox("set", ref set);

            if (ImGuiIV.Button("SET_ITEM_NOT_REMOVED_FROM_INVENTORY_ON_MOD_UNLOAD"))
            {
                if (SendScriptCommand("PackAndCarry", "SET_ITEM_NOT_REMOVED_FROM_INVENTORY_ON_MOD_UNLOAD", new object[] { PlayerInventoryID, addedItemID, set }, out object result))
                {
                    IVGame.Console.PrintEx("SET_ITEM_NOT_REMOVED_FROM_INVENTORY_ON_MOD_UNLOAD result: {0}", Convert.ToBoolean(result));
                }
            }

            // Events
            ImGuiIV.Spacing(4);
            ImGuiIV.TextDisabled("Events");

            if (ImGuiIV.Button("SUBSCRIBE_TO_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "SUBSCRIBE_TO_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("SUBSCRIBE_TO_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_OUT_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }

            ImGuiIV.Spacing(2);

            if (ImGuiIV.Button("SUBSCRIBE_TO_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "SUBSCRIBE_TO_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("SUBSCRIBE_TO_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("UNSUBSCRIBE_FROM_ON_ITEM_DRAGGED_TO_NEW_SLOT_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }

            ImGuiIV.Spacing(2);

            if (ImGuiIV.Button("SUBSCRIBE_TO_ON_ITEM_CLICK_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "SUBSCRIBE_TO_ON_ITEM_CLICK_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("SUBSCRIBE_TO_ON_ITEM_CLICK_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("UNSUBSCRIBE_FROM_ON_ITEM_CLICK_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "UNSUBSCRIBE_FROM_ON_ITEM_CLICK_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("UNSUBSCRIBE_FROM_ON_ITEM_CLICK_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }

            ImGuiIV.Spacing(2);

            if (ImGuiIV.Button("SUBSCRIBE_TO_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "SUBSCRIBE_TO_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("SUBSCRIBE_TO_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }
            if (ImGuiIV.Button("UNSUBSCRIBE_FROM_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM"))
            {
                if (SendScriptCommand("PackAndCarry", "UNSUBSCRIBE_FROM_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM", new object[] { PlayerInventoryID, addedItemID }, out object result))
                {
                    IVGame.Console.PrintEx("UNSUBSCRIBE_FROM_ON_POPUP_ITEM_CLICK_EVENT_FOR_ITEM result: {0}", Convert.ToBoolean(result));
                }
            }

            ImGuiIV.End();
        }

        private void Main_Tick(object sender, EventArgs e)
        {

        }

    }
}
