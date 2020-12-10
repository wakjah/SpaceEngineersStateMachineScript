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

            public List<IMyMotorBase> _rotors = new List<IMyMotorBase>(2);

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _rotors.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_rotors, b => b.CustomName.Contains("Door"));

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
                context.log("Stopped");
            }
        }

        class MoveToZeroPositionState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                bool anyStillMoving = false;
                foreach (IMyMotorBase rotor in context._rotors)
                {
                    anyStillMoving |= control(context, rotor);
                }

                if (!anyStillMoving)
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
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            private static readonly float DIST_THRESHOLD_RADS = (float)(0.3 * Math.PI / 180);

            private bool control(MyContext context, IMyMotorBase rotorBase)
            {
                IMyMotorStator rotor = rotorBase as IMyMotorStator;
                float distance = angleDistance_rads(rotor.Angle, 0);

                rotor.Enabled = true;
                if (Math.Abs(distance) < DIST_THRESHOLD_RADS)
                {
                    rotor.RotorLock = true;
                    rotor.TargetVelocityRPM = 0;
                    return false;
                }
                else
                {
                    rotor.RotorLock = false;
                    rotor.TargetVelocityRPM = distance > 0 ? -0.5f : 0.5f;
                    return true;
                }
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
            _impl.Context.updateBlocks();
            _impl.Context.transition(new MoveToZeroPositionState());
        }

        public void Save()
        {
            _impl.Save();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _impl.Main(argument, updateSource);
        }


        private static readonly float TWOPI_F = (float)(2 * Math.PI);

        private static readonly float PI_F = (float)(Math.PI);

        private static float normaliseAngle_0to2pi(float angle_rads)
        {
            while (angle_rads > TWOPI_F)
            {
                angle_rads -= TWOPI_F;
            }


            while (angle_rads < 0)
            {
                angle_rads += TWOPI_F;
            }

            return angle_rads;
        }

        private static float normaliseAngle_minusPiToPi(float angle_rads)
        {
            while (angle_rads > PI_F)
            {
                angle_rads -= TWOPI_F;
            }


            while (angle_rads < -PI_F)
            {
                angle_rads += TWOPI_F;
            }

            return angle_rads;
        }

        private static float angleDistance_rads(float a, float b)
        {
            return normaliseAngle_minusPiToPi(normaliseAngle_minusPiToPi(a) - normaliseAngle_minusPiToPi(b));
        }
    }
}
