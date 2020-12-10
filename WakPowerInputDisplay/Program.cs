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
        static readonly TimeSpan BLOCK_UPDATE_INTERVAL = new TimeSpan(0, 0, 30);
        static readonly TimeSpan DISPLAY_UPDATE_INTERVAL = new TimeSpan(0, 0, 2);

        bool _initialised = false;
        TimeSpan _timeSinceLastBlockUpdate = new TimeSpan();
        TimeSpan _timeSinceLastDisplayUpdate = new TimeSpan();
        List<IMyPowerProducer> _powerProducers = new List<IMyPowerProducer>(32);
        List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            _timeSinceLastBlockUpdate += Runtime.TimeSinceLastRun;
            _timeSinceLastDisplayUpdate += Runtime.TimeSinceLastRun;

            if (!_initialised || _timeSinceLastBlockUpdate >= BLOCK_UPDATE_INTERVAL)
            {
                updateBlocks();
                _timeSinceLastBlockUpdate = new TimeSpan();
            }

            if (!_initialised || _timeSinceLastDisplayUpdate >= DISPLAY_UPDATE_INTERVAL)
            {
                updateDisplay();
                _timeSinceLastDisplayUpdate = new TimeSpan();
            }

            _initialised = true;
        }

        private void updateBlocks()
        {
            GridTerminalSystem.GetBlocksOfType<IMyPowerProducer>(_powerProducers);

            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(_textPanels, b => b.CustomName.Contains("Power Production"));
        }

        private void updateDisplay()
        {
            float total_MW = 0;
            foreach (IMyPowerProducer p in _powerProducers)
            {
                if (!(p is IMyBatteryBlock))
                {
                    total_MW += p.CurrentOutput;
                }
            }

            string s = total_MW.ToString("Current Power\n0.00 MW");
            Echo(s);
            foreach (IMyTextPanel panel in _textPanels)
            {
                panel.WriteText(s);
            }
        }
    }
}
