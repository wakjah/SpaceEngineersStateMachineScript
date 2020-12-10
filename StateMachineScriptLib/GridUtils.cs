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
    partial class Program
    {
        public class GridUtils
        {
            public static void findAllGrids(
                IMyCubeGrid root, 
                HashSet<IMyCubeGrid> found, 
                List<IMyMechanicalConnectionBlock> temp, 
                IMyGridTerminalSystem terminal
            )
            {
                found.Clear();
                found.Add(root);

                temp.Clear();
                terminal.GetBlocksOfType<IMyMechanicalConnectionBlock>(temp);

                foreach (IMyMechanicalConnectionBlock connection in temp)
                {
                    if (connection.TopGrid != null)
                    {
                        found.Add(connection.TopGrid);
                    }
                }
            }
        }
    }
}
