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
            public static readonly StoppedState Stopped = new StoppedState();

            private static readonly string BLOCK_TAG = "DryDock";

            public List<IMyPistonBase> _pistonsAdvanceOrthogonal = new List<IMyPistonBase>(1);
            public List<IMyPistonBase> _pistonsAdvanceParallel = new List<IMyPistonBase>(1);
            public List<IMyLandingGear> _landingGearsAdvance = new List<IMyLandingGear>(1);
            public List<IMyPistonBase> _pistonsHaltOrthogonal = new List<IMyPistonBase>(1);
            public List<IMyLandingGear> _landingGearsHalt = new List<IMyLandingGear>(1);
            public List<IMyProjector> _projectors = new List<IMyProjector>(1);
            private StringBuilder _stringBuilder = new StringBuilder();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _pistonsAdvanceOrthogonal.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_pistonsAdvanceOrthogonal, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("AdvanceOrthogonal"));

                _pistonsAdvanceParallel.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_pistonsAdvanceParallel, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("AdvanceParallel"));

                _landingGearsAdvance.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_landingGearsAdvance, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("Advance"));

                _pistonsHaltOrthogonal.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_pistonsHaltOrthogonal, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("Halt"));

                _landingGearsHalt.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_landingGearsHalt, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("Halt"));

                _projectors.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_projectors, b => b.CustomName.Contains(BLOCK_TAG));

                return _pistonsAdvanceOrthogonal.Count > 0
                    && _pistonsAdvanceParallel.Count > 0
                    && _landingGearsAdvance.Count > 0
                    && _pistonsHaltOrthogonal.Count > 0
                    && _landingGearsHalt.Count > 0;
                    //&& _projectors.Count > 0;
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

            public static double getPistonGroupExtension(List<IMyPistonBase> pistons)
            {
                double sum = 0;
                foreach (IMyPistonBase p in pistons)
                {
                    sum += p.CurrentPosition;
                }
                return sum;
            }

            public static double getPistonGroupExtensionProportion(List<IMyPistonBase> pistons)
            {
                if (pistons.Count == 0)
                {
                    return 0;
                }
                return getPistonGroupExtension(pistons) / (pistons.Count * 10.0);
            }

            public bool allProjectorsDone()
            {
                foreach (IMyProjector j in _projectors)
                {
                    if (j.RemainingBlocks > 0
                        || j.RemainingArmorBlocks > 0
                    )
                    {
                        return false;
                    }
                }
                return true;
            }

            public bool allProjectorsProjecting()
            {
                foreach (IMyProjector j in _projectors)
                {
                    if (j.TotalBlocks == 0 || !j.IsProjecting)
                    {
                        return false;
                    }
                }
                return true;
            }

           /* public bool anyMergeBlockMerged()
            {
                foreach (IMyShipMergeBlock m in _mergeBlocksAdvance)
                {
                    if (m.IsConnected)
                    {
                        return true;
                    }
                }
                return false;
            }*/


            public static bool anyLandingGearLocked(List<IMyLandingGear> gears)
            {
                foreach (IMyLandingGear g in gears)
                {
                    if (g.IsLocked)
                    {
                        return true;
                    }
                }
                return false;
            }

            public bool anyLandingGearLocked()
            {
                return anyLandingGearLocked(_landingGearsAdvance) || anyLandingGearLocked(_landingGearsHalt);
            }

            public static void setLandingGearAutoLock(List<IMyLandingGear> gears, bool autoLock)
            {
                foreach (IMyLandingGear gear in gears)
                {
                    gear.AutoLock = autoLock;
                }
            }

            public static void setLandingGearLock(List<IMyLandingGear> gears, bool locked)
            {
                foreach (IMyLandingGear gear in gears)
                {
                    if (locked)
                    {
                        gear.Lock();
                    }
                    else
                    {
                        gear.Unlock();
                    }
                }
            }

            public static bool allBlocksFunctional<T>(List<T> blocks) where T : IMyTerminalBlock
            {
                foreach (IMyTerminalBlock block in blocks)
                {
                    if (!block.IsFunctional)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        class StoppedState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                //context.log("Stopped");
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                MyContext.setBlocksEnabled(context._pistonsAdvanceParallel, false);
                MyContext.setBlocksEnabled(context._pistonsAdvanceOrthogonal, false);
                MyContext.setBlocksEnabled(context._pistonsHaltOrthogonal, false);
            }

            public override string ToString()
            {
                return "Stopped";
            }
        }

        private class ExtendAndLockLandingGearState : State<MyContext>
        {
            private List<IMyPistonBase> _pistons;
            private List<IMyLandingGear> _landingGears;
            private State<MyContext> _nextState;

            public ExtendAndLockLandingGearState(List<IMyPistonBase> pistons, List<IMyLandingGear> landingGears, State<MyContext> nextState)
            {
                _pistons = pistons;
                _landingGears = landingGears;
                _nextState = nextState;
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }

            public override void leave(MyContext context)
            {
                base.leave(context);
                MyContext.setLandingGearAutoLock(_landingGears, false);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            public override void update(MyContext context)
            {
                MyContext.setLandingGearAutoLock(_landingGears, true);
                MyContext.extendPistons(_pistons, 0.5f);
                if (MyContext.anyLandingGearLocked(_landingGears))
                {
                    MyContext.setBlocksEnabled(_pistons, false);
                    context.transition(_nextState);
                }
            }
        }

        private class UnlockAndRetractLandingGearState : State<MyContext>
        {
            private List<IMyPistonBase> _pistons;
            private List<IMyLandingGear> _landingGears;
            private State<MyContext> _nextState;

            public UnlockAndRetractLandingGearState(List<IMyPistonBase> pistons, List<IMyLandingGear> landingGears, State<MyContext> nextState)
            {
                _pistons = pistons;
                _landingGears = landingGears;
                _nextState = nextState;
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }

            public override void leave(MyContext context)
            {
                base.leave(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            public override void update(MyContext context)
            {
                MyContext.setLandingGearAutoLock(_landingGears, false);
                MyContext.setLandingGearLock(_landingGears, false);
                MyContext.extendPistons(_pistons, -0.5f);
                if (Math.Abs(MyContext.getPistonGroupExtension(_pistons)) < 0.001)
                {
                    MyContext.setBlocksEnabled(_pistons, false);
                    context.transition(_nextState);
                }
            }
        }

        private class MoveToStartPositionState : State<MyContext>
        {
            private State<MyContext> _nextState;

            public MoveToStartPositionState(State<MyContext> nextState)
            {
                _nextState = nextState;
            }

            public override void update(MyContext context)
            {
                if (MyContext.getPistonGroupExtensionProportion(context._pistonsAdvanceParallel) >= 0.999f)
                {
                    // Lock advance gears, Unlock halt gears, move to next state
                    context.transition(new ExtendAndLockLandingGearState(
                        context._pistonsAdvanceOrthogonal,
                        context._landingGearsAdvance,
                        new UnlockAndRetractLandingGearState(
                            context._pistonsHaltOrthogonal,
                            context._landingGearsHalt,
                            _nextState
                        )
                    ));
                }

                // Make sure the "halt" landing gears are locked
                if (!MyContext.anyLandingGearLocked(context._landingGearsHalt))
                {
                    context.transition(new ExtendAndLockLandingGearState(context._pistonsHaltOrthogonal, context._landingGearsHalt, this));
                    return;
                }

                // Make sure the "advance" landing gears are not locked or we will try to push the print backwards
                if (MyContext.anyLandingGearLocked(context._landingGearsAdvance))
                {
                    context.transition(new UnlockAndRetractLandingGearState(context._pistonsAdvanceOrthogonal, context._landingGearsAdvance, this));
                    return;
                }

                MyContext.extendPistons(context._pistonsAdvanceParallel, 2.0f);
            }
        }

        private class AdvancePistonState : State<MyContext>
        {
            private double _targetPistonGroupExtension;

            public override void enter(MyContext context)
            {
                base.enter(context);
                _targetPistonGroupExtension = MyContext.getPistonGroupExtension(context._pistonsAdvanceParallel) - 2.5;
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;

            }

            public override void leave(MyContext context)
            {
                base.leave(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            public override void update(MyContext context)
            {
                // If we are locked on the halt gear, whatever the piston position is we just move to start position
                // which will end with the advance piston fully extended and the halt gear retracted. Then come back 
                // to this state
                if (MyContext.anyLandingGearLocked(context._landingGearsHalt))
                {
                    context.transition(new MoveToStartPositionState(new AdvancePistonState()));
                    return;
                }

                // Otherwise, if the piston has finished its run, move to start position before advancing
                if (MyContext.getPistonGroupExtensionProportion(context._pistonsAdvanceParallel) < 0.0001)
                {
                    // If we have reached our target, stop afterwards
                    // Otherwise, do an advance afterwards
                    State<MyContext> next;
                    if (_targetPistonGroupExtension >= -1.25 && _targetPistonGroupExtension <= 1.25)
                    {
                        next = MyContext.Stopped;
                    }
                    else
                    {
                        next = new AdvancePistonState();
                    }
                    
                    context.transition(new MoveToStartPositionState(next));
                    return;
                }

                // We are good to move
                MyContext.extendPistons(context._pistonsAdvanceParallel, -1.0f);
                if (MyContext.getPistonGroupExtension(context._pistonsAdvanceParallel) <= _targetPistonGroupExtension)
                {
                    MyContext.setBlocksEnabled(context._pistonsAdvanceParallel, false);
                    context.transition(MyContext.Stopped);
                }
            }
        }

        /*
        private class AdvancePistonState : State<MyContext>
        {
            private double _targetPistonGroupExtension;

            public override void update(MyContext context)
            {
                context.Program.Echo("current extension: " + MyContext.getPistonGroupExtension(context._pistonsAdvanceParallel) + "\ntarget: " + _targetPistonGroupExtension);

                if (_targetPistonGroupExtension < -2.25)
                {
                    context.transition(new SwitchLockForMovementState());
                    return;
                }

                // Safety check just in case we are in this state without being merged, we don't want to unlock the only 
                // thing keeping it in place!
                if (!context.anyLandingGearLocked(context._landingGearsAdvance))
                {
                    context.log("ERROR: No landing gear locked!");
                    context.transition(MyContext.Stopped);
                }

                MyContext.extendPistons(context._pistonsAdvance, -1);
                if (MyContext.getPistonGroupExtension(context._pistonsAdvance) <= _targetPistonGroupExtension)
                {
                    MyContext.extendPistons(context._pistonsAdvance, 0);
                    context.transition(MyContext.Stopped);
                }
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                _targetPistonGroupExtension = MyContext.getPistonGroupExtension(context._pistonsAdvance) - 2.5;
            }

            public override void leave(MyContext context)
            {
                base.leave(context);

                foreach (IMyLandingGear gear in context._landingGearsAdvance)
                {
                    gear.Lock();
                }

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }
        }


        private class SwitchLockForMovementState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                context.Program.Echo("SwitchLockForMovement");

                int countLocked = 0;
                foreach (IMyLandingGear gear in context._landingGearsAdvance)
                {
                    gear.Lock();
                    if (gear.IsLocked)
                    {
                        ++countLocked;
                    }
                }
                if (countLocked > 0 && countLocked == context._landingGearsAdvance.Count)
                {
                    MyContext.setBlocksEnabled(context._mergeBlocksAdvance, false);
                    context.transition(new MoveToSecondaryLockPositionState());
                }
            }

            public override string ToString()
            {
                return "Switching lock for movement";
            }
        }

        private class MoveToSecondaryLockPositionState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                context.Program.Echo("MoveToSecondary!");
                MyContext.extendPistons(context._pistonsAdvance, 2f);

                double maxExtension = context._pistonsAdvance.Count * 10;
                double distFromMax = maxExtension - MyContext.getPistonGroupExtension(context._pistonsAdvance);

                if (Math.Abs(distFromMax) < 0.05)
                {
                    MyContext.setBlocksEnabled(context._mergeBlocksAdvance, true);

                    if (context.anyMergeBlockMerged())
                    {
                        MyContext.setBlocksEnabled(context._pistonsAdvance, false);
                        context.transition(new LockState());
                    }
                }
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }

            public override void leave(MyContext context)
            {
                base.leave(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            public override string ToString()
            {
                return "Moving";
            }
        }

        class LockState : State<MyContext>
        {
            bool _timeInitialised = false;
            TimeSpan _timeRunning = new TimeSpan();

            public override void update(MyContext context)
            {
                if (_timeInitialised)
                {
                    _timeRunning += context.Program.Runtime.TimeSinceLastRun;
                }
                _timeInitialised = true;

                if (_timeRunning.TotalSeconds > 5 && context.anyMergeBlockMerged())
                {
                    context.transition(new AdvancePistonState());
                }
            }

            public override string ToString()
            {
                return "Locking";
            }
        }*/


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

            if (command == "advance")
            {
                _impl.Context.transition(new AdvancePistonState());
            }
            else if (command == "lock")
            {
                _impl.Context.transition(new ExtendAndLockLandingGearState(
                    _impl.Context._pistonsHaltOrthogonal, 
                    _impl.Context._landingGearsHalt, 
                    MyContext.Stopped
                ));
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
