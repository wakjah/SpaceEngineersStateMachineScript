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
        List<IMyShipMergeBlock> _mergeBlocks = new List<IMyShipMergeBlock>(64);

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void trigger(string command, string[] args)
        {
        }

        public void Save()
        {
        }

        private bool _state = false;

        public void Main(string argument, UpdateType updateSource)
        {
            _mergeBlocks.Clear();
            GridTerminalSystem.GetBlocksOfType(_mergeBlocks);

            bool anyConnected = false;
            foreach (IMyShipMergeBlock merge in _mergeBlocks)
            {
                anyConnected |= merge.IsConnected;
            }

            Echo("blocks " + _mergeBlocks.Count + "; connected? " + anyConnected);
            _state = !_state;

            // While no merges are connected, flip on/off periodically
            bool enable = anyConnected || _state;
            foreach (IMyShipMergeBlock merge in _mergeBlocks)
            {
                merge.Enabled = enable;
            }
        }
    }
}