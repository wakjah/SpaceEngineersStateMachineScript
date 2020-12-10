using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class IntegrityUtils
        {
            public static bool isConnectedToCargo(IMyCargoContainer container, List<IMyBlockGroup> groups)
            {
                foreach (IMyBlockGroup group in groups)
                {
                    if (!isConnectedToCargo(container, group))
                    {
                        return false;
                    }
                }
                return true;
            }

            private static readonly MyItemType ICE_ITEM = MyItemType.MakeOre("Ice");
            private static readonly List<IMyTerminalBlock> _tempBlocks = new List<IMyTerminalBlock>();

            public static bool isConnectedToCargo(IMyCargoContainer container, IMyBlockGroup group)
            {
                _tempBlocks.Clear();
                group.GetBlocksOfType(_tempBlocks);
                return isConnectedToCargo(container, _tempBlocks);
            }

            public static bool isConnectedToCargo(IMyCargoContainer container, IReadOnlyCollection<IMyTerminalBlock> blocks)
            {
                foreach (IMyTerminalBlock block in blocks)
                {
                    if (!isConnectedToCargo(container, block))
                    {
                        return false;
                    }
                }
                return true;
            }

            public static bool isConnectedToCargo(IMyCargoContainer container, IMyTerminalBlock block)
            {
                if (container.InventoryCount == 0)
                {
                    return false;
                }
                IMyInventory inventory = container.GetInventory(0);

                if (block.InventoryCount == 0)
                {
                    return true;
                }

                return block.GetInventory(0).CanTransferItemTo(inventory, ICE_ITEM);
            }

            public static bool isFunctional(IMyBlockGroup group)
            {
                _tempBlocks.Clear();
                group.GetBlocks(_tempBlocks);
                return isFunctional(_tempBlocks);
            }

            public static bool isFunctional(IReadOnlyCollection<IMyTerminalBlock> group)
            {
                bool functional = true;
                foreach (IMyTerminalBlock block in group)
                {
                    functional &= isFunctional(block);
                }
                return functional;
            }

            public static bool isFunctional(IMyTerminalBlock block)
            {
                if (block is IMyMechanicalConnectionBlock)
                {
                    IMyAttachableTopBlock top = (block as IMyMechanicalConnectionBlock).Top;
                    if (top == null || !top.IsFunctional)
                    {
                        return false;
                    }
                }
                return block.IsFunctional;
            }
        }
    }
}
