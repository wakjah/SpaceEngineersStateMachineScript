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

            private static readonly string BLOCK_TAG = "Inkjet";

            public List<IMyPistonBase> _pistonsAdvance = new List<IMyPistonBase>(1);
            public List<IMyLandingGear> _landingGearsAdvance = new List<IMyLandingGear>(1);
            public List<IMyShipMergeBlock> _mergeBlocksAdvance = new List<IMyShipMergeBlock>(2);
            public List<IMyProjector> _projectors = new List<IMyProjector>(1);
            private StringBuilder _stringBuilder = new StringBuilder();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _pistonsAdvance.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_pistonsAdvance, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("Advance"));

                _landingGearsAdvance.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_landingGearsAdvance, b => b.CustomName.Contains(BLOCK_TAG));

                _mergeBlocksAdvance.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_mergeBlocksAdvance, b => b.CustomName.Contains(BLOCK_TAG) && b.CustomName.Contains("Advance"));

                _projectors.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_projectors, b => b.CustomName.Contains(BLOCK_TAG));

                return _pistonsAdvance.Count > 0
                    && _landingGearsAdvance.Count > 0
                    && _mergeBlocksAdvance.Count > 0
                    && _projectors.Count > 0;
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

            public bool anyMergeBlockMerged()
            {
                foreach (IMyShipMergeBlock m in _mergeBlocksAdvance)
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
                //context.log("Stopped");
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                MyContext.setBlocksEnabled(context._pistonsAdvance, false);
            }

            public override string ToString()
            {
                return "Stopped";
            }
        }

        private class AdvancePistonState : State<MyContext>
        {
            private double _targetPistonGroupExtension;

            public override void update(MyContext context)
            {
                context.Program.Echo("current extension: " + MyContext.getPistonGroupExtension(context._pistonsAdvance) + "\ntarget: " + _targetPistonGroupExtension);

                if (_targetPistonGroupExtension < -2.25)
                {
                    context.transition(new SwitchLockForMovementState());
                    return;
                }

                // Safety check just in case we are in this state without being merged, we don't want to unlock the only 
                // thing keeping it in place!
                if (context.anyMergeBlockMerged())
                {
                    foreach (IMyLandingGear gear in context._landingGearsAdvance)
                    {
                        gear.Unlock();
                    }
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

            if (command == "advance")
            {
                _impl.Context.transition(new AdvancePistonState());
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
