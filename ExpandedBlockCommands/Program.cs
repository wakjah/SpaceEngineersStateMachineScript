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

        enum TriggerResult
        {
            Ok,
            Error,
            Async
        }

        class MyContext : Context<MyContext>
        {
            public static readonly IdleState Idle = new IdleState();
            private List<CommandParser.Command> _asyncCommands;
            private int _asyncCommandIndex = -1;

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

            public void completePendingAsyncCommands()
            {
                if (_asyncCommands != null && _asyncCommandIndex != -1 && _asyncCommandIndex < _asyncCommands.Count)
                {
                    trigger(_asyncCommands, _asyncCommandIndex);
                }
            }

            public void trigger(List<CommandParser.Command> commands, int fromIndex = 0)
            {
                for (int i = fromIndex; i < commands.Count; ++i)
                {
                    _asyncCommands = null;
                    _asyncCommandIndex = -1;

                    TriggerResult result = trigger(commands[i].name, commands[i].args);
                    if (result == TriggerResult.Error)
                    {
                        break;
                    }
                    else if (result == TriggerResult.Async)
                    {
                        _asyncCommands = commands;
                        _asyncCommandIndex = i + 1;
                        break;
                    }
                }
            }

            public TriggerResult trigger(string command, List<string> args)
            {
                try
                {
                    command = command.ToLower();
                    if (command == "set_piston_velocity")
                    {
                        return triggerPistonVelocity(args);
                    }
                    else if (command == "set_enabled")
                    {
                        return triggerSetEnabled(args);
                    }
                    else if (command == "standard_startup")
                    {
                        return triggerStandardStartup(args);
                    }
                    else if (command == "standard_shutdown")
                    {
                        return triggerStandardShutdown(args);
                    }
                    else if (command == "set_rotor_velocity")
                    {
                        return triggerRotorVelocity(args);
                    }
                    else if (command == "set_rotor_lock")
                    {
                        return triggerRotorLock(args);
                    }
                    else if (command == "move_rotor_to_angle")
                    {
                        return triggerMoveRotorToAngle(args);
                    }
                    else if (command == "move_piston_to_extension")
                    {
                        return triggerMovePistonToExtension(args);
                    }
                    else if (command == "sleep")
                    {
                        return triggerSleep(args);
                    }
                    else
                    {
                        Program.Echo("Unrecognised command '" + command + "'");
                        return TriggerResult.Error;
                    }
                }
                catch (Exception e)
                {
                    Program.Echo("Error: " + e.Message);
                    return TriggerResult.Error;
                }
            }

            private TriggerResult triggerPistonVelocity(List<string> args)
            {
                if (args.Count < 2)
                {
                    Program.Echo("Expected 1 arg: name velocity");
                    return TriggerResult.Error;
                }

                List<IMyPistonBase> pistons = new List<IMyPistonBase>(8);

                Program.GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(pistons, b => b.CustomName.Contains(args[0]) && isSameConstructAsMe(b));

                float velocity = float.Parse(args[1]);

                foreach (IMyPistonBase piston in pistons)
                {
                    piston.Velocity = velocity;
                }

                return TriggerResult.Ok;
            }

            private TriggerResult triggerRotorVelocity(List<string> args)
            {
                if (args.Count < 2)
                {
                    Program.Echo("Expected 2 args: name velocity");
                    return TriggerResult.Error;
                }

                List<IMyMotorBase> blocks = new List<IMyMotorBase>(8);

                Program.GridTerminalSystem.GetBlocksOfType<IMyMotorBase>(blocks, b => b.CustomName.Contains(args[0]) && isSameConstructAsMe(b));

                float velocity = float.Parse(args[1]);

                foreach (IMyMotorBase b in blocks)
                {
                    (b as IMyMotorStator).TargetVelocityRPM = velocity;
                }

                return TriggerResult.Ok;
            }

            private TriggerResult triggerRotorLock(List<string> args)
            {
                if (args.Count < 2)
                {
                    Program.Echo("Expected 2 args: name locked");
                    return TriggerResult.Error;
                }

                List<IMyMotorBase> blocks = new List<IMyMotorBase>(8);

                Program.GridTerminalSystem.GetBlocksOfType<IMyMotorBase>(blocks, b => b.CustomName.Contains(args[0]) && isSameConstructAsMe(b));

                bool enabled = bool.Parse(args[1]);

                foreach (IMyMotorBase b in blocks)
                {
                    (b as IMyMotorStator).RotorLock = enabled;
                }

                return TriggerResult.Ok;
            }


            private TriggerResult triggerSetEnabled(List<string> args)
            {
                if (args.Count < 2)
                {
                    Program.Echo("Expected 1 args: name enabled");
                    return TriggerResult.Error;
                }

                List<IMyFunctionalBlock> blocks = new List<IMyFunctionalBlock>(8);

                Program.GridTerminalSystem.GetBlocksOfType(blocks, b => b.CustomName.Contains(args[0]) && isSameConstructAsMe(b));

                bool enabled = bool.Parse(args[1]);

                foreach (IMyFunctionalBlock block in blocks)
                {
                    block.Enabled = enabled;
                }

                return TriggerResult.Ok;
            }

            private TriggerResult triggerStandardStartup(List<string> args)
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

                return TriggerResult.Ok;
            }

            private TriggerResult triggerStandardShutdown(List<string> args)
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

                return TriggerResult.Ok;
            }

            private TriggerResult triggerMoveRotorToAngle(List<string> args)
            {
                if (args.Count < 2)
                {
                    Program.Echo("Expected 4 args: name, angle_deg, threshold_deg, speed_rpm");
                    return TriggerResult.Error;
                }

                List<IMyMotorBase> blocks = new List<IMyMotorBase>(8);

                Program.GridTerminalSystem.GetBlocksOfType<IMyMotorBase>(blocks, b => b.CustomName.Contains(args[0]) && isSameConstructAsMe(b));

                float angle_deg = float.Parse(args[1]);
                float threshold_deg = float.Parse(args[2]);
                float speed_rpm = float.Parse(args[3]);

                float angle_rads = angle_deg * PI_F / 180.0f;
                float threshold_rads = Math.Abs(threshold_deg * PI_F / 180.0f);

                transition(new MoveRotorToAngleState(blocks, speed_rpm, angle_rads, threshold_rads));

                return TriggerResult.Async;
            }

            private TriggerResult triggerMovePistonToExtension(List<string> args)
            {
                if (args.Count < 2)
                {
                    Program.Echo("Expected 4 args: name, extension_m, threshold_m, speed_mpers");
                    return TriggerResult.Error;
                }

                List<IMyPistonBase> blocks = new List<IMyPistonBase>(8);

                Program.GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(blocks, b => b.CustomName.Contains(args[0]) && isSameConstructAsMe(b));

                float extension_m = float.Parse(args[1]);
                float threshold_m = float.Parse(args[2]);
                float speed_mpers = float.Parse(args[3]);

                transition(new MovePistonToExtensionState(blocks, speed_mpers, extension_m, threshold_m));

                return TriggerResult.Async;
            }

            private TriggerResult triggerSleep(List<string> args)
            {
                if (args.Count < 1)
                {
                    Program.Echo("Expected 1 args: time_s");
                    return TriggerResult.Error;
                }

                float time_s = float.Parse(args[0]);
                
                transition(new SleepState(time_s));

                return TriggerResult.Async;
            }

            private List<IMyTerminalBlock> _tempBlocks = new List<IMyTerminalBlock>(16);
            private void forEachBlockOfType<T>(Action<T> consumer, Func<IMyTerminalBlock, bool> collect = null) where T : class
            {
                _tempBlocks.Clear();
                Program.GridTerminalSystem.GetBlocksOfType<T>(_tempBlocks, collect);

                foreach (IMyTerminalBlock block in _tempBlocks)
                {
                    consumer.Invoke((T)block);
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
                return block.IsSameConstructAs(Program.Me);
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

                context.completePendingAsyncCommands();
            }
        }

        class MoveRotorToAngleState : State<MyContext>
        {
            private readonly List<IMyMotorBase> _rotors;
            private readonly float _angle_rads;
            private readonly float _speed_rpm;
            private readonly float _threshold_rads;

            public MoveRotorToAngleState(List<IMyMotorBase> rotors, float speed_rpm, float angle_rads, float threshold_rads)
            {
                _rotors = rotors;
                _speed_rpm = speed_rpm;
                _angle_rads = angle_rads;
                _threshold_rads = threshold_rads;
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
                    rotor.TargetVelocityRPM = 0;
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
                    rotor.TargetVelocityRPM = 0;
                    rotor.RotorLock = true;
                }
            }
        }

        class MovePistonToExtensionState : State<MyContext>
        {
            private readonly List<IMyPistonBase> _pistons;
            private readonly float _extension_m;
            private readonly float _speed_mpers;
            private readonly float _threshold_m;

            public MovePistonToExtensionState(List<IMyPistonBase> pistons, float speed_mpers, float extension_m, float threshold_m)
            {
                _pistons = pistons;
                _speed_mpers = speed_mpers;
                _extension_m = extension_m;
                _threshold_m = threshold_m;
            }

            public override void update(MyContext context)
            {
                bool anyStillMoving = false;
                foreach (IMyPistonBase piston in _pistons)
                {
                    anyStillMoving |= control(piston, context);
                }

                if (!anyStillMoving)
                {
                    context.transition(MyContext.Idle);
                }
            }

            private bool control(IMyPistonBase piston, MyContext context)
            {
                float distance = _extension_m - piston.CurrentPosition;

                if (Math.Abs(distance) < _threshold_m)
                {
                    piston.Enabled = false;
                    piston.Velocity = 0;
                    return false;
                }
                else
                {
                    piston.Enabled = true;
                    piston.Velocity = distance > 0 ? _speed_mpers : -_speed_mpers;
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
                foreach (IMyPistonBase piston in _pistons)
                {
                    piston.Enabled = false;
                    piston.Velocity = 0;
                }
            }
        }

        class SleepState : State<MyContext>
        {
            private float _seconds;
            private TimeSpan _elapsed = new TimeSpan();

            public SleepState(float seconds)
            {
                _seconds = seconds;
            }

            public override void update(MyContext context)
            {
                _elapsed += context.Program.Runtime.TimeSinceLastRun;

                setUpdateFrequency(context);

                if (_elapsed.TotalSeconds >= _seconds)
                {
                    context.transition(MyContext.Idle);
                }
            }

            public override void enter(MyContext context)
            {
                base.enter(context);
                setUpdateFrequency(context);
            }

            private void setUpdateFrequency(MyContext context)
            {
                double remaining = _seconds - _elapsed.TotalSeconds;
                if (remaining < 3)
                {
                    context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update10;
                }
                else if (remaining < 0.3)
                {
                    context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                }
                else
                {
                    context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
                }
            }
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
                    _impl.Context.trigger(commands);
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
