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
            CustomTest
        }

        class MyContext : Context<MyContext>
        {
            public static readonly StoppedState Stopped = new StoppedState();

            public List<IMyThrust> _fwdThrust = new List<IMyThrust>(16); // Thrusts the ship forward
            public List<IMyThrust> _revThrust = new List<IMyThrust>(16); // Thrusts the ship backward aka slowdown
            public List<IMyThrust> _upThrust = new List<IMyThrust>(16); // Thrusts the ship upward
            public List<IMyThrust> _downThrust = new List<IMyThrust>(16); // Thrusts the ship down aka pushdown
            public List<IMyThrust> _leftThrust = new List<IMyThrust>(16); // Thrusts the ship to the left
            public List<IMyThrust> _rightThrust = new List<IMyThrust>(16); // Thrusts the ship to the right
            public List<IMyGyro> _gyros = new List<IMyGyro>(16);
            public List<Gyroscope> _compensatedGyros = new List<Gyroscope>(16);
            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);
            public List<IMyTextPanel> _textPanelsDebug = new List<IMyTextPanel>(1);
            public List<IMyCockpit> _cockpits = new List<IMyCockpit>(16);
            public List<IMyBatteryBlock> _batteries = new List<IMyBatteryBlock>(16);
            public CruiseDebug _debug = CruiseDebug.None;
            public VelocityTracker _velocityTracker = new VelocityTracker();
            public double _startingElevation = 0.0;
            public Vector3D _planetPosition = new Vector3D();
            public bool _isInAtmosphere = false;
            public double _elevationFromSeaLevel = 0.0;
            public double _elevationFromSurface = 0.0;
            public Vector3D _directionFromPlanetToMe = new Vector3D();
            private StringBuilder _stringBuilder = new StringBuilder();
            public double _desiredSpeed = 0.0;

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            private void updatePlanetInfo()
            {
                _isInAtmosphere = false;
                if (FoundAllBlocks)
                {
                    if (!_cockpits[0].TryGetPlanetPosition(out _planetPosition))
                    {
                        return;
                    }

                    if (!_cockpits[0].TryGetPlanetElevation(MyPlanetElevation.Sealevel, out _elevationFromSeaLevel))
                    {
                        return;
                    }

                    if (!_cockpits[0].TryGetPlanetElevation(MyPlanetElevation.Surface, out _elevationFromSurface))
                    {
                        return;
                    }

                    _directionFromPlanetToMe = (Program.Me.CubeGrid.GetPosition() - _planetPosition);
                    _directionFromPlanetToMe.Normalize();

                    _isInAtmosphere = true;
                }
            }

            public override void update(TimeSpan timeSinceLastUpdate)
            {
                _velocityTracker.update(Program.Me.CubeGrid.GetPosition(), timeSinceLastUpdate);
                updatePlanetInfo();

                base.update(timeSinceLastUpdate);
            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                _fwdThrust.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_fwdThrust, b => b.GridThrustDirection == VRageMath.Vector3I.Backward);

                _revThrust.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_revThrust, b => b.GridThrustDirection == VRageMath.Vector3I.Forward);

                _upThrust.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_upThrust, b => b.GridThrustDirection == VRageMath.Vector3I.Down);

                _downThrust.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_downThrust, b => b.GridThrustDirection == VRageMath.Vector3I.Up);

                _leftThrust.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_leftThrust, b => b.GridThrustDirection == VRageMath.Vector3I.Right);

                _rightThrust.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_rightThrust, b => b.GridThrustDirection == VRageMath.Vector3I.Left);

                _textPanels.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains("CruiseMain"));

                _textPanelsDebug.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_textPanelsDebug, b => b.CustomName.Contains("CruiseDebug"));

                _cockpits.Clear();
                // Todo make cockpit name an argument
                Program.GridTerminalSystem.GetBlocksOfType(_cockpits, b => b.CustomName.Contains("Cruise"));

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
                }

                // At least one cockpit is required
                return _cockpits.Count > 0;
            }

            protected override void updateDisplayImpl()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("Cruise Control\n");
                if (!FoundAllBlocks)
                {
                    _stringBuilder.Append("ERROR: No cockpit found\n");
                }

                bool isCruising = State is CruiseControlState;
                _stringBuilder.Append("Status: ");
                if (!_isInAtmosphere || !FoundAllBlocks)
                {
                    _stringBuilder.Append("Unavailable");
                }
                else
                {
                    _stringBuilder.Append(isCruising ? "Cruising" : "Off");
                }
                _stringBuilder.Append("\n");

                _stringBuilder.Append("Altitude (sea lvl): ");
                _stringBuilder.Append(string.Format("{0:0.00}", _elevationFromSeaLevel));
                _stringBuilder.Append("\n");

                if (isCruising)
                {
                    _stringBuilder.Append("Cruise altitude: ");
                    _stringBuilder.Append(string.Format("{0:0.00}", _startingElevation));
                    _stringBuilder.Append("\n");
                }

                double verticalSpeed = _velocityTracker.Velocity.Dot(_directionFromPlanetToMe);
                _stringBuilder.Append("Vertical speed: ");
                _stringBuilder.Append(string.Format("{0:0.00}", verticalSpeed));
                _stringBuilder.Append("\n");

                if (FoundAllBlocks)
                {
                    Vector3D directionForward = _cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                    double forwardSpeed = _velocityTracker.Velocity.Dot(directionForward);
                    _stringBuilder.Append("Forward speed: ");
                    _stringBuilder.Append(string.Format("{0:0.00}", forwardSpeed));
                    _stringBuilder.Append("\n");

                    _stringBuilder.Append("Target forward speed: ");
                    _stringBuilder.Append(string.Format("{0:0.00}", _desiredSpeed));
                    _stringBuilder.Append("\n");

                    Vector3D directionRight = _cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Right);
                    double horizontalSpeed = _velocityTracker.Velocity.Dot(directionRight);
                    _stringBuilder.Append("Horizontal speed: ");
                    _stringBuilder.Append(string.Format("{0:0.00}", horizontalSpeed));
                    _stringBuilder.Append("\n");
                }

                double verticalThrustMargin = getVerticalThrustMargin_N();
                _stringBuilder.Append(string.Format("Vertical thrust margin: {0:0.00} MN\n", verticalThrustMargin / 1e6));

                string text = _stringBuilder.ToString();

                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }
            }

            public double getVerticalThrustMargin_N()
            {
                if (_cockpits.Count == 0)
                {
                    return 0;
                }

                double totalThrust_N = getTotalEffectiveThrust(_upThrust);

                double totalMass_kg = _cockpits[0].CalculateShipMass().TotalMass;
                double g = _cockpits[0].GetNaturalGravity().Length();
                double requiredForce_N = totalMass_kg * g;

                return totalThrust_N - requiredForce_N;
            }

            public double getTotalEffectiveThrust(List<IMyThrust> thrust)
            {
                double totalThrust_N = 0;
                foreach (IMyThrust t in thrust)
                {
                    totalThrust_N += t.MaxEffectiveThrust;
                }
                return totalThrust_N;
            }

            public void setThrustOverrideRatio(List<IMyThrust> thrusters, double ratio)
            {
                ratio = Math.Max(0, Math.Min(1, ratio));

                // Guard band to avoid useless tiny thrusts
                if (Math.Abs(ratio) < 0.1)
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

            public void resetThrustOverrideRatio(List<IMyThrust> thrusters)
            {
                foreach (IMyThrust thrust in thrusters)
                {
                    thrust.ThrustOverridePercentage = 0;
                    thrust.Enabled = true;
                }
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

            public void SetRotation(float[] rotationVector)
            {
                gyro.GyroOverride = true;
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

            private static double P_VERT = 100;
            private static double I_VERT = 10;
            private static double D_VERT = 0.0;

            private static double P_FWD = 30;
            // We can't use integral control on this one because it's unable to 
            // overshoot, meaning the integrator will just get stuck at the max
            private static double I_FWD = 0.0;
            private static double D_FWD = 0.0001;

            private static double P_HORZ = P_FWD;
            private static double I_HORZ = I_FWD;
            private static double D_HORZ = D_FWD;

            private static double P_GYRO = 4;
            private static double I_GYRO = 0.01;
            private static double D_GYRO = 0.2;
            private static double IMIN_GYRO = -1;
            private static double IMAX_GYRO = 1;

            private PidController _fwdController = new PidController(P_FWD, I_FWD, D_FWD, IMIN, IMAX);
            private PidController _vertController = new PidController(P_VERT, I_VERT, D_VERT, IMIN, IMAX);
            private PidController _horzController = new PidController(P_HORZ, I_HORZ, D_HORZ, IMIN, IMAX);
            private PidController _pitchController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);
            private PidController _rollController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);

            private float[] _rotationVector = new float[6];

            private Vector3D _lastPosition = new Vector3D();
            private bool _isCorrectingSeaLevelElevationUpwards = false;
            private bool _isCorrectingSeaLevelElevationDownwards = false;
            private bool _isCorrectingSurfaceElevation = false;

            private TimeSpan _timeRunning = new TimeSpan();

            public override void update(MyContext context)
            {
                if (!context.FoundAllBlocks || !context._isInAtmosphere)
                {
                    context.transition(MyContext.Stopped);
                }

                TimeSpan timeSinceLast = context.Program.Runtime.TimeSinceLastRun;

                Vector3D directionForward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                Vector3D directionBackward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward);
                Vector3D directionUp = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                Vector3D directionRight = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Right);

                Vector3D v = context._velocityTracker.Velocity;

                double desiredSpeed = context._desiredSpeed;

                // Project direction vector onto plane tangent to current position on planet
                Vector3D planetTangentDesiredDirection = directionForward - directionForward.Dot(context._directionFromPlanetToMe) * context._directionFromPlanetToMe;
                // todo check that desired direction norm is within some bounds
                // otherwise this projection could be numerically unstable because
                // e.g. the nose is pointing straight down
                planetTangentDesiredDirection.Normalize();

                Vector3D desiredDirection = planetTangentDesiredDirection;
                Vector3D desiredGyroDirection = planetTangentDesiredDirection;
                // Hysteresis in the elevation correction to prevent cycling
                if (context._elevationFromSeaLevel >= context._startingElevation)
                {
                    _isCorrectingSeaLevelElevationUpwards = false;
                }
                if ((context._startingElevation - context._elevationFromSeaLevel) > 1)
                {
                    _isCorrectingSeaLevelElevationUpwards = true;
                }
                /*if (_isCorrectingSeaLevelElevationUpwards)
                {
                    // Vector upwards to correct any droop greater than a threshold
                    desiredDirection += 0.001 * context._directionFromPlanetToMe;
                    desiredDirection.Normalize();
                    desiredGyroDirection = desiredDirection;
                }*/

                double minElevationFromSurface = 1000;
                if (context._elevationFromSurface > minElevationFromSurface)
                {
                    _isCorrectingSurfaceElevation = false;
                }
                if ((minElevationFromSurface - context._elevationFromSurface) > 1)
                {
                    _isCorrectingSurfaceElevation = true;
                }
                if (_isCorrectingSurfaceElevation || _isCorrectingSeaLevelElevationUpwards)
                {
                    // Vector upwards to avoid hitting ground in mountainous regions
                    double maxElevationError = 100;
                    double surfaceElevationError = minElevationFromSurface - context._elevationFromSurface;
                    double seaLevelElevationError = context._startingElevation - context._elevationFromSeaLevel;
                    double elevationError = Math.Max(surfaceElevationError, seaLevelElevationError);
                    elevationError = Math.Min(elevationError, maxElevationError);
                    desiredDirection += 0.001 * elevationError * context._directionFromPlanetToMe;
                    desiredDirection.Normalize();

                    desiredGyroDirection += 0.001 * elevationError * context._directionFromPlanetToMe;
                    desiredGyroDirection.Normalize();

                    // Maintain the new altitude
                    if (_isCorrectingSurfaceElevation && context._elevationFromSeaLevel > context._startingElevation)
                    {
                        context._startingElevation = context._elevationFromSeaLevel;
                    }
                }


                if (context._elevationFromSeaLevel <= context._startingElevation)
                {
                    _isCorrectingSeaLevelElevationDownwards = false;
                }
                if ((context._elevationFromSeaLevel - context._startingElevation) > 100)
                {
                    _isCorrectingSeaLevelElevationDownwards = true;
                }
                if (_isCorrectingSeaLevelElevationDownwards && !_isCorrectingSurfaceElevation)
                {
                    // Vector upwards to correct any droop greater than a threshold
                    desiredDirection += -0.01 * context._directionFromPlanetToMe;
                    desiredDirection.Normalize();

                    desiredGyroDirection += -0.01 * context._directionFromPlanetToMe;
                    desiredGyroDirection.Normalize();
                }

                Vector3D desiredV = desiredDirection * desiredSpeed;

                Vector3D position = context.Program.Me.CubeGrid.GetPosition();
                Vector3D desiredPositionDueToVelocity = _lastPosition + desiredV * timeSinceLast.TotalSeconds;
                _lastPosition = position;

                Vector3D desiredPosition = desiredPositionDueToVelocity;

                double fwdControl = updateDirectionalController(
                    context,
                    _fwdController,
                    directionForward,
                    desiredPosition,
                    position,
                    timeSinceLast
                );

                double vertControl = updateDirectionalController(
                    context,
                    _vertController,
                    directionUp,
                    desiredPosition,
                    position,
                    timeSinceLast
                );

                double horzControl = updateDirectionalController(
                    context,
                    _horzController,
                    directionRight,
                    desiredPosition,
                    position,
                    timeSinceLast
                );

                double pitchError = signedAngleBetweenNormalizedVectors(directionForward, desiredGyroDirection, directionRight);
                double pitchControl = _pitchController.update(timeSinceLast, pitchError, pitchError, context.Program);

                Vector3D planetTangentRollDirection = planetTangentDesiredDirection.Cross(context._directionFromPlanetToMe);
                double rollError = signedAngleBetweenNormalizedVectors(directionRight, planetTangentRollDirection, directionForward);
                double rollControl = _rollController.update(timeSinceLast, rollError, rollError, context.Program);

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
                        displayController(context, _rollController, "Roll");
                        break;
                }

                float yaw = context._cockpits[0].RotationIndicator.Y / 30;

                Vector3 moveIndicator = context._cockpits[0].MoveIndicator;
                float moveVert = moveIndicator.Y;
                float moveForward = moveIndicator.Z;
                if (moveVert > 0.1)
                {
                    context._startingElevation = context._elevationFromSeaLevel;
                    vertControl = 1;
                }
                else if (moveVert < -0.1)
                {
                    context._startingElevation = context._elevationFromSeaLevel;
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
                _rotationVector[1] = yaw;
                _rotationVector[2] = (float)-rollControl / 2;
                _rotationVector[3] = -_rotationVector[0];
                _rotationVector[4] = -_rotationVector[1];
                _rotationVector[5] = -_rotationVector[2];

                foreach (Gyroscope gyro in context._compensatedGyros)
                {
                    gyro.SetRotation(_rotationVector);
                }

                context.setThrustOverrideRatio(context._fwdThrust, fwdControl);
                context.setThrustOverrideRatio(context._revThrust, -fwdControl);

                _timeRunning += timeSinceLast;
                if (_timeRunning.TotalSeconds > 5 && 
                    (_isCorrectingSeaLevelElevationUpwards || _isCorrectingSurfaceElevation || _isCorrectingSeaLevelElevationDownwards))
                {
                    context.setThrustOverrideRatio(context._upThrust, vertControl);
                    context.setThrustOverrideRatio(context._downThrust, -vertControl);
                }
                else
                {
                    // Let the dampers do it because the buggy ass piece of shit game is 
                    // able to do it with less power when it uses its own dampers. Asshole
                    context.resetThrustOverrideRatio(context._upThrust);
                    context.resetThrustOverrideRatio(context._downThrust);
                }
                
                context.setThrustOverrideRatio(context._rightThrust, horzControl);
                context.setThrustOverrideRatio(context._leftThrust, -horzControl);
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
                double control = controller.update(timeSinceLastUpdate, error, position, context.Program);
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

                if (!context.FoundAllBlocks || !context._isInAtmosphere)
                {
                    context.transition(MyContext.Stopped);
                }

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;

               // context._cockpits[0].DampenersOverride = false;

               
                _fwdController.reset();
                _vertController.reset();
                _horzController.reset();
                _pitchController.reset();
                _rollController.reset();

                _isCorrectingSeaLevelElevationUpwards = false;
                _isCorrectingSeaLevelElevationDownwards = false;
                _isCorrectingSurfaceElevation = false;
                _lastPosition = context.Program.Me.CubeGrid.GetPosition();

                _timeRunning = new TimeSpan();

                if (!context._cockpits[0].TryGetPlanetElevation(MyPlanetElevation.Sealevel, out context._startingElevation))
                {
                    context.transition(MyContext.Stopped);
                    return;
                }
            }

            public override void leave(MyContext context)
            {
                base.leave(context);

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;

                if (context._cockpits.Count > 0)
                {
                    context._cockpits[0].DampenersOverride = true;
                }

                context.resetThrustOverrideRatio(context._fwdThrust);
                context.resetThrustOverrideRatio(context._revThrust);
                        
                context.resetThrustOverrideRatio(context._upThrust);
                context.resetThrustOverrideRatio(context._downThrust);
                        
                context.resetThrustOverrideRatio(context._leftThrust);
                context.resetThrustOverrideRatio(context._rightThrust);

                foreach (IMyGyro gyro in context._gyros)
                {
                    gyro.GyroOverride = false;
                }
            }
        }

        private class InitializeDesiredSpeedState : State<MyContext>
        {
            private VelocityTracker _velocityTracker = new VelocityTracker();

            public override void update(MyContext context)
            {
                if (!context.FoundAllBlocks)
                {
                    context.transition(MyContext.Stopped);
                    return;
                }

                bool wasInitialised = _velocityTracker.IsInitialized;
                _velocityTracker.update(context.Program.Me.CubeGrid.GetPosition(), context.Program.Runtime.TimeSinceLastRun);

                if (wasInitialised)
                {
                    Vector3D directionForward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                    context._desiredSpeed = _velocityTracker.Velocity.Dot(directionForward);
                    context._desiredSpeed = Math.Min(100, Math.Max(0, context._desiredSpeed));
                    context.transition(new CruiseControlState());
                }
            }

            public override void enter(MyContext context)
            {
                base.enter(context);

                context._desiredSpeed = 0;
                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;

                // todo
                //_velocityTracker.reset();
            }

            public override void leave(MyContext context)
            {
                base.leave(context);

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
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
            if (command == "cruise")
            {
                _impl.Context.log("Cruise control enabled");
                if (args.Length > 0)
                {
                    _impl.Context._desiredSpeed = Math.Min(Math.Max(float.Parse(args[0]), 0), 100.0);
                    _impl.Context.transition(new CruiseControlState());
                }
                else
                {
                    _impl.Context.transition(new InitializeDesiredSpeedState());
                }
            }
            else if (command == "cancel_cruise")
            {
                _impl.Context.log("Cruise control disabled");
                _impl.Context.transition(MyContext.Stopped);
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