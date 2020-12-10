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
            public static readonly DefaultState DefaultState = new DefaultState();

            public List<IMyShipDrill> _drills = new List<IMyShipDrill>(8);
            public List<IMyCockpit> _cockpits = new List<IMyCockpit>(1);

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), DefaultState)
            {
            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _drills.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_drills);

                _cockpits.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_cockpits);

                return _cockpits.Count > 0;
            }

            protected override void updateDisplayImpl()
            {
                
            }

            public static void setBlocksEnabled<T>(List<T> blocks, bool enabled) where T : IMyFunctionalBlock
            {
                foreach (T b in blocks)
                {
                    b.Enabled = enabled;
                }
            }
        }

        class DefaultState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                if (!context.FoundAllBlocks)
                {
                    return;
                }

                MyShipMass mass = context._cockpits[0].CalculateShipMass();
                float maxMass;
                if (!float.TryParse(context.Program.Me.CustomData, out maxMass))
                {
                    context.Program.Echo("Invalid max weight custom data");
                    return;
                }

                if (mass.PhysicalMass >= maxMass)
                {
                    MyContext.setBlocksEnabled(context._drills, false);
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