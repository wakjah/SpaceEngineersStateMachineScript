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

            private List<IMyBlockGroup> _groups = new List<IMyBlockGroup>();
            public List<IMyGravityGenerator> _pitchFwd = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _pitchAft = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _yawFwd = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _yawFwdInv = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _yawAft = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _yawAftInv = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _rollUpper = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _rollLower = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _fwdRev = new List<IMyGravityGenerator>();
            public List<IMyGravityGenerator> _fwdRevInv = new List<IMyGravityGenerator>();
            public List<IMyArtificialMassBlock> _artificialMass = new List<IMyArtificialMassBlock>();
            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            private void getGroupBlocksOfTypeContainingName<T>(List<T> gensOut, string groupNameContains) where T : class, IMyTerminalBlock
            {
                gensOut.Clear();

                _groups.Clear();
                Program.GridTerminalSystem.GetBlockGroups(_groups, g => g.Name.Contains(groupNameContains));

                foreach (IMyBlockGroup g in _groups)
                {
                    g.GetBlocksOfType(gensOut, b => b.IsSameConstructAs(Program.Me));
                }
            }

            protected override bool updateBlocksImpl()
            {
                getGroupBlocksOfTypeContainingName(_pitchFwd, "Pitch Fwd");
                getGroupBlocksOfTypeContainingName(_pitchAft, "Pitch Aft");
                getGroupBlocksOfTypeContainingName(_yawFwd, "Yaw Fwd");
                getGroupBlocksOfTypeContainingName(_yawFwdInv, "Yaw InvFwd");
                getGroupBlocksOfTypeContainingName(_yawAft, "Yaw Aft");
                getGroupBlocksOfTypeContainingName(_yawAftInv, "Yaw InvAft");
                getGroupBlocksOfTypeContainingName(_rollLower, "Roll Lower");
                getGroupBlocksOfTypeContainingName(_rollUpper, "Roll Upper");
                getGroupBlocksOfTypeContainingName(_fwdRev, "Fwd Rev");
                getGroupBlocksOfTypeContainingName(_fwdRevInv, "Fwd InvRev");

                getGroupBlocksOfTypeContainingName(_artificialMass, "Gravity Drive");

                return true;
            }

            public void resetAllGravityGenerators()
            {
                resetGravityGenerators(_pitchFwd);
                resetGravityGenerators(_pitchAft);
                resetGravityGenerators(_yawFwd);
                resetGravityGenerators(_yawFwdInv);
                resetGravityGenerators(_yawAft);
                resetGravityGenerators(_yawAftInv);
                resetGravityGenerators(_rollLower);
                resetGravityGenerators(_rollUpper);
                resetGravityGenerators(_fwdRev);
                resetGravityGenerators(_fwdRevInv);

                setBlocksEnabled(_artificialMass, false);
            }

            public static void resetGravityGenerators(List<IMyGravityGenerator> generators)
            {
                setBlocksEnabled(generators, false);
                setGravity(generators, 9.81f);
            }

            public static void setBlocksEnabled<T>(List<T> blocks, bool enabled) where T : IMyFunctionalBlock
            {
                foreach (IMyFunctionalBlock b in blocks)
                {
                    b.Enabled = enabled;
                }
            }

            public static void setGravity(List<IMyGravityGenerator> generators, float gravitationalAcceleration_mpers2)
            {
                foreach (IMyGravityGenerator g in generators)
                {
                    g.GravityAcceleration = gravitationalAcceleration_mpers2;
                }
            }

            public static void enableGravity(List<IMyGravityGenerator> generators, float gravitationalAcceleration_mpers2)
            {
                setGravity(generators, gravitationalAcceleration_mpers2);
                setBlocksEnabled(generators, true);
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

            public override void enter(MyContext context)
            {
                base.enter(context);

                context.resetAllGravityGenerators();
            }
        }

        class ControllingState : State<MyContext>
        {
            private static readonly float g = 9.81f;

            IMyShipController _shipController;

            public ControllingState(IMyShipController controller)
            {
                _shipController = controller;
            }

            public override void update(MyContext context)
            {
                if (!_shipController.IsUnderControl)
                {
                    context.transition(MyContext.Stopped);
                }

                // Q => roll < 0
                // E => roll > 0
                // Pitch up => rot.x < 0
                // Pitch down => rot.x > 0
                // Yaw left => rot.y < 0
                // Yaw right => rot.y > 0
                //
                // W => move.z < 0
                // S => move.z > 0
                // A => move.x < 0
                // D => move.x > 0
                // C => move.y < 0
                // SPACE => move.y > 0

                bool anyControl = false;

                float roll = _shipController.RollIndicator;
                Vector2 rot = _shipController.RotationIndicator;
                Vector3 move = _shipController.MoveIndicator;
                if (move.X == 0)
                {
                    // Do gyro yaw
                    if (rot.Y == 0)
                    {
                        MyContext.resetGravityGenerators(context._yawFwd);
                        MyContext.resetGravityGenerators(context._yawFwdInv);
                        MyContext.resetGravityGenerators(context._yawAft);
                        MyContext.resetGravityGenerators(context._yawAftInv);
                    }
                    else
                    {
                        int d = rot.Y > 0 ? 1 : -1;
                        MyContext.enableGravity(context._yawFwd, d * g);
                        MyContext.enableGravity(context._yawFwdInv, d * -g);
                        MyContext.enableGravity(context._yawAft, d * -g);
                        MyContext.enableGravity(context._yawAftInv, d * g);

                        anyControl = true;
                    }
                }
                else
                {
                    int d = move.X > 0 ? 1 : -1;
                    MyContext.enableGravity(context._yawFwd, d * g);
                    MyContext.enableGravity(context._yawFwdInv, d * -g);
                    MyContext.enableGravity(context._yawAft, d * g);
                    MyContext.enableGravity(context._yawAftInv, d * -g);

                    anyControl = true;
                }

                if (move.Y == 0)
                {
                    if (rot.X == 0)
                    {
                        MyContext.resetGravityGenerators(context._pitchFwd);
                        MyContext.resetGravityGenerators(context._pitchAft);
                    }
                    else
                    {
                        int d = rot.X > 0 ? 1 : -1;
                        MyContext.enableGravity(context._pitchFwd, d * g);
                        MyContext.enableGravity(context._pitchAft, d * -g);

                        anyControl = true;
                    }
                }
                else
                {
                    int d = move.Y > 0 ? 1 : -1;
                    MyContext.enableGravity(context._pitchFwd, -d * g);
                    MyContext.enableGravity(context._pitchAft, -d * g);

                    anyControl = true;
                }

                if (move.Z == 0)
                {
                    MyContext.resetGravityGenerators(context._fwdRev);
                    MyContext.resetGravityGenerators(context._fwdRevInv);
                }
                else 
                { 
                    int d = move.Z > 0 ? 1 : -1;
                    MyContext.enableGravity(context._fwdRev, d * g);
                    MyContext.enableGravity(context._fwdRevInv, -d * g);

                    anyControl = true;
                }

                MyContext.setBlocksEnabled(context._artificialMass, anyControl);
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
            if (command == "control")
            {
                _impl.Context.log("CONTROL");

                if (args.Length == 0)
                {
                    return;
                }

                string cockpitName = string.Join(" ", args);
                IMyTerminalBlock cockpit = GridTerminalSystem.GetBlockWithName(cockpitName);
                if (cockpit == null)
                {
                    Echo("Cockpit with name '" + cockpitName + "' not found");
                    return;
                }

                if (!(cockpit is IMyShipController))
                {
                    Echo("Block wiht name '" + cockpit + "' is not a cockpit or remote control");
                    return;
                }

                if (!(_impl.Context.State is StoppedState))
                {
                    Echo("Someone else is using the controller right now");
                    return;
                }

                _impl.Context.transition(new ControllingState(cockpit as IMyShipController));
            }
            else if (command == "stop")
            {
                _impl.Context.transition(new StoppedState());
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
