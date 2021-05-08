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

        enum CruiseDebug
        {
            None,
            Forward,
            Horizontal,
            Vertical,
            Pitch,
            Roll,
            Yaw,
            CustomTest
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

        enum TargetingMode
        {
            Override,
            Trilateration,
            None
        }

        class MyContext : Context<MyContext>
        {
            public static readonly StoppedState Stopped = new StoppedState();

            public Thrust _thrust = new Thrust();
            public List<IMyGyro> _gyros = new List<IMyGyro>(16);
            public List<Gyroscope> _compensatedGyros = new List<Gyroscope>(16);
            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);
            public List<IMyTextPanel> _textPanelsAux = new List<IMyTextPanel>(1);
            public List<IMyTextPanel> _textPanelsTrilaterationInput = new List<IMyTextPanel>(1);
            public List<IMyTextPanel> _textPanelsTrilaterationOutput = new List<IMyTextPanel>(1);
            public List<IMyTextPanel> _textPanelsDebug = new List<IMyTextPanel>(1);
            public List<IMyShipController> _cockpits = new List<IMyShipController>(16);
            public List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>(16);
            public List<IMyTerminalBlock> _targetingBlock = new List<IMyTerminalBlock>(1);
            public CruiseDebug _debug = CruiseDebug.None;
            public VelocityTracker _velocityTracker = new VelocityTracker();
            public Vector3D _planetPosition = new Vector3D();
            public Vector3D _directionFromPlanetToMe = new Vector3D();
            private StringBuilder _stringBuilder = new StringBuilder();
            public NearestPlanet _nearestPlanet = new NearestPlanet();
            public double _lastPitchError_rads = 0;
            public double _lastYawError_rads = 0;
            private TrilaterationResult _lastTrilaterationResult = TrilaterationResult.NotEnoughPoints; // Initially assume not enough data
            private Vector3D _targetPositionFromTrilateration = new Vector3D();
            private Vector3D _targetPosition = new Vector3D();
            public TargetingMode _targetingMode = TargetingMode.None;
            private double _gridMass = 0.0;

            public static readonly float ORBIT_SAFETY_MARGIN = 200;

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            private void updatePlanetInfo(TimeSpan timeSinceLastUpdate)
            {
                Vector3D gridPosition = getTargetingBlockPosition();
                _nearestPlanet.update(gridPosition, timeSinceLastUpdate);
                _planetPosition = _nearestPlanet.getNearestPlanetPosition();

                _directionFromPlanetToMe = (gridPosition - _planetPosition);
                _directionFromPlanetToMe.Normalize();
            }

            public override void update(TimeSpan timeSinceLastUpdate)
            {
                _velocityTracker.update(getTargetingBlockPosition(), timeSinceLastUpdate);
                updatePlanetInfo(timeSinceLastUpdate);

                base.update(timeSinceLastUpdate);
            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                
                _textPanels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains("CruiseMain"));

                _textPanelsAux.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanelsAux, b => b.CustomName.Contains("CruiseAux"));

                _textPanelsTrilaterationInput.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanelsTrilaterationInput, b => b.CustomName.Contains("CruiseTrilaterationInput"));

                _textPanelsTrilaterationOutput.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanelsTrilaterationOutput, b => b.CustomName.Contains("CruiseTrilaterationOutput"));

                _textPanelsDebug.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanelsDebug, b => b.CustomName.Contains("CruiseDebug"));

                _cockpits.Clear();
                // Todo make cockpit name an argument
                Program.GridTerminalSystem.GetBlocksOfType(_cockpits, b => b.CustomName.Contains("CruiseControl"));

                _targetingBlock.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_targetingBlock, b => b.CustomName.Contains("CruiseTargeting"));

                _batteries.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_batteries);

                _gyros.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_gyros);

                if (_cockpits.Count > 0)
                {
                    _compensatedGyros.Clear();
                    foreach (IMyGyro gyro in _gyros)
                    {
                        _compensatedGyros.Add(new Gyroscope(gyro, _cockpits[0]));
                    }

                    _thrust.update(Program.GridTerminalSystem, _cockpits[0]);

                    _gridMass = _cockpits[0].CalculateShipMass().PhysicalMass;
                }

                updateTargetPosition();

                // At least one cockpit and targeting block are required
                return _cockpits.Count > 0 && _targetingBlock.Count > 0;
            }

            protected override void updateDisplayImpl()
            {
                updateMainDisplayImpl();
                updateAuxDisplayImpl();
                updateTrilaterationOutputDisplayImpl();
            }

            private void updateMainDisplayImpl()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("Orbital Targeting\n");
                if (!FoundAllBlocks)
                {
                    _stringBuilder.Append("ERROR: Missing blocks; ensure cockpit and targeting block setup\n");
                }

                bool isCruising = State is CruiseControlState;
                _stringBuilder.Append("Status:\n  ");
                if (!FoundAllBlocks)
                {
                    _stringBuilder.Append("Unavailable: Missing blocks");
                }
                else if (!TargetAcquired)
                {
                    _stringBuilder.Append("Unavailable: No target");
                }
                else
                {
                    _stringBuilder.Append(isCruising ? "Cruising" : "Off");
                }
                _stringBuilder.Append("\n");

                _stringBuilder.Append("Targeting mode: ");
                _stringBuilder.Append(_targetingMode.ToString());
                _stringBuilder.Append("\n");

                _stringBuilder.Append("Nearest planet:\n  ");
                _stringBuilder.Append(string.Format("{0}", _nearestPlanet.getNearestPlanetName()));
                _stringBuilder.Append("\n");

                double distanceToPlanetSurface =
                    (getTargetingBlockPosition() - _planetPosition).Length()
                    - _nearestPlanet.getNearestPlanetRadius();
                _stringBuilder.Append(string.Format("Distance to {0}:\n  ", _nearestPlanet.getNearestPlanetName()));
                _stringBuilder.Append(string.Format("{0:0.00} km", distanceToPlanetSurface / 1000));
                _stringBuilder.Append("\n");

                _stringBuilder.Append(string.Format("Error:\n  pitch={0:0.00} deg\n  yaw={1:0.00} deg", _lastPitchError_rads * 180 / Math.PI, _lastYawError_rads * 180 / Math.PI));
                _stringBuilder.Append("\n");

                double errorX = distanceToPlanetSurface * Math.Sin(_lastPitchError_rads);
                double errorY = distanceToPlanetSurface * Math.Sin(_lastYawError_rads);
                double errorAbs = Math.Sqrt(errorX * errorX + errorY * errorY);
                _stringBuilder.Append(string.Format("Accuracy:\n  +/- {0:0.00} m", errorAbs));

                string text = _stringBuilder.ToString();

                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }
            }

            private void updateAuxDisplayImpl()
            {
                _stringBuilder.Clear();
                Vector3D firingPosition = getFiringPosition();

                Vector3D position = getTargetingBlockPosition();
                Vector3D positionError = position - firingPosition;
                _stringBuilder.Append("Fire pos:\n");
                _stringBuilder.Append(string.Format("  X={0:0.0000}\n  Y={1:0.0000}\n  Z={2:0.0000}\n", firingPosition.X, firingPosition.Y, firingPosition.Z));

                _stringBuilder.Append("Dist to fire pos:\n");
                _stringBuilder.Append(string.Format("  X={0:0.0000}\n  Y={1:0.0000}\n  Z={2:0.0000}\n", positionError.X, positionError.Y, positionError.Z));

                string text = _stringBuilder.ToString();
                foreach (IMyTextPanel panel in _textPanelsAux)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }
            }

            private void updateTrilaterationOutputDisplayImpl()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("Trilateration:\n");
                _stringBuilder.Append("Status: ");
                switch (_lastTrilaterationResult)
                {
                    case TrilaterationResult.Ok:
                        _stringBuilder.Append("Success");
                        break;
                    case TrilaterationResult.NotEnoughPoints:
                        _stringBuilder.Append("Not enough data\nPlease take more range measurements.");
                        break;
                    case TrilaterationResult.NoSolution:
                        _stringBuilder.Append("Invalid data: no firing solution found.");
                        break;
                }
                _stringBuilder.Append("\n");


                _stringBuilder.AppendFormat(
                    "  X={0:0.0000}\n  Y={1:0.0000}\n  Z={2:0.0000}\n",
                    _targetPositionFromTrilateration.X,
                    _targetPositionFromTrilateration.Y,
                    _targetPositionFromTrilateration.Z
                );

                string text = _stringBuilder.ToString();
                foreach (IMyTextPanel panel in _textPanelsTrilaterationOutput)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }
            }

            public Vector3D getFiringPosition()
            {
                Vector3D directionVector = _targetPosition - _nearestPlanet.getNearestPlanetPosition();
                directionVector.Normalize();
                return directionVector * (_nearestPlanet.getNearestPlanetOrbitRadius() + MyContext.ORBIT_SAFETY_MARGIN);
            }

            public Vector3D getTargetingBlockPosition()
            {
                if (_targetingBlock.Count > 0)
                {
                    return _targetingBlock[0].GetPosition();
                }
                else
                {
                    return Program.Me.CubeGrid.GetPosition();
                }
            }

            public bool TargetAcquired { get { return _targetingMode != TargetingMode.None; } }

            private void updateTargetPosition()
            {
                // If we have no target in the custom data, try to get one from trilateration. Otherwise fail
                if (updateTargetPositionFromCustomData())
                {
                    _targetingMode = TargetingMode.Override;
                }
                else if (updateTargetPositionFromTrilateration())
                {
                    _targetingMode = TargetingMode.Trilateration;
                }
                else
                {
                    _targetingMode = TargetingMode.None;
                }
            }

            public bool updateTargetPositionFromCustomData()
            {
                string data = Program.Me.CustomData.Trim();
                if (data.Length == 0)
                {
                    _targetPosition = new Vector3();
                    return false;
                }

                if (data.StartsWith("#"))
                {
                    // User requested to ignore the value without clearing custom data
                    return false;
                }

                string[] parts = data.Split(' ');
                _targetPosition = new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));

                return true;
            }
            
            public bool updateTargetPositionFromTrilateration()
            {
                // Update the targetting position by interfacing with Alastor's code
                // 
                // The data should be contained in the text pannel named "CruiseTargettingData"
                // It is a series of lines of GPS position followed by a distance in km. 
                // Spurious white space characters are ignored. 
                // 
                // Example : 
                // #1:1087630:131373:1650670:#000000:19.90
                // #2:1111172:130967:1631037:#000000:20.10
                // #3:1101042:131107:1648341:#000000:19.94
                // #4:1105568:145568:1631072:#000000:20.50
                // #5:1112285:131146:1652285:#000000:30.00
                // #6:1091072:131072:1648572:#000000:17.50
                // #7:1109451:134313:1620297:#000000:21.55
                // 
                // The target is calculated by this script when launching with action "calculate_target"

                Trilateration trilateration = new Trilateration();

                if (_textPanelsTrilaterationInput.Count == 0)
                {
                    log("No targetting data text panel detected. ");
                    return false;
                }
                foreach (IMyTextPanel panel in _textPanelsTrilaterationInput)
                {
                    string text = panel.GetText();
                    trilateration.Add(text);
                }

                _lastTrilaterationResult = trilateration.calculateAverageTarget();
                if (_lastTrilaterationResult == TrilaterationResult.Ok)
                {
                    _targetPositionFromTrilateration = trilateration.calculatedTarget;
                }
                else
                {
                    _targetPositionFromTrilateration = new Vector3D();
                }

                _targetPosition = _targetPositionFromTrilateration;

                return _lastTrilaterationResult == TrilaterationResult.Ok;
            }

            public void displayDebugText(string text, bool append = false)
            {
                if (_debug == CruiseDebug.None)
                {
                    return;
                }

                foreach (IMyTextPanel panel in _textPanelsDebug)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text, append);
                }
            }

            public Vector3D getAcceleration()
            {
                Vector3D directionForward = _cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                Vector3D directionUp = _cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                Vector3D directionRight = _cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Right);

                double m = _gridMass;
                if (m == 0)
                {
                    return new Vector3D();
                }
                return (_thrust.currentThrust_Newtons(Thrust.FWD) - _thrust.currentThrust_Newtons(Thrust.REV)) / m * directionForward
                    + (_thrust.currentThrust_Newtons(Thrust.UP) - _thrust.currentThrust_Newtons(Thrust.DOWN)) / m * directionUp
                    + (_thrust.currentThrust_Newtons(Thrust.RIGHT) - _thrust.currentThrust_Newtons(Thrust.LEFT)) / m * directionRight;
            }
        }

        class StoppedState : State<MyContext>
        {
            public override void update(MyContext context)
            {
                context.log("Stopped");
            }
        }

        private static double getBatteryOutput_MW(List<IMyBatteryBlock> batteries)
        {
            double total = 0;
            foreach (IMyBatteryBlock block in batteries)
            {
                total += block.CurrentOutput;
            }
            return total;
        }

        class VelocityTracker
        {
            bool _velocityInitialised = false;
            private Vector3D _vMeasLastPosition = new Vector3D();
            public Vector3D Velocity { get; private set; }

            public bool IsInitialized { get { return _velocityInitialised; } }

            public VelocityTracker()
            {
                Velocity = new Vector3D();
            }

            public void update(Vector3D position, TimeSpan timeSinceLast)
            {
                double seconds = timeSinceLast.TotalSeconds;
                if (_velocityInitialised && seconds > 0)
                {
                    Velocity = (position - _vMeasLastPosition) / seconds;
                }
                else
                {
                    Velocity = new Vector3D();
                }
                _velocityInitialised = true;
                _vMeasLastPosition = position;
            }
        }

        //Hellothere_1's Fast Gyroscope Adjustment Code

        public class Gyroscope
        {
            public IMyGyro gyro;
            private int[] conversionVector = new int[3];

            public Gyroscope(IMyGyro gyroscope, IMyTerminalBlock reference)
            {
                gyro = gyroscope;

                for (int i = 0; i < 3; i++)
                {
                    Vector3D vectorShip = GetAxis(i, reference);

                    for (int j = 0; j < 3; j++)
                    {
                        double dot = vectorShip.Dot(GetAxis(j, gyro));

                        if (dot > 0.9)
                        {
                            conversionVector[j] = i;
                            break;
                        }
                        if (dot < -0.9)
                        {
                            conversionVector[j] = i + 3;
                            break;
                        }
                    }
                }
            }

            public void SetRotation(float[] rotationVector, float gyroPower)
            {
                gyro.GyroOverride = true;
                gyro.GyroPower = gyroPower;
                gyro.Pitch = rotationVector[conversionVector[0]];
                gyro.Yaw = rotationVector[conversionVector[1]];
                gyro.Roll = rotationVector[conversionVector[2]];
            }

            private Vector3D GetAxis(int dimension, IMyTerminalBlock block)
            {
                switch (dimension)
                {
                    case 0:
                        return block.WorldMatrix.Right;
                    case 1:
                        return block.WorldMatrix.Up;
                    default:
                        return block.WorldMatrix.Backward;
                }
            }
        }

        class CruiseControlState : State<MyContext>
        {
            private static double IMIN = -0.25;
            private static double IMAX = 0.25;

            private static double P_FWD = 10;
            // We can't use integral control on this one because it's unable to 
            // overshoot, meaning the integrator will just get stuck at the max
            private static double I_FWD = 0.0;
            private static double D_FWD = -1;

            private static double P_VERT = P_FWD;
            private static double I_VERT = I_FWD;
            private static double D_VERT = D_FWD;

            private static double P_HORZ = P_FWD;
            private static double I_HORZ = I_FWD;
            private static double D_HORZ = D_FWD;

            private static double P_GYRO = 4;
            private static double I_GYRO = 0.01;
            private static double D_GYRO = -2;
            private static double IMIN_GYRO = -1;
            private static double IMAX_GYRO = 1;

            private PidController _fwdController = new PidController(P_FWD, I_FWD, D_FWD, IMIN, IMAX);
            private PidController _vertController = new PidController(P_VERT, I_VERT, D_VERT, IMIN, IMAX);
            private PidController _horzController = new PidController(P_HORZ, I_HORZ, D_HORZ, IMIN, IMAX);
            private PidController _pitchController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);
            private PidController _yawController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);

            private float[] _rotationVector = new float[6];

            private Vector3D _lastPosition = new Vector3D();

            private TimeSpan _timeRunning = new TimeSpan();

            public override void update(MyContext context)
            {
                if (!context.FoundAllBlocks || !context.TargetAcquired)
                {
                    context.transition(MyContext.Stopped);
                    return;
                }

                TimeSpan timeSinceLast = context.Program.Runtime.TimeSinceLastRun;

                Vector3D directionForward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                Vector3D directionBackward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward);
                Vector3D directionUp = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                Vector3D directionRight = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Right);

                Vector3D v = context._velocityTracker.Velocity;

                Vector3D desiredGyroDirection = -context._directionFromPlanetToMe;

                Vector3D position = context.getTargetingBlockPosition();
                Vector3D firingPosition = context.getFiringPosition();
                Vector3D positionErrorDirection = firingPosition - position;
                double errorMagnitude = positionErrorDirection.Normalize();
                Vector3D desiredV;
                if (errorMagnitude > 0.05)
                {
                    double desiredAcceleration = 5;
                    double desiredSpeed = Math.Sqrt(2 * desiredAcceleration * errorMagnitude);
                    desiredV = positionErrorDirection * desiredSpeed;
                }
                else
                {
                    desiredV = new Vector3D();
                }
                double t = timeSinceLast.TotalSeconds;
                Vector3 desiredPositionDueToVelocity = position + desiredV * t;


                _lastPosition = position;

                Vector3D feedforwardPosition = position + v * t + context.getAcceleration() * t * t;
                Vector3D desiredPosition = desiredPositionDueToVelocity;

                double fwdControl = updateDirectionalController(
                    context,
                    _fwdController,
                    directionForward,
                    desiredPosition,
                    feedforwardPosition,
                    timeSinceLast
                );

                double vertControl = updateDirectionalController(
                    context,
                    _vertController,
                    directionUp,
                    desiredPosition,
                    feedforwardPosition,
                    timeSinceLast
                );

                double horzControl = updateDirectionalController(
                    context,
                    _horzController,
                    directionRight,
                    desiredPosition,
                    feedforwardPosition,
                    timeSinceLast
                );

                double pitchError = signedAngleBetweenNormalizedVectors(directionForward, desiredGyroDirection, directionRight);
                double pitchControl = _pitchController.update(timeSinceLast, pitchError, pitchError);
                if (Math.Abs(pitchError) < 1e-5)
                {
                    pitchControl = 0;
                }
                context._lastPitchError_rads = pitchError;

                double yawError = signedAngleBetweenNormalizedVectors(directionForward, desiredGyroDirection, directionUp);
                double yawControl = _yawController.update(timeSinceLast, yawError, yawError);
                if (Math.Abs(yawError) < 1e-5)
                {
                    yawControl = 0;
                }
                context._lastYawError_rads = yawError;

                float gyroPower = 1;
                if (Math.Abs(pitchError) < 1e-2 && Math.Abs(yawError) < 1e-2)
                {
                    pitchControl *= 2;
                    yawControl *= 2;
                    gyroPower = 0.1f;
                }

                switch (context._debug)
                {
                    case CruiseDebug.None:
                        break;
                    case CruiseDebug.Forward:
                        displayController(context, _fwdController, "Forward");
                        break;
                    case CruiseDebug.Horizontal:
                        displayController(context, _horzController, "Horizontal");
                        break;
                    case CruiseDebug.Vertical:
                        displayController(context, _vertController, "Vertical");
                        break;
                    case CruiseDebug.Pitch:
                        displayController(context, _pitchController, "Pitch");
                        break;
                    case CruiseDebug.Roll:
                        //displayController(context, _rollController, "Roll");
                        break;
                    case CruiseDebug.Yaw:
                        displayController(context, _yawController, "Yaw");
                        break;
                }

                float roll = context._cockpits[0].RollIndicator;

                Vector3 moveIndicator = context._cockpits[0].MoveIndicator;
                float moveVert = moveIndicator.Y;
                float moveForward = moveIndicator.Z;
                if (moveVert > 0.1)
                {
                    vertControl = 1;
                }
                else if (moveVert < -0.1)
                {
                    vertControl = -1;
                }

                if (moveForward > 0.1)
                {
                    // Stop if the user presses backwards
                    context.transition(MyContext.Stopped);
                    return;
                }
                else if (moveForward < -0.1)
                {
                    fwdControl = 1;
                }

                // Divide by 2 because it goes -180 to 180 instead of -90 to 90
                _rotationVector[0] = (float)pitchControl;
                _rotationVector[1] = (float)yawControl;
                _rotationVector[2] = roll;
                _rotationVector[3] = -_rotationVector[0];
                _rotationVector[4] = -_rotationVector[1];
                _rotationVector[5] = -_rotationVector[2];

                foreach (Gyroscope gyro in context._compensatedGyros)
                {
                    gyro.SetRotation(_rotationVector, gyroPower);
                }


                _timeRunning += timeSinceLast;
                context._thrust.setOverrideRatio(Thrust.FWD, fwdControl);
                context._thrust.setOverrideRatio(Thrust.REV, -fwdControl);
                context._thrust.setOverrideRatio(Thrust.UP, vertControl);
                context._thrust.setOverrideRatio(Thrust.DOWN, -vertControl);
                context._thrust.setOverrideRatio(Thrust.RIGHT, horzControl);
                context._thrust.setOverrideRatio(Thrust.LEFT, -horzControl);
            }

            private void displayController(MyContext context, PidController controller, string name)
            {
                context.displayDebugText(
                        string.Format(
                            "{0}:\nP={1:0.00}\nI={2:0.00}\nD={3}\nCTRL={4}\nErr={5}\nPos={6}\nIntegral={7};\nDerivative={8}\n",
                            name,
                            controller.getPTerm(),
                            controller.getITerm(),
                            controller.getDTerm(),
                            controller.getControl(),
                            controller.getError(),
                            controller.getPosition(),
                            controller.getErrorIntegral(),
                            controller.getPositionDerivative()
                        ),
                        false
                    );
            }

            public double updateDirectionalController(
                MyContext context,
                PidController controller,
                Vector3D direction,
                Vector3D desiredPosition,
                Vector3D currentPosition,
                TimeSpan timeSinceLastUpdate
            )
            {
                Vector3D errorVector = desiredPosition - currentPosition;
                double error = errorVector.Dot(direction);
                double position = currentPosition.Dot(direction);
                double control = controller.update(timeSinceLastUpdate, error, position);
                return control;
            }

            private double signedAngleBetweenNormalizedVectors(Vector3D a, Vector3D b, Vector3D axis)
            {
                double sin = b.Cross(a).Dot(axis);
                double cos = a.Dot(b);

                return Math.Atan2(sin, cos);
            }


            private string printVector(Vector3D v)
            {
                return string.Format("X={0:0.00}, Y={1:0.00}, Z={2:0.00}", v.X, v.Y, v.Z);
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                context.updateBlocks();

                if (!context.FoundAllBlocks)
                {
                    context.transition(MyContext.Stopped);
                }

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;

               // context._cockpits[0].DampenersOverride = false;

               
                _fwdController.reset();
                _vertController.reset();
                _horzController.reset();
                _pitchController.reset();
                //_rollController.reset();
                _yawController.reset();

                Vector3D gridPosition = context.getTargetingBlockPosition();
                context._nearestPlanet.update(gridPosition, new TimeSpan(), true);
                _lastPosition = gridPosition;

                _timeRunning = new TimeSpan();
            }

            public override void leave(MyContext context)
            {
                base.leave(context);

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;

                if (context._cockpits.Count > 0)
                {
                    context._cockpits[0].DampenersOverride = true;
                }

                context._thrust.resetOverrideRatio();

                foreach (IMyGyro gyro in context._gyros)
                {
                    gyro.GyroOverride = false;
                    gyro.GyroPower = 1;
                }
            }
        }
        
        /// Alastor's trilateration code
        
        static class Combinations
        {
            // Source : https://codereview.stackexchange.com/questions/194967/get-all-combinations-of-selecting-k-elements-from-an-n-sized-array
            // Enumerate all possible m-size combinations of [0, 1, ..., n-1] array
            // in lexicographic order (first [0, 1, 2, ..., m-1]).
            private static IEnumerable<int[]> CombinationsRosettaWoRecursion(int m, int n)
            {
                int[] result = new int[m];
                Stack<int> stack = new Stack<int>(m);
                stack.Push(0);
                while (stack.Count > 0)
                {
                    int index = stack.Count - 1;
                    int value = stack.Pop();
                    while (value < n)
                    {
                        result[index++] = value++;
                        stack.Push(value);
                        if (index != m) continue;
                        yield return result;
                        break;
                    }
                }
            }
        
            public static IEnumerable<T[]> getAllCombinations<T>(T[] array, int m)
            {
                if (array.Length < m)
                    throw new ArgumentException("Array length can't be less than number of selected elements");
                if (m < 1)
                    throw new ArgumentException("Number of selected elements can't be less than 1");
                T[] result = new T[m];
                foreach (int[] j in CombinationsRosettaWoRecursion(m, array.Length))
                {
                    for (int i = 0; i < m; i++)
                    {
                        result[i] = array[j[i]];
                    }
                    yield return (T[])result.Clone(); // thanks to @xanatos
                    //yield return result;
                }
            }
        }
        
        class Matrix33
        {
            private double[,] _M = new double[3,3]{{0d,0d,0d},{0d,0d,0d},{0d,0d,0d}};
            
            public Matrix33(
                double M11, double M12, double M13, 
                double M21, double M22, double M23, 
                double M31, double M32, double M33
                )
            {
                this._M[0,0] = M11;
                this._M[0,1] = M12;
                this._M[0,2] = M13;
                this._M[1,0] = M21;
                this._M[1,1] = M22;
                this._M[1,2] = M23;
                this._M[2,0] = M31;
                this._M[2,1] = M32;
                this._M[2,2] = M33;
            }
            
            public double determinant 
            {
                get {
                    return this._M[0, 0] * (this._M[1, 1] * this._M[2, 2] - this._M[1, 2] * this._M[2, 1])
                         - this._M[0, 1] * (this._M[1, 0] * this._M[2, 2] - this._M[1, 2] * this._M[2, 0])
                         + this._M[0, 2] * (this._M[1, 0] * this._M[2, 1] - this._M[1, 1] * this._M[2, 0]);
                }
                
            }
        }
        
        class Range
        {
            private Vector3D _location = new Vector3D();
            private double _distance = 0;
            
            public Range(Vector3D location, double distance)
            {
                this._location = location;
                this._distance = distance;
            }
            
            public Range(double X, double Y, double Z, double distance)
            {
                this._location = new Vector3D(X, Y, Z);
                this._distance = distance;
            }
            
            public Range(string str)
            { 
                // Expected string format : "name:X:Y:Z:Color:distance"
                // with distance in km, X, Y and Z in m
                string[] substr = str.Split(':');
                double X = double.Parse(substr[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                double Y = double.Parse(substr[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                double Z = double.Parse(substr[3].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                double distance = double.Parse(substr[5].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                this._location = new Vector3D(X, Y, Z);
                this._distance = distance*1000;
            }
            
            public Vector3D Location { get { return this._location; } }
            public double Distance { get { return this._distance; } }
        }
        
        enum TrilaterationResult
        {
            Ok,
            NotEnoughPoints,
            NoSolution, // Is there a physical scenario in which there is no solution, or would it just be poor numerical conditions that cause this?
        }

        class Trilateration
        {
            private List<Range> _ranges = new List<Range>();
            private Vector3D _calculatedTarget = new Vector3D();

            public Vector3D calculatedTarget
            {
                get { return this._calculatedTarget; }
            }
            
            public void Add(Range range)
            {
                this._ranges.Add(range);
            }
            
            public void Add(Range[] ranges)
            {
                foreach(Range range in ranges)
                    this.Add(range);
            }
            
            public void Add(string rangesStr)
            {
                char[] separators = new char[] {'\n','\r'};
                foreach (string line in rangesStr.Trim().Split(separators, StringSplitOptions.RemoveEmptyEntries))
                    this.Add(new Range(line.Trim()));
            }
            
            public void Reset()
            {
                _ranges = new List<Range>();
                _calculatedTarget = new Vector3D();
            }
            
            private static Vector3D? calculateSingleTarget(Range rangeA, Range rangeB, Range rangeC, Range rangeD)
            {
                Vector3D vectA = rangeA.Location;
                double D_A = rangeA.Distance;
                Vector3D vectB = rangeB.Location;
                double D_B = rangeB.Distance;
                Vector3D vectC = rangeC.Location;
                double D_C = rangeC.Distance;
                Vector3D vectD = rangeD.Location;
                double D_D = rangeD.Distance;
                
                // Calculate intermediate factors
                Vector3D vectAB = 2*(vectB - vectA);
                double D_AB = (vectB.LengthSquared() - Math.Pow(D_B,2)) - (vectA.LengthSquared() - Math.Pow(D_A,2));
                Vector3D vectAC = 2*(vectC - vectA);
                double D_AC = (vectC.LengthSquared() - Math.Pow(D_C,2)) - (vectA.LengthSquared() - Math.Pow(D_A,2));
                Vector3D vectAD = 2*(vectD - vectA);
                double D_AD = (vectD.LengthSquared() - Math.Pow(D_D,2)) - (vectA.LengthSquared() - Math.Pow(D_A,2));
                
                // Solve the equation system
                double D = new Matrix33(vectAB.X, vectAB.Y, vectAB.Z, 
                                        vectAC.X, vectAC.Y, vectAC.Z, 
                                        vectAD.X, vectAD.Y, vectAD.Z).determinant;
                double D_X = new Matrix33(D_AB, vectAB.Y, vectAB.Z, 
                                          D_AC, vectAC.Y, vectAC.Z, 
                                          D_AD, vectAD.Y, vectAD.Z).determinant;
                double D_Y = new Matrix33(vectAB.X, D_AB, vectAB.Z, 
                                          vectAC.X, D_AC, vectAC.Z, 
                                          vectAD.X, D_AD, vectAD.Z).determinant;
                double D_Z = new Matrix33(vectAB.X, vectAB.Y, D_AB, 
                                          vectAC.X, vectAC.Y, D_AC, 
                                          vectAD.X, vectAD.Y, D_AD).determinant;
                
                // If there is a solution, return it, otherwise, null
                if (D == 0) return null;
                return new Vector3D(D_X/D, D_Y/D, D_Z/D);
            }
            
            public TrilaterationResult calculateAverageTarget()
            {
                // Check that we have enough data points
                double dataCount = this._ranges.Count;
                if (dataCount < 4)
                {
                    return TrilaterationResult.NotEnoughPoints;
                }
                
                List<Vector3D> targets = new List<Vector3D>();
                Vector3D? target = null;
                
                // Iterate over all the combinations of 4 Range points to measure the corresponding target
                foreach (Range[] c in Combinations.getAllCombinations(this._ranges.ToArray(), 4))
                {
                    target = calculateSingleTarget(c[0], c[1], c[2], c[3]);
                    if (target != null) targets.Add(target.Value); // If we have a solution, store it
                }

                // If no combination of data gave a solution, report failure
                if (targets.Count == 0)
                {
                    return TrilaterationResult.NoSolution;
                }
                
                // Otherwise, the target is the average of the solutions found
                this._calculatedTarget = new Vector3D(targets.Average(vect => vect.X),
                                                      targets.Average(vect => vect.Y),
                                                      targets.Average(vect => vect.Z));
                return TrilaterationResult.Ok;
            }
        }
        
        
        /// End of Alastor's trilateration code
        

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
            if (command == "cruise")
            {
                _impl.Context.log("Cruise control enabled");
                _impl.Context.transition(new CruiseControlState());
            }
            else if (command == "cancel_cruise")
            {
                _impl.Context.log("Cruise control disabled");
                _impl.Context.transition(MyContext.Stopped);
            }
            else if (command == "calculate_target")
            {
                _impl.Context.log("Target calculation requested");
                _impl.Context.updateTargetPositionFromTrilateration();
            }
            else if (command == "debug")
            {
                CruiseDebug debug = (CruiseDebug) Enum.Parse(typeof(CruiseDebug), args[0]);
                if (_impl.Context._debug == debug)
                {
                    // toggle
                    debug = CruiseDebug.None;
                }
                _impl.Context._debug = debug;
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
