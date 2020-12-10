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
            public List<IMyMotorSuspension> _wheels = new List<IMyMotorSuspension>(4);

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            protected override bool updateBlocksImpl()
            {
                _wheels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_wheels);

                return true;
            }

            protected override void updateDisplayImpl()
            {
            }
        }

        class StoppedState : State<MyContext>
        {
            public override void update(MyContext context)
            {
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                context.log("Stopped");

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.None;
            }
        }

        class ChangeHeightState : State<MyContext>
        {
            private float _rate_cm_per_sec;

            public ChangeHeightState(float rate_cm_per_sec)
            {
                _rate_cm_per_sec = rate_cm_per_sec;
            }

            public override void update(MyContext context)
            {
                bool anyNotDone = false;
                foreach (IMyMotorSuspension wheel in context._wheels)
                {
                    float current = wheel.Height;

                    float limit = _rate_cm_per_sec > 0 ? wheel.GetMaximum<float>("Height") : wheel.GetMinimum<float>("Height");

                    if (Math.Abs(current - limit) > 0.01)
                    {
                        float next = current + _rate_cm_per_sec / 100.0f / 60.0f;
                        wheel.Height = next;

                        anyNotDone = true;
                    }
                }

                if (!anyNotDone)
                {
                    context.transition(MyContext.Stopped);
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

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.None;
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
            if (command == "lower")
            {
                _impl.Context.transition(new ChangeHeightState(8));
            }
            else if (command == "raise")
            {
                _impl.Context.transition(new ChangeHeightState(-30));
            }
            else if (command == "stop")
            {
                _impl.Context.transition(MyContext.Stopped);
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
