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
            public static readonly string BLOCK_TAG_AUTOHANGAR = "AutoHangar";

            public class DoorDesiredState
            {
                public bool open;
                public List<IMySensorBlock> openForSensors = new List<IMySensorBlock>(1);
                public bool mark;

                public bool IsDesiredOpen { get { return open || openForSensors.Count > 0;  } }
                public void removeInactiveSensors()
                {
                    openForSensors.RemoveAll(b => !b.IsActive);
                }
            }

            public List<IMyBlockGroup> _hangarDoorGroups = new List<IMyBlockGroup>(8);
            public List<IMyAirVent> _airVents = new List<IMyAirVent>(8);
            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);
            public Dictionary<string, DoorDesiredState> _desiredStates = new Dictionary<string, DoorDesiredState>();
            private List<string> _tempStrings = new List<string>(16);
            private List<IMyDoor> _tempDoors = new List<IMyDoor>(16);
            private StringBuilder _stringBuilder = new StringBuilder();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), new ClosedState())
            {
                initialize();
            }

            protected override bool updateBlocksImpl()
            {
                _hangarDoorGroups.Clear();
                Program.GridTerminalSystem.GetBlockGroups(_hangarDoorGroups, b => b.Name.Contains(BLOCK_TAG_AUTOHANGAR));

                _airVents.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_airVents, b => b.CustomName.Contains(BLOCK_TAG_AUTOHANGAR));

                _textPanels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains(BLOCK_TAG_AUTOHANGAR));

                foreach (DoorDesiredState desired in _desiredStates.Values)
                {
                    desired.mark = false;
                }

                foreach (IMyBlockGroup group in _hangarDoorGroups)
                {
                    DoorDesiredState desired;
                    if (_desiredStates.ContainsKey(group.Name))
                    {
                        desired = _desiredStates[group.Name];
                    }
                    else
                    {
                        desired = new DoorDesiredState();
                        _desiredStates[group.Name] = desired;
                    }
                    desired.mark = true;
                }

                _tempStrings.Clear();
                foreach (KeyValuePair<string, DoorDesiredState> desired in _desiredStates)
                {
                    if (!desired.Value.mark)
                    {
                        _tempStrings.Add(desired.Key);
                    }
                }

                foreach (string s in _tempStrings)
                {
                    _desiredStates.Remove(s);
                }

                return true;
            }

            protected override void updateDisplayImpl()
            {
                if (_textPanels.Count == 0)
                {
                    return;
                }

                _stringBuilder.Clear();

                _stringBuilder.Append("State: " + State.ToString() + "\n");
                _stringBuilder.Append("All doors closed: " + allDoorsClosed() + "\n");
                _stringBuilder.Append("Any desired open: " + anyDesiredOpen() + "\n");

                //foreach (IMyAirVent vent in _airVents)
                //{
                //    _stringBuilder.Append("vent " + vent.CustomName + " \nstatus " + vent.Status + "\n" + "pressurized " + vent.IsPressurized());
                //}

                string text = _stringBuilder.ToString();

                foreach (IMyTextPanel textPanel in _textPanels)
                {
                    textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
                    textPanel.WriteText(text);
                }
            }

            public void setDoorGroupOpenState(IMyBlockGroup group, bool state)
            {
                _tempDoors.Clear();
                group.GetBlocksOfType(_tempDoors);

                foreach (IMyDoor door in _tempDoors)
                {
                    if ((door.Status != DoorStatus.Closed) && !state)
                    {
                        door.CloseDoor();
                    }

                    if ((door.Status != DoorStatus.Open) && state)
                    {
                        door.OpenDoor();
                    }
                }
            }

            public bool allDoorsClosed()
            {
                foreach (IMyBlockGroup group in _hangarDoorGroups)
                {
                    if (!allDoorsClosed(group))
                    {
                        return false;
                    }
                }
                return true;
            }

            public bool allDoorsClosed(IMyBlockGroup group)
            {
                _tempDoors.Clear();
                group.GetBlocksOfType(_tempDoors);
                foreach (IMyDoor door in _tempDoors)
                {
                    if (door.Status != DoorStatus.Closed)
                    {
                        return false;
                    }
                }
                return true;
            }

            private void initialize()
            {
                if (allDoorsClosed())
                {
                    transition(new PressurizingState());
                    Program.Echo("init pressurizing");
                }
                else
                {
                    transition(new OpenableState());

                    foreach (IMyBlockGroup group in _hangarDoorGroups)
                    {
                        bool anyOpen = !allDoorsClosed(group);
                        // Ensure consistent within group
                        setDoorGroupOpenState(group, anyOpen);
                        _desiredStates[group.Name].open = anyOpen;
                    }

                    Program.Echo("init openable");
                }
            }

            public bool anyDesiredOpen()
            {
                foreach (DoorDesiredState state in _desiredStates.Values)
                {
                    if (state.IsDesiredOpen)
                    {
                        return true;
                    }
                }
                return false;
            }

            public void open(string groupName)
            {
                setGroupDesiredState(groupName, true);
            }

            public void close(string groupName)
            {
                setGroupDesiredState(groupName, false);
            }

            public void toggle(string groupName)
            {
                if (!_desiredStates.ContainsKey(groupName))
                {
                    return;
                }
                DoorDesiredState desired = _desiredStates[groupName];
                setGroupDesiredState(groupName, !desired.open);
            }

            public void setDesiredOpenAll(bool open)
            {
                foreach (IMyBlockGroup group in _hangarDoorGroups)
                {
                    setGroupDesiredState(group.Name, open);
                }
            }

            public void setGroupDesiredState(string groupName, bool open)
            {
                if (!_desiredStates.ContainsKey(groupName))
                {
                    return;
                }
                DoorDesiredState desired = _desiredStates[groupName];

                bool old = desired.open;
                if (old == open)
                {
                    return;
                }

                desired.open = open;
                if (open)
                {
                    (State as AutoDoorState).onOpenRequested(this);
                }
                else
                {
                    (State as AutoDoorState).onCloseRequested(this);
                }
            }

            public void openForSensor(IMySensorBlock sensor, string groupName)
            {
                if (!_desiredStates.ContainsKey(groupName))
                {
                    return;
                }
                DoorDesiredState desired = _desiredStates[groupName];
                bool old = desired.IsDesiredOpen;
                desired.openForSensors.Add(sensor);
                if (!old)
                {
                    (State as AutoDoorState).onOpenRequested(this);
                }
            }

            public void updateSensors()
            {
                foreach (DoorDesiredState desired in _desiredStates.Values)
                {
                    if (desired.openForSensors.Count == 0)
                    {
                        continue;
                    }

                    updateSensors(desired);
                }
            }

            private void updateSensors(DoorDesiredState desired)
            {
                bool old = desired.IsDesiredOpen;
                desired.removeInactiveSensors();
                if (old && !desired.IsDesiredOpen)
                {
                    (State as AutoDoorState).onCloseRequested(this);
                }
            }
        }

        abstract class AutoDoorState : State<MyContext>
        {
            public abstract void onOpenRequested(MyContext context);
            public abstract void onCloseRequested(MyContext context);
        }

        class ClosedState : AutoDoorState
        {
            public override void update(MyContext context)
            {
            }

            public override void onCloseRequested(MyContext context)
            {
            }

            public override void onOpenRequested(MyContext context)
            {
                context.transition(new DepressurizingState());
            }
        }

        class DepressurizingState : AutoDoorState
        {
            public override void update(MyContext context)
            {
                foreach (IMyAirVent vent in context._airVents)
                {
                    vent.Depressurize = true;
                }

                bool allDepressurized = true;
                foreach (IMyAirVent vent in context._airVents)
                {
                    if (vent.GetOxygenLevel() > 0)
                    {
                        allDepressurized = false;
                        break;
                    }
                }

                if (allDepressurized)
                {
                    context.transition(new OpenableState());
                }

                context.updateSensors();
            }

            public override void onCloseRequested(MyContext context)
            {
                if (!context.anyDesiredOpen())
                {
                    context.transition(new PressurizingState());
                }
            }

            public override void onOpenRequested(MyContext context)
            {
            }
        }

        class PressurizingState : AutoDoorState
        {
            public override void update(MyContext context)
            {
                foreach (IMyBlockGroup group in context._hangarDoorGroups)
                {
                    context.setDoorGroupOpenState(group, false);
                }

                foreach (IMyAirVent vent in context._airVents)
                {
                    vent.Depressurize = false;
                }

                bool allPressurized = true;
                foreach (IMyAirVent vent in context._airVents)
                {
                    if (vent.CanPressurize && vent.Status != VentStatus.Pressurized)
                    {
                        allPressurized = false;
                        break;
                    }
                }

                if (allPressurized && context.allDoorsClosed())
                {
                    context.transition(new ClosedState());
                }

                context.updateSensors();
            }

            public override void onCloseRequested(MyContext context)
            {
            }

            public override void onOpenRequested(MyContext context)
            {
                context.transition(new DepressurizingState());
            }
        }

        class OpenableState : AutoDoorState
        {
            public override void update(MyContext context)
            {
                foreach (IMyBlockGroup group in context._hangarDoorGroups)
                {
                    bool desiredOpen = context._desiredStates[group.Name].IsDesiredOpen;
                    context.setDoorGroupOpenState(group, desiredOpen);
                }

                foreach (IMyAirVent vent in context._airVents)
                {
                    // In case a door somehow closes, don't let the vent state be 
                    // inconsistent with script state
                    vent.Depressurize = true;
                }

                context.updateSensors();
            }

            public override void onCloseRequested(MyContext context)
            {
                if (!context.anyDesiredOpen())
                {
                    context.transition(new PressurizingState());
                }
            }

            public override void onOpenRequested(MyContext context)
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

        StateMachineProgram<MyContext> _impl;
        List<IMySensorBlock> _tempSensors = new List<IMySensorBlock>();

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
            if (command == "open")
            {
                string groupName = string.Join(" ", args);
                _impl.Context.open(groupName);
            }
            else if (command == "close")
            {
                string groupName = string.Join(" ", args);
                _impl.Context.close(groupName);
            }
            else if (command == "toggle")
            {
                string groupName = string.Join(" ", args);
                _impl.Context.toggle(groupName);
            }
            else if (command == "open_all")
            {
                _impl.Context.setDesiredOpenAll(true);
            }
            else if (command == "close_all")
            {
                _impl.Context.setDesiredOpenAll(false);
            }
            else if (command == "open_for_sensor")
            {
                string[] names = string.Join(" ", args).Split('/');
                string sensorName = names[0];
                string groupName = names[1];

                _tempSensors.Clear();
                GridTerminalSystem.GetBlocksOfType(_tempSensors, b => b.CustomName == sensorName);

                if (_tempSensors.Count != 1)
                {
                    return;
                }

                _impl.Context.openForSensor(_tempSensors[0], groupName);
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
