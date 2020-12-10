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

            private static readonly string BLOCK_TAG = "Ouroborous";

            public List<IMyProjector> _projectors = new List<IMyProjector>(1);
            public List<IMyShipWelder> _welders = new List<IMyShipWelder>(8);
            public List<IMyShipGrinder> _grinders = new List<IMyShipGrinder>(8);
            public List<IMyPistonBase> _pistons = new List<IMyPistonBase>(1);
            public List<IMyLandingGear> _landingGears = new List<IMyLandingGear>(1);
            public List<IMyShipMergeBlock> _mergeBlocks = new List<IMyShipMergeBlock>(2);
            public List<IMyCargoContainer> _cargo = new List<IMyCargoContainer>(16);
            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);
            private StringBuilder _stringBuilder = new StringBuilder();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _projectors.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_projectors, b => b.CustomName.Contains(BLOCK_TAG));

                _welders.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_welders, b => b.CustomName.Contains(BLOCK_TAG));

                _grinders.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_grinders, b => b.CustomName.Contains(BLOCK_TAG));

                _pistons.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_pistons, b => b.CustomName.Contains(BLOCK_TAG));

                _landingGears.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_landingGears, b => b.CustomName.Contains(BLOCK_TAG));

                _mergeBlocks.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_mergeBlocks, b => b.CustomName.Contains(BLOCK_TAG));

                _cargo.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_cargo);

                _textPanels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains(BLOCK_TAG));

                return _projectors.Count > 0
                    && _welders.Count > 0
                    && _grinders.Count > 0
                    && _pistons.Count > 0
                    && _landingGears.Count > 0
                    && _mergeBlocks.Count > 0;
            }

            protected override void updateDisplayImpl()
            {
                _stringBuilder.Clear();

                _stringBuilder.Append("Ouroborous\n\n");

                _stringBuilder.Append("Status:\n");
                _stringBuilder.Append(State.ToString());

                string text = _stringBuilder.ToString();
                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }
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

            public bool anyMergeBlockMerged()
            {
                foreach (IMyShipMergeBlock m in _mergeBlocks)
                {
                    if (m.IsConnected)
                    {
                        return true;
                    }
                }
                return false;
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
                context.log("Stopped");
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                MyContext.setBlocksEnabled(context._grinders, false);
                MyContext.setBlocksEnabled(context._welders, false);
                MyContext.setBlocksEnabled(context._projectors, false);
                MyContext.setBlocksEnabled(context._pistons, false);
            }

            public override string ToString()
            {
                return "Stopped";
            }
        }

        class TurnOnProjectorState : State<MyContext>
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

                MyContext.setBlocksEnabled(context._projectors, true);

                if (_timeRunning.TotalSeconds > 5 && context.allProjectorsProjecting())
                {
                    context.transition(new WeldNewRowState());
                }
            }

            public override string ToString()
            {
                return "Turning on projector";
            }
        }

        class WeldNewRowState : State<MyContext>
        {
            private List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>(64);
            private List<IMyShipMergeBlock> _mergeBlocks = new List<IMyShipMergeBlock>(16);

            public override void update(MyContext context)
            {
                MyContext.setBlocksEnabled(context._projectors, true);
                MyContext.setBlocksEnabled(context._welders, true);

                _batteries.Clear();
                context.Program.GridTerminalSystem.GetBlocksOfType(_batteries);

                _mergeBlocks.Clear();
                context.Program.GridTerminalSystem.GetBlocksOfType(_mergeBlocks);
                
                if (context.allProjectorsDone()
                    && MyContext.allBlocksFunctional(_batteries) 
                    && MyContext.allBlocksFunctional(_mergeBlocks)
                )
                {
                    context.updateBlocks();

                    MyContext.setBlocksEnabled(context._projectors, false);
                    MyContext.setBlocksEnabled(context._welders, false);

                    context.transition(new ExtendPistonState());
                }
            }

            public override string ToString()
            {
                return "Welding new row";
            }
        }

        private class ExtendPistonState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                MyContext.extendPistons(context._pistons, 2.0f);
                if (MyContext.getPistonGroupExtensionProportion(context._pistons) > 0.999999)
                {
                    context.transition(new SwitchLockForMovementState());
                }
            }

            public override string ToString()
            {
                return "Extending piston";
            }
        }

        private class SwitchLockForMovementState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                int countLocked = 0;
                foreach (IMyLandingGear gear in context._landingGears)
                {
                    gear.Lock();
                    if (gear.IsLocked)
                    {
                        ++countLocked;
                    }
                }
                if (countLocked > 0 && countLocked == context._landingGears.Count)
                {
                    MyContext.setBlocksEnabled(context._mergeBlocks, false);
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
                MyContext.setBlocksEnabled(context._grinders, true);
                MyContext.extendPistons(context._pistons, -0.5f);

                
                double maxExtension = context._pistons.Count * 10;
                double distFromMax = maxExtension - MyContext.getPistonGroupExtension(context._pistons);
                if (distFromMax > 2.0)
                {
                    MyContext.setBlocksEnabled(context._grinders, false);
                }

                if (distFromMax > 2.25)
                {
                    MyContext.setBlocksEnabled(context._mergeBlocks, true);

                    if (context.anyMergeBlockMerged())
                    {
                        MyContext.setBlocksEnabled(context._pistons, false);
                        context.transition(new LockState());
                    }
                }

                if (distFromMax > 2.499)
                {
                    MyContext.setBlocksEnabled(context._pistons, false);
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
                    foreach (IMyLandingGear gear in context._landingGears)
                    {
                        gear.Unlock();
                    }

                    context.transition(new StoppedState());
                }
            }

            public override string ToString()
            {
                return "Locking";
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
                return;
            }

            _impl.Context.transition(new TurnOnProjectorState());
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
