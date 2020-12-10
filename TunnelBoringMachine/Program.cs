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

        class RequiredInventory
        {
            class Item
            {
                public readonly long amountRequired;
                public long amountRemaining;

                public Item(long required)
                {
                    amountRequired = required;
                }
            }

            private Dictionary<string, Item> _amounts;
            private List<string> _missing = new List<string>();
            private List<MyInventoryItem> _items = new List<MyInventoryItem>(64);

            public RequiredInventory(Dictionary<string, double> amounts)
            {
                _amounts = new Dictionary<string, Item>();
                foreach (var kv in amounts)
                {
                    _amounts.Add(kv.Key, new Item((long)Math.Ceiling(kv.Value * 1000000)));
                }
            }

            public IReadOnlyCollection<string> findMissing(IReadOnlyCollection<IMyCargoContainer> cargo)
            {
                foreach (var kv in _amounts)
                {
                    kv.Value.amountRemaining = kv.Value.amountRequired;
                }

                //_missing.Clear(); // asdfasgoaeirgaoirngarg

                foreach (var c in cargo)
                {
                    findMissing(c);
                }

                _missing.Clear();
                foreach (var kv in _amounts)
                {
                    if (kv.Value.amountRemaining > 0)
                    {
                        double amountFound = (kv.Value.amountRequired - kv.Value.amountRemaining) / 1000000.0;
                        double amountRequired = kv.Value.amountRequired / 1000000.0;
                        _missing.Add(kv.Key + " (" + amountFound + " / " + amountRequired + ")");
                    }
                }

                _items.Clear();

                return _missing;
            }

            private void findMissing(IMyCargoContainer cargo)
            {
                for (int i = 0; i < cargo.InventoryCount; ++i)
                {
                    IMyInventory inventory = cargo.GetInventory(i);
                    _items.Clear();
                    //inventory.GetItems(_items);//, item => _amounts.ContainsKey(item.Type.SubtypeId));
                    inventory.GetItems(_items, item => _amounts.ContainsKey(item.Type.SubtypeId));

                    foreach (var item in _items)
                    {
                        /*if (item.Type.SubtypeId != "Stone")
                        {
                            _missing.Add(item.Type.SubtypeId + " " + item.Amount.RawValue / 1000000.0);
                        }*/
                        _amounts[item.Type.SubtypeId].amountRemaining -= item.Amount.RawValue;
                    }
                }
            }
        }

        class MyContext : Context<MyContext>
        {
            public static readonly StoppedState Stopped = new StoppedState();

            private readonly RequiredInventory _requiredInventory = new RequiredInventory(
                new Dictionary<string, double>
                {
                    { "SteelPlate", 2500 },
                    { "Motor", 100 },
                    { "SmallTube", 200 },
                    { "Construction", 200 },
                    { "InteriorPlate", 100 },
                    { "LargeTube", 10 },
                    { "Computer", 10 },
                }
            );

            public List<IMyPistonBase> _pistons = new List<IMyPistonBase>(16);
            public List<IMyShipMergeBlock> _mergeBlocks = new List<IMyShipMergeBlock>(16);
            public List<IMyShipConnector> _connectors = new List<IMyShipConnector>(16);
            public List<IMyLandingGear> _landingGears = new List<IMyLandingGear>(16);
            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(4);
            public List<IMyShipDrill> _drills = new List<IMyShipDrill>(64);
            public List<IMyShipWelder> _welders = new List<IMyShipWelder>(64);
            public List<IMyMotorBase> _offsetRotors = new List<IMyMotorBase>(2);
            public List<IMyCargoContainer> _cargo = new List<IMyCargoContainer>(32);
            private Dictionary<string, IReadOnlyCollection<IMyTerminalBlock>> _integrityRequiredBlocks;
            private StringBuilder _stringBuilder = new StringBuilder();
            public readonly float _minRotorDisplacement = -0.4f;
            public readonly float _maxRotorDisplacement = -0.1f;
            private string _error = null;
            public bool _integrityOk = true;
            public bool _stopStateRequested = false;
            public bool _hasRequiredInventory = true;
            private string _missingInventory = "";

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {
                _integrityRequiredBlocks = new Dictionary<string, IReadOnlyCollection<IMyTerminalBlock>>()
                {
                    { "pistons", _pistons },
                    { "merge block", _mergeBlocks },
                    { "landing gear", _landingGears },
                    { "drills", _drills },
                    { "welders", _welders },
                    { "rotor", _offsetRotors }
                };
            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _mergeBlocks.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_mergeBlocks, b => b.CustomName.Contains("TBM"));

                _connectors.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_connectors, b => b.CustomName.Contains("TBM"));

                _pistons.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_pistons, b => b.CustomName.Contains("TBM"));

                _landingGears.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_landingGears, b => b.CustomName.Contains("TBM"));

                _drills.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_drills);

                _welders.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_welders, b => b.CustomName.Contains("TBM"));

                _textPanels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains("TBM"));

                _offsetRotors.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_offsetRotors, b => b.CustomName.Contains("TBM_Offset"));

                if (isHeadConnected())
                {
                    _cargo.Clear();
                    Program.GridTerminalSystem.GetBlocksOfType(_cargo);
                }

                _integrityOk = checkIntegrity();
                updateRequiredInventory();

                return _pistons.Count > 0 
                    && _mergeBlocks.Count > 0 
                    && _connectors.Count > 0 
                    && _landingGears.Count > 0
                    && _welders.Count > 0
                    && _offsetRotors.Count > 0;
            }

            public bool isHeadConnected()
            {
                return isMergeBlockConnected() && isConnectorConnected();
            }

            public bool isMergeBlockConnected()
            {
                foreach (IMyShipMergeBlock merge in _mergeBlocks)
                {
                    if (merge.IsConnected)
                    {
                        return true;
                    }
                }
                return false;
            }

            public bool isConnectorConnected()
            {
                foreach (IMyShipConnector conn in _connectors)
                {
                    if (conn.Status == MyShipConnectorStatus.Connected)
                    {
                        return true;
                    }
                }
                return false;
            }

            public static void setBlocksEnabled<T>(List<T> blocks, bool enabled) where T : IMyFunctionalBlock
            {
                foreach (T b in blocks)
                {
                    b.Enabled = enabled;
                }
            }

            public static void extendPistons(List<IMyPistonBase> pistons, float headSpeed_mPerS)
            {
                float perPistonSpeed = headSpeed_mPerS / pistons.Count;
                foreach (IMyPistonBase p in pistons)
                {
                    p.Velocity = perPistonSpeed;
                    p.Enabled = true;
                }
            }

            public static double getPistonGroupExtensionProportion(List<IMyPistonBase> pistons)
            {
                double sum = 0;
                foreach (IMyPistonBase p in pistons)
                {
                    sum += p.CurrentPosition;
                }
                return sum / (pistons.Count * 10.0);
            }

            public static void setRotorDisplacement(List<IMyMotorBase> rotors, float displacement_m)
            {
                foreach (IMyMotorBase r in rotors)
                {
                    (r as IMyMotorStator).Displacement = displacement_m;
                }
            }

            private void updateRequiredInventory()
            {
                IReadOnlyCollection<string> missing = _requiredInventory.findMissing(_cargo);
                if (missing.Count == 0)
                {
                    _hasRequiredInventory = true;
                    _missingInventory = null;
                }
                else
                {
                    _hasRequiredInventory = false;
                    _missingInventory = "";
                    _missingInventory = string.Join("\n    ", missing);
                }
            }

            protected override void updateDisplayImpl()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("TBM\n");
                if (!FoundAllBlocks)
                {
                    _stringBuilder.Append("ERROR: Missing blocks\n");
                }

                _stringBuilder.Append("Status:\n");
                _stringBuilder.Append("    ");
                _stringBuilder.Append(State.ToString());
                _stringBuilder.Append("\n");

                _stringBuilder.Append("Cargo:\n");
                _stringBuilder.Append(string.Format("    {0:0.0} %\n", InventoryFilled.computeProportion(_cargo) * 100));

                if (_error != null)
                {
                    _stringBuilder.Append(_error);
                }

                if (!_hasRequiredInventory)
                {
                    _stringBuilder.Append("Missing inventory for welders:\n    ");
                    _stringBuilder.Append(_missingInventory);
                }

                string text = _stringBuilder.ToString();

                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }
            }

            public void logError(string msg)
            {
                log(msg);
                _error = msg;
                updateDisplay();
            }

            public void clearError()
            {
                if (_error != null)
                {
                    _error = null;
                    updateDisplay();
                }
            }

            private bool checkIntegrity()
            {
                string bad = "";
                bool functional = true;
                foreach (var kv in _integrityRequiredBlocks)
                {
                    if (!IntegrityUtils.isFunctional(kv.Value))
                    {
                        functional = false;
                        bad += "    " + kv.Key + "\n";
                    }

                    if (_cargo.Count > 0)
                    {
                        if (!IntegrityUtils.isConnectedToCargo(_cargo[0], kv.Value))
                        {
                            functional = false;
                            bad += "    " + kv.Key + " (conveyor)\n";
                        }
                    }
                }

                if (!functional)
                {
                    logError("ERROR: TBM integrity compromised:\n" + bad);
                }
                else
                {
                    clearError();
                }

                return functional;
            }
        }

        class BoringState : State<MyContext>
        {
            private TimeSpan _running = new TimeSpan();

            public override void update(MyContext context)
            {
                _running += context.Program.Runtime.TimeSinceLastRun;

                if (context._stopStateRequested)
                {
                    context.transition(MyContext.Stopped);
                    return;
                }

                if (!context.isHeadConnected())
                {
                    context.log("Cannot start: head not connected");
                    return;
                }

                bool inventoryFull = InventoryFilled.computeProportion(context._cargo) >= 0.95;
                bool ok = context.FoundAllBlocks
                    && context._integrityOk
                    //&& context._hasRequiredInventory
                    && !inventoryFull;

                if (!ok)
                {
                    MyContext.setBlocksEnabled(context._drills, false);
                    MyContext.setBlocksEnabled(context._welders, false);
                    MyContext.setBlocksEnabled(context._pistons, false);
                    return;
                }

                foreach (IMyLandingGear gear in context._landingGears)
                {
                    gear.Unlock();
                }

                MyContext.setBlocksEnabled(context._drills, true);
                bool enablePistons = _running.TotalSeconds > 2;
                if (enablePistons)
                {
                    MyContext.extendPistons(context._pistons, 0.10f);
                }
                else
                {
                    MyContext.setBlocksEnabled(context._pistons, false);
                }
                MyContext.setBlocksEnabled(context._welders, true);

                if (MyContext.getPistonGroupExtensionProportion(context._pistons) > 0.99999)
                {
                    context.transition(new SettleState());
                }
            }

            public override void leave(MyContext context)
            {
                MyContext.setBlocksEnabled(context._drills, false);
                MyContext.setBlocksEnabled(context._welders, false);
                MyContext.setBlocksEnabled(context._pistons, false);
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                _running = new TimeSpan();
            }

            public override string ToString()
            {
                return "Boring";
            }
        }

        class SettleState : State<MyContext>
        {
            private TimeSpan _running = new TimeSpan();

            public override void update(MyContext context)
            {
                _running += context.Program.Runtime.TimeSinceLastRun;

                if (_running.TotalSeconds >= 2)
                {
                    context.transition(new LockForMovementState());
                }
            }

            public override string ToString()
            {
                return "Settling";
            }
        }

        class LockForMovementState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                bool locked = false;
                foreach (IMyLandingGear gear in context._landingGears)
                {
                    gear.Lock();
                    locked |= gear.IsLocked;
                }

                if (!locked)
                {
                    context.log("Failed to lock landing gear");
                    return;
                }

                context.transition(new SetRotorDisplacementForFinalWeldState(context));
            }

            public override string ToString()
            {
                return "Locking for movement";
            }
        }

        class SetRotorDisplacementForFinalWeldState : MoveRotorDisplacementState
        {
            public SetRotorDisplacementForFinalWeldState(MyContext context)
                : base(context._offsetRotors, context._maxRotorDisplacement, 0.1f)
            {
                MoveRotorDisplacementState next = new MoveRotorDisplacementState(context._offsetRotors, context._minRotorDisplacement, 0.1f);
                next.setNextState(new UnlockHeadState());
                setNextState(next);
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                MyContext.setBlocksEnabled(context._welders, true);
            }

            public override void leave(MyContext context)
            {
                base.leave(context);
                MyContext.setBlocksEnabled(context._welders, false);
            }

            public override string ToString()
            {
                return "Final welding";
            }
        }

        class MoveRotorDisplacementState : State<MyContext>
        {
            private float _target;
            private float _speed_mPerS;
            private State<MyContext> _next;
            private List<IMyMotorBase> _rotors;

            public MoveRotorDisplacementState(
                List<IMyMotorBase> rotors,
                float target, 
                float speed_mPerS
            )
            {
                _target = target;
                _speed_mPerS = speed_mPerS;
                _rotors = rotors;
            }

            public void setNextState(State<MyContext> next)
            {
                _next = next;
            }

            public override void update(MyContext context)
            {
                int countDone = 0;
                foreach (IMyMotorBase motor in _rotors)
                {
                    IMyMotorStator stator = motor as IMyMotorStator;
                    
                    if  (Math.Abs(stator.Displacement - _target) < 1e-3f)
                    {
                        ++countDone;
                    }
                    else
                    {
                        float delta = _speed_mPerS * 1.0f / 60;
                        float sign = stator.Displacement < _target ? 1 : -1;
                        stator.Displacement += sign * delta;
                    }
                }

                if (countDone == _rotors.Count)
                {
                    transitionNext(context, _next);
                }
            }

            protected virtual void transitionNext(MyContext context, State<MyContext> next)
            {
                context.transition(next);
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }

            public override void leave(MyContext context)
            {
                base.enter(context);

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            public override string ToString()
            {
                return "Moving rotor displacement";
            }
        }

        class UnlockHeadState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                MyContext.setBlocksEnabled(context._mergeBlocks, false);
                foreach (IMyShipConnector b in context._connectors)
                {
                    b.Disconnect();
                }

                MoveRotorDisplacementState next = new MoveRotorDisplacementState(context._offsetRotors, context._maxRotorDisplacement, 0.1f);
                next.setNextState(new MoveHeadState());
                context.transition(next);
            }

            public override string ToString()
            {
                return "Unlocking head";
            }
        }

        class MoveHeadState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                MyContext.extendPistons(context._pistons, -2.5f);

                MyContext.setBlocksEnabled(context._mergeBlocks, true);

                if (MyContext.getPistonGroupExtensionProportion(context._pistons) < 0.00001)
                {
                    context.transition(new ReMergeHeadState(context));
                }
            }

            public override string ToString()
            {
                return "Moving head";
            }
        }

        class ReMergeHeadState : MoveRotorDisplacementState
        {
            public ReMergeHeadState(MyContext context)
                : base(context._offsetRotors, context._minRotorDisplacement, 0.1f)
            {
                setNextState(new BoringState());
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
            }

            protected override void transitionNext(MyContext context, State<MyContext> next)
            {
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;

                MyContext.setBlocksEnabled(context._mergeBlocks, true);

                if (context.isMergeBlockConnected())
                {
                    // Do the connection in transitionNext -> rotors will be in the
                    // right place before connection
                    foreach (IMyShipConnector b in context._connectors)
                    {
                        b.Connect();
                    }

                    if (context.isConnectorConnected())
                    {
                        base.transitionNext(context, next);
                    }
                }
            }

            public override string ToString()
            {
                return "Reconnecting";
            }
        }

        class StoppedState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                context.log("Stopped");
                context._stopStateRequested = false;

                MyContext.setBlocksEnabled(context._drills, false);
                MyContext.setBlocksEnabled(context._welders, false);
                MyContext.setBlocksEnabled(context._pistons, false);
            }

            public override string ToString()
            {
                return "Stopped";
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
            if (command == "start")
            {
                _impl.Context.log("Boring started");
                MoveRotorDisplacementState next = new MoveRotorDisplacementState(_impl.Context._offsetRotors, _impl.Context._minRotorDisplacement, 0.1f);
                next.setNextState(new BoringState());
                _impl.Context.transition(next);
            }
            else if (command == "stop")
            {
                _impl.Context.log("Boring stopped");
                _impl.Context._stopStateRequested = true;
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