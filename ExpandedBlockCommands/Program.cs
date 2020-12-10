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
            public static readonly IdleState Idle = new IdleState();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Idle)
            {
            }

            protected override bool updateBlocksImpl()
            {
                return true;
            }

            protected override void updateDisplayImpl()
            {
            }
        }

        class IdleState : State<MyContext>
        {
            public override void update(MyContext context)
            {
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.None;
            }
        }

        class MoveRotorToAngleState : State<MyContext>
        {
            private readonly List<IMyMotorBase> _rotors;
            private readonly float _angle_rads;
            private readonly float _speed_rpm;
            private readonly float _threshold_rads;
            private readonly float _targetVelocityOnFinish;

            public MoveRotorToAngleState(List<IMyMotorBase> rotors, float speed_rpm, float angle_rads, float threshold_rads, float targetVelocityOnFinish = 0)
            {
                _rotors = rotors;
                _speed_rpm = speed_rpm;
                _angle_rads = angle_rads;
                _threshold_rads = threshold_rads;
                _targetVelocityOnFinish = targetVelocityOnFinish;
            }

            public override void update(MyContext context)
            {
                bool anyStillMoving = false;
                foreach (IMyMotorBase rotor in _rotors)
                {
                    anyStillMoving |= control(rotor, context);
                }

                if (!anyStillMoving)
                {
                    context.transition(MyContext.Idle);
                }
            }

            private bool control(IMyMotorBase rotorBase, MyContext context)
            {
                IMyMotorStator rotor = rotorBase as IMyMotorStator;
                float distance = angleDistance_rads(rotor.Angle, _angle_rads);

                rotor.Enabled = true;
                if (Math.Abs(distance) < _threshold_rads)
                {
                    rotor.RotorLock = true;
                    rotor.TargetVelocityRPM = _targetVelocityOnFinish;
                    return false;
                } 
                else
                {
                    rotor.RotorLock = false;
                    rotor.TargetVelocityRPM = distance > 0 ? -_speed_rpm : _speed_rpm;
                    return true;
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
                foreach (IMyMotorBase rotorBase in _rotors)
                {
                    IMyMotorStator rotor = rotorBase as IMyMotorStator;
                    rotor.TargetVelocityRPM = _targetVelocityOnFinish;
                    rotor.RotorLock = true;
                }
            }

            //private static readonly float DIST_THRESHOLD_RADS = (float)(0.3 * Math.PI / 180);
        }


        private CommandParser _parser = new CommandParser();
        private StateMachineProgram<MyContext> _impl;

        public Program()
        {
            _impl = new StateMachineProgram<MyContext>(
                this,
                (cmd, args) => triggerFromStateMachineProg(cmd, args),
                v => Storage = v
            );

            _impl.init(new MyContext(_impl));
        }

        public void triggerFromStateMachineProg(string command, string[] args)
        {
            // shouldn't happen because we eat the trigger event in Main
            Echo("Error");
        }

        public void Save()
        {
            _impl.Save();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
            {
                try
                {
                    string commandStr = getCommand(argument);
                    List<CommandParser.Command> commands = _parser.parse(commandStr, this);
                    foreach (CommandParser.Command cmd in commands)
                    {
                        trigger(cmd.name, cmd.args);
                    }
                }
                catch (Exception e)
                {
                    Echo("ERROR: " + e.Message);
                }
            }
            else
            {
                _impl.Main(argument, updateSource);
            }
        }

        string getCommand(string argument)
        {
            int commandNumber;
            if (int.TryParse(argument, out commandNumber))
            {
                string customData = Me.CustomData;
                string[] commands = customData.Split('\n');
                for (int i = 0; i < commands.Length; ++i)
                {
                    KeyValuePair<int, string>? cmd = getNumberedCommand(commands[i]);
                    if (cmd == null)
                    {
                        continue;
                    }

                    if (cmd.Value.Key == commandNumber)
                    {
                        return cmd.Value.Value;
                    }
                }

                Echo("Error: Command not found " + commandNumber);
                return "";
            }
            else
            {
                return argument;
            }
        }

        KeyValuePair<int, string>? getNumberedCommand(string line)
        {
            int idx = line.IndexOf(':');
            if (idx < 0)
            {
                return null;
            }

            string numberStr = line.Substring(0, idx);
            int number;
            if (!int.TryParse(numberStr, out number))
            {
                return null;
            }

            string command = line.Substring(idx + 1).Trim();
            return new KeyValuePair<int, string>(number, command);
        }

        /*
        private void trigger(string argument)
        {
            string[] parts = argument.Split(' ');

            List<string> nonempty = removeEmpty(parts, 0);
            if (nonempty.Count == 0)
            {
                Echo("Missing command");
                return;
            }

            string command = nonempty[0];
            nonempty.RemoveAt(0);
            trigger(command, nonempty);
        }

        private List<string> removeEmpty(string[] args, int offset)
        {
            List<string> nonempty = new List<string>();
            for (int i = offset; i < args.Length; ++i)
            {
                var s = args[i];
                if (s.Length == 0)
                {
                    continue;
                }
                nonempty.Add(s);
            }
            return nonempty;
        }
        */

        private void trigger(string command, List<string> args)
        {
            command = command.ToLower();
            if (command == "set_piston_velocity")
            {
                triggerPistonVelocity(args);
            }
            else if (command == "set_enabled")
            {
                triggerSetEnabled(args);
            }
            else if (command == "standard_startup")
            {
                triggerStandardStartup(args);
            }
            else if (command == "standard_shutdown")
            {
                triggerStandardShutdown(args);
            }
            else if (command == "set_rotor_velocity")
            {
                triggerRotorVelocity(args);
            }
            else if (command == "set_rotor_lock")
            {
                triggerRotorLock(args);
            }
            else if (command == "move_rotor_to_angle")
            {
                triggerMoveRotorToAngle(args);
            }
            else
            {
                Echo("Unrecognised command '" + command + "'");
            }
        }

        private void triggerPistonVelocity(List<string> args)
        {
            if (args.Count < 2)
            {
                Echo("Expected 1 arg: name velocity");
            }

            List<IMyPistonBase> pistons = new List<IMyPistonBase>(8);

            GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistons, b => b.CustomName.Contains(args[0]));

            float velocity = float.Parse(args[1]);

            foreach (IMyPistonBase piston in pistons)
            {
                piston.Velocity = velocity;
            }
        }

        private void triggerRotorVelocity(List<string> args)
        {
            if (args.Count < 2)
            {
                Echo("Expected 2 args: name velocity");
            }

            List<IMyMotorBase> blocks = new List<IMyMotorBase>(8);

            GridTerminalSystem.GetBlocksOfType<IMyMotorBase>(blocks, b => b.CustomName.Contains(args[0]));

            float velocity = float.Parse(args[1]);

            foreach (IMyMotorBase b in blocks)
            {
                (b as IMyMotorStator).TargetVelocityRPM = velocity;
            }
        }

        private void triggerRotorLock(List<string> args)
        {
            if (args.Count < 2)
            {
                Echo("Expected 2 args: name locked");
            }

            List<IMyMotorBase> blocks = new List<IMyMotorBase>(8);

            GridTerminalSystem.GetBlocksOfType<IMyMotorBase>(blocks, b => b.CustomName.Contains(args[0]));

            bool enabled = bool.Parse(args[1]);

            foreach (IMyMotorBase b in blocks)
            {
                (b as IMyMotorStator).RotorLock = enabled;
            }
        }


        private void triggerSetEnabled(List<string> args)
        {
            if (args.Count < 2)
            {
                Echo("Expected 1 args: name enabled");
            }

            List<IMyFunctionalBlock> blocks = new List<IMyFunctionalBlock>(8);

            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CustomName.Contains(args[0]));

            bool enabled = bool.Parse(args[1]);

            foreach (IMyFunctionalBlock block in blocks)
            {
                block.Enabled = enabled;
            }
        }

        private void triggerStandardStartup(List<string> args)
        {
            forEachBlockOfType<IMyLandingGear>(gear => gear.AutoLock = false, isSameConstructAsMe);
            forEachBlockOfType<IMyLandingGear>(gear => gear.Unlock(), isSameConstructAsMe);
            forEachBlockOfType<IMyShipConnector>(connector => connector.Disconnect(), isSameConstructAsMe);

            forEachBlockOfType<IMyGyro>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyThrust>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyReactor>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyBatteryBlock>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyBatteryBlock>(battery => battery.ChargeMode = ChargeMode.Auto, isSameConstructAsMe);
            forEachBlockOfType<IMyLightingBlock>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyReflectorLight>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyBeacon>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyRadioAntenna>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyOreDetector>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyProjector>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyGasGenerator>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyMedicalRoom>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyGasTank>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyGasTank>(tank => tank.Stockpile = false, isSameConstructAsMe);
        }

        private void triggerStandardShutdown(List<string> args)
        {
            forEachBlockOfType<IMyGyro>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyThrust>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyReactor>(disable, isSameConstructAsMe);
            // Batteries in auto state on both startup & shutdown
            forEachBlockOfType<IMyBatteryBlock>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyBatteryBlock>(battery => battery.ChargeMode = ChargeMode.Auto, isSameConstructAsMe);
            forEachBlockOfType<IMyLightingBlock>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyReflectorLight>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyBeacon>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyRadioAntenna>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyOreDetector>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyProjector>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyGasGenerator>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyMedicalRoom>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyGasTank>(enable, isSameConstructAsMe);
            forEachBlockOfType<IMyGasTank>(tank => tank.Stockpile = false, isSameConstructAsMe);
            forEachBlockOfType<IMyShipDrill>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyShipWelder>(disable, isSameConstructAsMe);
            forEachBlockOfType<IMyShipGrinder>(disable, isSameConstructAsMe);

            forEachBlockOfType<IMyLandingGear>(gear => gear.Lock(), isSameConstructAsMe);
            forEachBlockOfType<IMyShipConnector>(connector => connector.Connect(), isSameConstructAsMe);
        }

        private void triggerMoveRotorToAngle(List<string> args)
        {
            if (args.Count < 2)
            {
                Echo("Expected 4 args: name, angle_deg, threshold_deg, speed_rpm");
            }

            List<IMyMotorBase> blocks = new List<IMyMotorBase>(8);

            GridTerminalSystem.GetBlocksOfType<IMyMotorBase>(blocks, b => b.CustomName.Contains(args[0]));

            float angle_deg = float.Parse(args[1]);
            float threshold_deg = float.Parse(args[2]);
            float speed_rpm = float.Parse(args[3]);
            float targetVelocityOnFinish = 0;
            if (args.Count >= 5)
            {
                targetVelocityOnFinish = float.Parse(args[4]);
            }

            float angle_rads = angle_deg * PI_F / 180.0f;
            float threshold_rads = Math.Abs(threshold_deg * PI_F / 180.0f);

            _impl.Context.transition(new MoveRotorToAngleState(blocks, speed_rpm, angle_rads, threshold_rads, targetVelocityOnFinish));
        }

        private List<IMyTerminalBlock> _tempBlocks = new List<IMyTerminalBlock>(16);
        private void forEachBlockOfType<T>(Action<T> consumer, Func<IMyTerminalBlock, bool> collect = null) where T : class
        {
            _tempBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType<T>(_tempBlocks, collect);

            foreach (IMyTerminalBlock block in _tempBlocks)
            {
                consumer.Invoke((T) block);
            }
        }

        private static void enable(IMyTerminalBlock block)
        {
            if (block is IMyFunctionalBlock)
            {
                (block as IMyFunctionalBlock).Enabled = true;
            }
        }

        private static void disable(IMyTerminalBlock block)
        {
            if (block is IMyFunctionalBlock)
            {
                (block as IMyFunctionalBlock).Enabled = false;
            }
        }

        private bool isSameConstructAsMe(IMyTerminalBlock block)
        {
            return block.IsSameConstructAs(Me);
        }

        private static readonly float TWOPI_F = (float)(2 * Math.PI);

        private static readonly float PI_F = (float)(Math.PI);

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
// setpistonvelocity test 0.123; setenabled test true
// setpistonvelocity test -0.5; setenabled test true
/*
setpistonvelocity Piston_Rig 0.123; setenabled Piston_Rig true
setpistonvelocity Piston_Rig -0.5; setenabled Piston_Rig true
*/
