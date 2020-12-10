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
        public class Thrust
        {
            private static double MIN_THRUST = 0.02;

            public static readonly int FWD = 0;     // Thrusts the ship forward
            public static readonly int REV = 1;     // Thrusts the ship backward aka slowdown
            public static readonly int UP = 2;      // Thrusts the ship upward
            public static readonly int DOWN = 3;    // Thrusts the ship down aka pushdown
            public static readonly int LEFT = 4;    // Thrusts the ship to the left
            public static readonly int RIGHT = 5;   // Thrusts the ship to the right

            /*public static readonly Vector3I[] GRID_THRUST_DIRECTIONS =
            {
                VRageMath.Vector3I.Backward,
                VRageMath.Vector3I.Forward,
                VRageMath.Vector3I.Down,
                VRageMath.Vector3I.Up,
                VRageMath.Vector3I.Right,
                VRageMath.Vector3I.Left
            };*/

            public static readonly Base6Directions.Direction[] GRID_THRUST_DIRECTIONS =
            {
                Base6Directions.Direction.Forward,
                Base6Directions.Direction.Backward,
                Base6Directions.Direction.Up,
                Base6Directions.Direction.Down,
                Base6Directions.Direction.Left,
                Base6Directions.Direction.Right
            };

            public List<IMyThrust>[] _thrust = {
                new List<IMyThrust>(16),
                new List<IMyThrust>(16),
                new List<IMyThrust>(16),
                new List<IMyThrust>(16),
                new List<IMyThrust>(16),
                new List<IMyThrust>(16)
            };

            private double[] _effectiveThrust = new double[6];
            private IMyShipController _cockpit;

            public void update(IMyGridTerminalSystem terminal, IMyShipController cockpit)
            {
                _cockpit = cockpit;

                Base6Directions.Direction cockpitForward = _cockpit.Orientation.TransformDirection(Base6Directions.Direction.Forward);
                for (int i = 0; i < 6; ++i)
                {
                    _thrust[i].Clear();
                    terminal.GetBlocksOfType(_thrust[i], b => 
                            // The desired direction, transformed by the cockpit, is equal to the direction of thrust
                            _cockpit.Orientation.TransformDirection(GRID_THRUST_DIRECTIONS[i]) 
                            == Base6Directions.GetFlippedDirection(b.Orientation.Forward));
                    _effectiveThrust[i] = computeTotalEffectiveThrust(_thrust[i]);
                }
            }

            private static double computeTotalEffectiveThrust(List<IMyThrust> thrust)
            {
                double sum = 0;
                foreach (IMyThrust t in thrust)
                {
                    sum += t.MaxEffectiveThrust;
                }
                return sum;
            }

            public void setOverrideRatio(int thrustIndex, double ratio)
            {
                setOverrideRatio(_thrust[thrustIndex], ratio);
            }

            public void resetOverrideRatio(int thrustIndex)
            {
                resetOverrideRatio(_thrust[thrustIndex]);
            }

            public void resetOverrideRatio()
            {
                for (int i = 0; i < 6; ++i)
                {
                    resetOverrideRatio(i);
                }
            }

            public static void setOverrideRatio(List<IMyThrust> thrusters, double ratio)
            {
                ratio = Math.Max(0, Math.Min(1, ratio));

                // Guard band to avoid useless tiny thrusts
                if (Math.Abs(ratio) < MIN_THRUST)
                {
                    ratio = 0;
                }

                foreach (IMyThrust thrust in thrusters)
                {
                    thrust.ThrustOverridePercentage = (float)(ratio);

                    // In case the user has dampers on but we want the thruster to do nothing,
                    // turn it off to stop it fighting us
                    thrust.Enabled = ratio != 0;
                }
            }

            public static void resetOverrideRatio(List<IMyThrust> thrusters)
            {
                foreach (IMyThrust thrust in thrusters)
                {
                    thrust.ThrustOverridePercentage = 0;
                    thrust.Enabled = true;
                }
            }

            public List<IMyThrust> this[int i]
            {
                get { return _thrust[i]; }
            }

            public double effectiveThrust_Newtons(int i)
            {
                return _effectiveThrust[i];
            }

            public double currentThrust_Newtons(int i)
            {
                double sum = 0;
                foreach (IMyThrust t in _thrust[i])
                {
                    sum += t.CurrentThrust;
                }
                return sum;
            }

            public List<IMyThrust> forward()
            {
                return _thrust[FWD];
            }

            public List<IMyThrust> reverse()
            {
                return _thrust[REV];
            }

            public List<IMyThrust> up()
            {
                return _thrust[UP];
            }

            public List<IMyThrust> down()
            {
                return _thrust[DOWN];
            }

            public List<IMyThrust> left()
            {
                return _thrust[LEFT];
            }

            public List<IMyThrust> right()
            {
                return _thrust[RIGHT];
            }
        }
    }
}
