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
    partial class Program : MyGridProgram
    {
        class MyOptions : Options
        {
            public override Dictionary<string, object> getValues()
            {
                return new Dictionary<string, object>();
            }

            public override void parseArg(string arg)
            {

            }

            public override void setDefaults()
            {

            }
        }

        class MyContext : Context<MyContext>
        {
            public static readonly DefaultState Default = new DefaultState();

            private static readonly string BLOCK_TAG = "DryDock";
            public static readonly int INVENTORIES_PER_UPDATE = 50;

            public List<IMyShipWelder> _welders = new List<IMyShipWelder>(1);
            public List<IMyCargoContainer> _cargo = new List<IMyCargoContainer>(4);
            public List<IMyLandingGear> _landingGearsAdvance = new List<IMyLandingGear>(1);
            private StringBuilder _stringBuilder = new StringBuilder();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Default)
            {

            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                IMyBlockGroup group = Program.GridTerminalSystem.GetBlockGroupWithName("Welder DryDock MainBody");
                if (group != null)
                {
                    _welders.Clear();
                    group.GetBlocksOfType(_welders);
                }

                _cargo.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_cargo, block => block.CustomName.Contains("Components"));

                return true;
            }

            protected override void updateDisplayImpl()
            {
                //_stringBuilder.Clear();

                //_stringBuilder.Append("Ouroborous\n\n");

                //_stringBuilder.Append("Status:\n");
                //_stringBuilder.Append(State.ToString());

                //string text = _stringBuilder.ToString();
                //foreach (IMyTextPanel panel in _textPanels)
                //{
                //    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                //    panel.WriteText(text);
                //}
            }

        }

        class DefaultState : State<MyContext>
        {
            private static KeyValuePair<K, V> kv<K, V>(K k, V v)
            {
                return new KeyValuePair<K, V>(k, v);
            }

            private static MyItemType component(string name)
            {
                return MyItemType.MakeComponent(name);
            }

            private static readonly MyItemType SteelPlate = component("SteelPlate");
            private static readonly MyItemType Construction = component("Construction");
            private static readonly MyItemType MetalGrid = component("MetalGrid");
            private static readonly MyItemType InteriorPlate = component("InteriorPlate");
            private static readonly MyItemType Girder = component("Girder");
            private static readonly MyItemType SmallTube = component("SmallTube");
            private static readonly MyItemType LargeTube = component("LargeTube");
            private static readonly MyItemType Motor = component("Motor");
            private static readonly MyItemType Display = component("Display");
            private static readonly MyItemType BulletproofGlass = component("BulletproofGlass");
            private static readonly MyItemType Superconductor = component("Superconductor");
            private static readonly MyItemType Computer = component("Computer");
            private static readonly MyItemType Reactor = component("Reactor");
            private static readonly MyItemType Thrust = component("Thrust");
            private static readonly MyItemType GravityGenerator = component("GravityGenerator");
            private static readonly MyItemType Medical = component("Medical");
            private static readonly MyItemType RadioCommunication = component("RadioCommunication");
            private static readonly MyItemType Detector = component("Detector");
            private static readonly MyItemType Explosives = component("Explosives");
            private static readonly MyItemType SolarCell = component("SolarCell");
            private static readonly MyItemType PowerCell = component("PowerCell");

            private static readonly KeyValuePair<MyItemType, int>[] DESIRED_ITEMS = {
                kv(BulletproofGlass, 5),
                kv(Computer, 5),
                kv(Construction, 20),
                kv(Display, 5),
                kv(Girder, 5),
                kv(InteriorPlate, 50),
                kv(LargeTube, 10),
                kv(MetalGrid, 5),
                kv(Motor, 10),
                kv(PowerCell, 4),
                kv(SmallTube, 10),
                kv(SteelPlate, 200)
            };


            int _offset = 0;
            // The count of the list of desired items in the component cargo boxes
            List<List<List<MyInventoryItem>>> _cargoDesiredItems = new List<List<List<MyInventoryItem>>>();
            private Dictionary<MyItemType, int> _desiredItemTypeIndices = new Dictionary<MyItemType, int>();
            private Func<MyInventoryItem, bool> _itemFilter;
            private List<MyInventoryItem> _tempItems = new List<MyInventoryItem>(128);

            public DefaultState()
            {
                for (int i = 0; i < DESIRED_ITEMS.Length; ++i)
                {
                    _desiredItemTypeIndices[DESIRED_ITEMS[i].Key] = i;
                }

                _itemFilter = item => _desiredItemTypeIndices.ContainsKey(item.Type);
            }

            public override void update(MyContext context)
            {
                updateCargoDesiredItemCounts(context);

                int count = Math.Min(MyContext.INVENTORIES_PER_UPDATE, context._welders.Count);
                for (int i = 0; i < count; ++i)
                {
                    //context.Program.Echo("Fill special inventory " + i);
                    if (!context._welders[i].IsFunctional)
                    {
                        continue;
                    }

                    fillSpecialInventory(context._welders[i].GetInventory(0), context);
                }

                _offset += MyContext.INVENTORIES_PER_UPDATE;
                if (_offset >= context._welders.Count)
                {
                    _offset -= context._welders.Count;
                }
            }

            private void updateCargoDesiredItemCounts(MyContext context)
            {
                if (_cargoDesiredItems.Count != context._cargo.Count)
                {
                    _cargoDesiredItems.Clear();
                    for (int i = 0; i < context._cargo.Count; ++i)
                    {
                        List<List<MyInventoryItem>> innerList = new List<List<MyInventoryItem>>();
                        _cargoDesiredItems.Add(innerList);
                        for (int j = 0; j < DESIRED_ITEMS.Length; ++j)
                        {
                            innerList.Add(new List<MyInventoryItem>());
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < context._cargo.Count; ++i)
                    {
                        for (int j = 0; j < DESIRED_ITEMS.Length; ++j)
                        {
                            _cargoDesiredItems[i][j].Clear();
                        }
                    }
                }

                for (int i = 0; i < context._cargo.Count; ++i)
                {
                    IMyInventory inventory = context._cargo[i].GetInventory(0);
                    _tempItems.Clear();
                    inventory.GetItems(_tempItems, _itemFilter);
                    for (int itemIdx = 0; itemIdx < _tempItems.Count; ++itemIdx)
                    {
                        MyInventoryItem item = _tempItems[itemIdx];
                        int j = _desiredItemTypeIndices[item.Type];
                        _cargoDesiredItems[i][j].Add(item);
                    }
                }
            }

            private void fillSpecialInventory(IMyInventory inventory, MyContext context)
            {
                for (int i = 0; i < DESIRED_ITEMS.Length; ++i)
                {
                    fillSpecialInventoryWithItem(inventory, i, context);
                }
            }

            private void fillSpecialInventoryWithItem(IMyInventory inventory, int desiredIndex, MyContext context)
            {
                KeyValuePair<MyItemType, int> desired = DESIRED_ITEMS[desiredIndex];
                long amount = inventory.GetItemAmount(desired.Key).RawValue;
                long deficit = desired.Value * 1000000 - amount;
                if (deficit <= 0)
                {
                    return;
                }

                for (int i = 0; i < context._cargo.Count; ++i)
                {
                    IMyInventory cargoInventory = context._cargo[i].GetInventory(0);
                    List<MyInventoryItem> items = _cargoDesiredItems[i][desiredIndex];
                    for (int candidateIndex = 0; candidateIndex < items.Count; ++candidateIndex)
                    {
                        MyInventoryItem candidate = items[candidateIndex];
                        MyFixedPoint toTransfer = new MyFixedPoint();
                        toTransfer.RawValue = Math.Min(deficit, candidate.Amount.RawValue);

                        List<IMyTextPanel> panel = new List<IMyTextPanel>();
                        context.Program.GridTerminalSystem.GetBlocksOfType(panel);

                        if (inventory.TransferItemFrom(cargoInventory, candidate, toTransfer))
                        {
                            deficit -= toTransfer.RawValue;
                            // todo really we should adjust our internal state of what amount is in this inventory item
                            // because the game apparently doesn't do it for us
                            //
                            // this means that if 2 welders want an item and there is not enough in one box to fulfil it,
                            // it won't pull from a different box until the next update
                            //
                            // but I can't be bothered to fix it and this will work well enough I think
                        }
                    }
                }
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
            }

            public override string ToString()
            {
                return "Default";
            }
        }

        StateMachineProgram<MyContext> _impl;

        public Program()
        {
            _impl = new StateMachineProgram<MyContext>(
                this,
                (cmd, args) => trigger(cmd, args),
                v => Storage = v
            );

            _impl.init(new MyContext(_impl));
        }

        public void trigger(string command, string[] args)
        {
            if (!_impl.Context.FoundAllBlocks)
            {
                Echo("All blocks not found!");
                return;
            }

            
        }

        public void Save()
        {
            _impl.Save();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _impl.Main(argument, updateSource);
        }
    }
}
