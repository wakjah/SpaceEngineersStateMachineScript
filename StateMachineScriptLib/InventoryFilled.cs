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
        public class InventoryFilled
        {
            public static double computeProportion(List<IMyCargoContainer> cargo)
            {
                long maxVolume = 0;
                long currentVolume = 0;
                foreach (IMyCargoContainer container in cargo)
                {
                    for (int i = 0; i < container.InventoryCount; ++i)
                    {
                        maxVolume += container.GetInventory(0).MaxVolume.RawValue;
                        currentVolume += container.GetInventory(0).CurrentVolume.RawValue;
                    }
                }

                return currentVolume / (double)maxVolume;
            }
        }
    }
}
