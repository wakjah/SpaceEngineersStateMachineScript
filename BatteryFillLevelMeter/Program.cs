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
        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            update();
        }

        public void Save()
        {
        }

        private static readonly TimeSpan UPDATE_INTERVAL = new TimeSpan(0, 0, 10);
        private TimeSpan _timeSinceUpdate = new TimeSpan();
        private bool _initialised = false;
        private List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>(16);
        private StringBuilder _sb = new StringBuilder();

        public void Main(string argument, UpdateType updateSource)
        {
            if (_initialised)
            {
                _timeSinceUpdate += Runtime.TimeSinceLastRun;
            }
            _initialised = true;

            if (_timeSinceUpdate >= UPDATE_INTERVAL)
            {
                _timeSinceUpdate = new TimeSpan();
                update();
            }
        }

        private void update()
        {
            _batteries.Clear();
            GridTerminalSystem.GetBlocksOfType(_batteries, b => !b.HasLocalPlayerAccess());

            double totalStoredPower = 0;
            foreach (IMyBatteryBlock b in _batteries)
            {
                totalStoredPower += b.CurrentStoredPower;
            }

            _sb.Clear();
            _sb.Append("Batteries: ");
            _sb.Append(_batteries.Count);
            _sb.Append("\n");
            _sb.Append("Power: ");
            _sb.AppendFormat("{0:0.00}", totalStoredPower);

            IMyTextSurface surface = Me.GetSurface(0);
            surface.ContentType = ContentType.TEXT_AND_IMAGE;
            surface.WriteText(_sb);
        }
    }
}
