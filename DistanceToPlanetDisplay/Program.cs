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

        class NearestPlanet
        {
            private static readonly Vector3[] POSITIONS =
            {
                new Vector3(0, 0, 0),
                new Vector3(16384, 136384, -113616),
                new Vector3(1031072, 131072, 1631072),
                new Vector3(916384, 16384, 1616384),
                new Vector3(131072, 131072, 5731072),
                new Vector3(36384, 226384, 5796384),
            };

            private static readonly string[] NAMES =
            {
                "Earth",
                "Moon",
                "Mars",
                "Europa",
                "Alien",
                "Titan"
                    // todo missing triton & moon
            };

            private static readonly double[] RADII =
            {
                60000,
                9500,
                60000,
                9500,
                60000,
                9500
            };

            private static readonly double[] ORBIT_RADII =
            {
                103097,
                12315,
                101557,
                12674,
                104510,
                12315
            };

            private static readonly TimeSpan UPDATE_PERIOD = new TimeSpan(0, 5, 0);

            private bool _initialised = false;
            private TimeSpan _timeSinceLastUpdate = new TimeSpan();
            private int _nearestPlanet = 0;

            public void reset()
            {
                _initialised = false;
                _timeSinceLastUpdate = new TimeSpan();
                _nearestPlanet = 0;
            }

            public void update(Vector3 gridPosition, TimeSpan timeSinceLast, bool force = false)
            {
                _timeSinceLastUpdate += timeSinceLast;
                if (!_initialised || force || _timeSinceLastUpdate >= UPDATE_PERIOD)
                {
                    _nearestPlanet = findNearest(gridPosition);
                    _timeSinceLastUpdate = new TimeSpan();
                    _initialised = true;
                }
            }

            public Vector3 getNearestPlanetPosition()
            {
                return POSITIONS[_nearestPlanet];
            }

            public string getNearestPlanetName()
            {
                return NAMES[_nearestPlanet];
            }

            public double getNearestPlanetRadius()
            {
                return RADII[_nearestPlanet];
            }

            public double getNearestPlanetOrbitRadius()
            {
                return ORBIT_RADII[_nearestPlanet];
            }

            private static int findNearest(Vector3 gridPosition)
            {
                double minDist = double.PositiveInfinity;
                int minDistIndex = -1;
                for (int i = 0; i < POSITIONS.Length; ++i)
                {
                    double dist = (gridPosition - POSITIONS[i]).Length();
                    if (dist < minDist)
                    {
                        minDist = dist;
                        minDistIndex = i;
                    }
                }
                return minDistIndex;
            }
        }

        class MyContext : Context<MyContext>
        {
            public static readonly StoppedState Stopped = new StoppedState();

            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);
            public List<IMyCockpit> _cockpits = new List<IMyCockpit>(16);
            
            public Vector3D _planetPosition = new Vector3D();
            private StringBuilder _stringBuilder = new StringBuilder();
            public NearestPlanet _nearestPlanet = new NearestPlanet();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            private void updatePlanetInfo(TimeSpan timeSinceLastUpdate)
            {
                Vector3D gridPosition = getTargetingBlockPosition();
                _nearestPlanet.update(gridPosition, timeSinceLastUpdate);
                _planetPosition = _nearestPlanet.getNearestPlanetPosition();
            }

            public override void update(TimeSpan timeSinceLastUpdate)
            {
                updatePlanetInfo(timeSinceLastUpdate);

                base.update(timeSinceLastUpdate);
            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _cockpits.Clear();
                // Todo make cockpit name an argument
                Program.GridTerminalSystem.GetBlocksOfType(_cockpits, b => b.CustomName.Contains("NearestGravityDisplay"));

                _textPanels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains("NearestGravityDisplay"));

                //return _cockpits.Count > 0;

                return true;
            }

            protected override void updateDisplayImpl()
            {
                _stringBuilder.Clear();

                double distanceToPlanetSurface =
                    (getTargetingBlockPosition() - _planetPosition).Length()
                    - _nearestPlanet.getNearestPlanetOrbitRadius();
                _stringBuilder.Append(string.Format("{0} gravity:\n  ", _nearestPlanet.getNearestPlanetName()));
                _stringBuilder.Append(string.Format("{0:0.00} km", distanceToPlanetSurface / 1000));
                _stringBuilder.Append("\n");

                string text = _stringBuilder.ToString();

                foreach (IMyCockpit cockpit in _cockpits)
                {
                    IMyTextSurfaceProvider surfaceProvider = cockpit as IMyTextSurfaceProvider;
                    if (surfaceProvider.SurfaceCount > 0)
                    {
                        IMyTextSurface surface = surfaceProvider.GetSurface(0);
                        surface.ContentType = ContentType.TEXT_AND_IMAGE;
                        surface.WriteText(text);
                        surface.FontSize = 2.3f;
                        surface.Alignment = TextAlignment.CENTER;
                    }
                }

                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                    panel.FontSize = 2.3f;
                    panel.Alignment = TextAlignment.CENTER;
                }
            }

            public Vector3D getTargetingBlockPosition()
            {
                return Program.Me.CubeGrid.GetPosition();
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