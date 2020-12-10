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
            public List<IMyShipDrill> _drills = new List<IMyShipDrill>(16);
            private List<IMyCargoContainer> _cargoContainers = new List<IMyCargoContainer>(16);
            public List<IMyInventory> _inventories = new List<IMyInventory>(16);
            public CruiseDebug _debug = CruiseDebug.None;
            public VelocityTracker _velocityTracker = new VelocityTracker();
            private StringBuilder _stringBuilder = new StringBuilder();
            public double _desiredSpeed = 0.0;
            public Vector3D _startingDirectionForward = new Vector3D();
            public Vector3D _startingDirectionUp = new Vector3D();
            public Vector3D _startingDirectionRight = new Vector3D();
            public double _inventoryFilledRatio = 0.0;
            public Vector3D _cubeGridPosition = new Vector3D();
            public double _desiredRollSpeed_radsPerSec = 0;
            public Quaternion _startingRotation = new Quaternion();

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {

            }

            public void setDesiredSpeed(double speed, double rollSpeed)
            {
                _desiredSpeed = speed;
                _desiredRollSpeed_radsPerSec = rollSpeed;
            }

            public override void update(TimeSpan timeSinceLastUpdate)
            {
                if (!FoundAllBlocks)
                {
                    _cubeGridPosition = new Vector3D();
                    _velocityTracker.reset();
                }
                else
                {
                    _cubeGridPosition = _cockpits[0].CenterOfMass;
                    _velocityTracker.update(_cubeGridPosition, timeSinceLastUpdate);
                }
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

                _drills.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_drills);

                _cargoContainers.Clear();
                Program.GridTerminalSystem.GetBlocksOfType(_cargoContainers);

                foreach (IMyCargoContainer cargo in _cargoContainers)
                {
                    for (int i = 0; i < cargo.InventoryCount; ++i)
                    {
                        _inventories.Add(cargo.GetInventory(i));
                    }
                }

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

            public void updateInventoryFilled()
            {
                long maxVolume = 0;
                long currentVolume = 0;
                foreach (IMyInventory inventory in _inventories)
                {
                    maxVolume += inventory.MaxVolume.RawValue;
                    currentVolume += inventory.CurrentVolume.RawValue;
                }

                _inventoryFilledRatio = currentVolume / (double) maxVolume;
            }

            protected override void updateDisplayImpl()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("Mining Cruise\n");
                if (!FoundAllBlocks)
                {
                    _stringBuilder.Append("ERROR: No cockpit found\n");
                }

                bool isCruising = State is CruiseControlState;
                _stringBuilder.Append("Status: ");
                if (!FoundAllBlocks)
                {
                    _stringBuilder.Append("Unavailable");
                }
                else
                {
                    _stringBuilder.Append(isCruising ? "Cruising" : "Off");
                }
                _stringBuilder.Append("\n");

                updateInventoryFilled();
                _stringBuilder.Append(string.Format("Inventory: {0:0.0} %\n", _inventoryFilledRatio * 100));

                string text = _stringBuilder.ToString();

                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.ContentType = ContentType.TEXT_AND_IMAGE;
                    panel.WriteText(text);
                }

                foreach (IMyCockpit cockpit in _cockpits)
                {
                    IMyTextSurface surface = cockpit.GetSurface(0);
                    surface.ContentType = ContentType.TEXT_AND_IMAGE;
                    surface.WriteText(text);
                }
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

                foreach (IMyCockpit cockpit in _cockpits)
                {
                    IMyTextSurface surface = cockpit.GetSurface(1);
                    surface.ContentType = ContentType.TEXT_AND_IMAGE;
                    surface.WriteText(text, append);
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

            public void reset()
            {
                _vMeasLastPosition = new Vector3D();
                Velocity = new Vector3D();
                _velocityInitialised = false;
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

        class WaitForInventoryToDropState : State<MyContext>
        {
            private CruiseControlState _nextState;

            public WaitForInventoryToDropState(CruiseControlState nextState)
            {
                _nextState = nextState;
            }

            public override void update(MyContext context)
            {
                if (context._inventoryFilledRatio < 0.9)
                {
                    context.transition(_nextState);
                }
            }
        }

        class CruiseControlState : State<MyContext>
        {
            private static double IMIN = -0.25;
            private static double IMAX = 0.25;

           /* private static double P_VERT = 100;
            private static double I_VERT = 10;
            private static double D_VERT = 0.0;*/

            private static double P_FWD = 30;
            // We can't use integral control on this one because it's unable to 
            // overshoot, meaning the integrator will just get stuck at the max
            private static double I_FWD = 0.0;
            private static double D_FWD = 0.0001;

            private static double P_VERT = P_FWD;
            private static double I_VERT = I_FWD;
            private static double D_VERT = D_FWD;

            private static double P_HORZ = P_FWD;
            private static double I_HORZ = I_FWD;
            private static double D_HORZ = D_FWD;

            private static double P_GYRO = 4;
            private static double I_GYRO = 0.01;
            private static double D_GYRO = 0.02;
            private static double IMIN_GYRO = -1;
            private static double IMAX_GYRO = 1;

            private PidController _fwdController = new PidController(P_FWD, I_FWD, D_FWD, IMIN, IMAX);
            private PidController _vertController = new PidController(P_VERT, I_VERT, D_VERT, IMIN, IMAX);
            private PidController _horzController = new PidController(P_HORZ, I_HORZ, D_HORZ, IMIN, IMAX);
            private PidController _pitchController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);
            private PidController _rollController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);
            private PidController _yawController = new PidController(P_GYRO, I_GYRO, D_GYRO, IMIN_GYRO, IMAX_GYRO);

            private float[] _rotationVector = new float[6];

            private Vector3D _lastPosition = new Vector3D();

            private TimeSpan _timeRunning = new TimeSpan();

            private WaitForInventoryToDropState _waitForInventoryToDrop;

            public CruiseControlState()
            {
                _waitForInventoryToDrop = new WaitForInventoryToDropState(this);
            }

            public override void update(MyContext context)
            {
                if (!context.FoundAllBlocks)
                {
                    context.transition(MyContext.Stopped);
                    return;
                }

                if (context._inventoryFilledRatio > 0.99)
                {
                    context.transition(_waitForInventoryToDrop);
                    return;
                }

                TimeSpan timeSinceLast = context.Program.Runtime.TimeSinceLastRun;

                Vector3D directionForward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                Vector3D directionBackward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Backward);
                Vector3D directionUp = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                Vector3D directionRight = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Right);

                Vector3D v = context._velocityTracker.Velocity;

                double desiredSpeed = context._desiredSpeed;
                
                Vector3D desiredDirection = context._startingDirectionForward;
                
                Vector3D desiredV = desiredDirection * desiredSpeed;

                Vector3D position = context._cubeGridPosition;
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

                Matrix3x3 rotationMatrix = context.Program.Me.CubeGrid.WorldMatrix.Rotation;
                Quaternion rotation = new Quaternion();
                Quaternion.CreateFromRotationMatrix(ref rotationMatrix, out rotation);

                Quaternion q1 = context._startingRotation;
                q1.Conjugate();
                q1.Normalize();
                Quaternion diff = rotation * q1;

                Matrix3x3 xxx = Matrix3x3.CreateFromQuaternion(diff);
                xxx.

                float pitchError, rollError, yawError;
                diff.
                diff.GetAxisAngle(directionRight, out pitchError);
                diff

                /*Vector3D desiredUpDirectionForRoll = context._startingDirectionUp;
                Vector3D desiredRightDirectionForRoll = context._startingDirectionRight;
                if (context._desiredRollSpeed_radsPerSec != 0)
                {
                    double rollAmount = context._desiredRollSpeed_radsPerSec * _timeRunning.TotalSeconds;
                    context._debug = CruiseDebug.CustomTest;
                    context.displayDebugText("" + rollAmount * 180 / Math.PI + "\n");

                    //MatrixD rotation = MatrixD.CreateFromYawPitchRoll(0, rollAmount, 0);
                    MatrixD rotation = rodriguesRotation(context._startingDirectionForward, rollAmount);
                    desiredUpDirectionForRoll = Vector3D.Transform(desiredUpDirectionForRoll, rotation);
                    desiredRightDirectionForRoll = Vector3D.Transform(desiredRightDirectionForRoll, rotation);
                    desiredUpDirectionForRoll.Normalize();
                    desiredRightDirectionForRoll.Normalize();
                }


                // Project current forward direction onto plane specified by initial forward direction
                // This gives us a vector in the yaw direction of the current forward but at 0 pitch 
                // error from original
                // Then normalize and take the signed angle distance to get our current pitch error
                // Similar procedures used for yaw / roll
                double pitchError;
                {
                    Vector3D projected = directionForward - directionForward.Dot(desiredUpDirectionForRoll) * desiredUpDirectionForRoll;
                    projected.Normalize();
                    pitchError = signedAngleBetweenNormalizedVectors(directionForward, projected, directionRight);
                }
                double pitchControl = _pitchController.update(timeSinceLast, pitchError, pitchError);
                if (Math.Abs(pitchError) < 1e-5)
                {
                    pitchControl = 0;
                }

                double yawError;
                {
                    Vector3D projected = directionForward - directionForward.Dot(desiredRightDirectionForRoll) * desiredRightDirectionForRoll;
                    projected.Normalize();
                    yawError = signedAngleBetweenNormalizedVectors(directionForward, projected, directionUp);
                }
                double yawControl = _rollController.update(timeSinceLast, yawError, yawError);

                double rollError;
                {
                    Vector3D projected = directionRight - directionRight.Dot(desiredUpDirectionForRoll) * desiredUpDirectionForRoll;
                    projected.Normalize();

                    rollError = signedAngleBetweenNormalizedVectors(directionRight, projected, directionForward);
                }
                double rollControl = _rollController.update(timeSinceLast, rollError, rollError);*/

                
                float gyroPower = 1;
                if (Math.Abs(pitchError) < 1e-2 && Math.Abs(rollError) < 1e-2 && Math.Abs(yawControl) < 1e-2)
                {
                    pitchControl *= 2;
                    rollControl *= 2;
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
                        displayController(context, _rollController, "Roll");
                        break;
                    case CruiseDebug.Yaw:
                        displayController(context, _yawController, "Yaw");
                        break;
                }

                

                float yaw = context._cockpits[0].RotationIndicator.Y / 30;
                float pitch = context._cockpits[0].RotationIndicator.X / 30;
                float roll = context._cockpits[0].RollIndicator / 30;
                if (yaw != 0)
                {
                    yawControl = yaw;
                    gyroPower = 1;
                }

                if (pitch != 0)
                {
                    pitchControl = pitch;
                    gyroPower = 1;
                }

                if (roll != 0)
                {
                    rollControl = roll;
                    gyroPower = 1;
                }

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
                _rotationVector[2] = (float)-rollControl / 2;
                _rotationVector[3] = -_rotationVector[0];
                _rotationVector[4] = -_rotationVector[1];
                _rotationVector[5] = -_rotationVector[2];

                _timeRunning += timeSinceLast;

                foreach (Gyroscope gyro in context._compensatedGyros)
                {
                    gyro.SetRotation(_rotationVector, gyroPower);
                }

                /*context.setThrustOverrideRatio(context._fwdThrust, fwdControl);
                context.setThrustOverrideRatio(context._revThrust, -fwdControl);

                context.setThrustOverrideRatio(context._upThrust, vertControl);
                context.setThrustOverrideRatio(context._downThrust, -vertControl);

                context.setThrustOverrideRatio(context._rightThrust, horzControl);
                context.setThrustOverrideRatio(context._leftThrust, -horzControl);*/
            }

            private static MatrixD rodriguesRotation(Vector3D axis, double rotation)
            {
                MatrixD W = new MatrixD(
                    0, -axis.Z, axis.Y,
                    axis.Z, 0, -axis.X,
                    -axis.Y, axis.X, 0
                );
                double cosineTerm = Math.Sin(rotation / 2);
                return MatrixD.Identity + Math.Sin(rotation) * W + 2 * cosineTerm * cosineTerm * (W * W);
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
                context.updateInventoryFilled();

                if (!context.FoundAllBlocks)
                {
                    context.transition(MyContext.Stopped);
                }

                context.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;

                context._cockpits[0].DampenersOverride = false;

               
                _fwdController.reset();
                _vertController.reset();
                _horzController.reset();
                _pitchController.reset();
                _rollController.reset();
                _yawController.reset();

                Vector3D directionForward = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
                Vector3D directionUp = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up);
                Vector3D directionRight = context._cockpits[0].WorldMatrix.GetDirectionVector(Base6Directions.Direction.Right);
                context._startingDirectionForward = directionForward;
                context._startingDirectionUp = directionUp;
                context._startingDirectionRight = directionRight;

                Matrix3x3 rotationMatrix = context.Program.Me.CubeGrid.WorldMatrix.Rotation;
                Quaternion.CreateFromRotationMatrix(ref rotationMatrix, out context._startingRotation);

                _lastPosition = context._cubeGridPosition;

                _timeRunning = new TimeSpan();

                foreach (IMyShipDrill drill in context._drills)
                {
                    drill.Enabled = true;
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
                    gyro.GyroPower = 1;
                }

                foreach (IMyShipDrill drill in context._drills)
                {
                    drill.Enabled = false;
                }
            }
        }

        /*private class InitializeDesiredSpeedState : State<MyContext>
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
        }*/



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
                double desiredSpeed = Math.Min(Math.Max(float.Parse(args[0]), 0), 100.0);
                double desiredRollSpeed_rpm = 0;
                if (args.Length > 1)
                {
                    desiredRollSpeed_rpm = float.Parse(args[1]);
                }
                double desiredRollSpeed_radsPerSec = desiredRollSpeed_rpm / 60 * 2 * Math.PI;
                _impl.Context.setDesiredSpeed(desiredSpeed, desiredRollSpeed_radsPerSec);
                _impl.Context.transition(new CruiseControlState());
            }
            else if (command == "reverse")
            {
                _impl.Context.log("Cruise control enabled");
                if (args.Length > 0)
                {
                    _impl.Context.setDesiredSpeed(-Math.Min(Math.Max(float.Parse(args[0]), 0), 100.0), 0);
                    if (!(_impl.Context.State is CruiseControlState))
                    {
                        _impl.Context.transition(new CruiseControlState());
                    }
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