using Sandbox.ModAPI.Ingame;
using System;

namespace IngameScript
{
    partial class Program
    {
        public class PidController
        {
            private double _lastError = 0;
            private double _integratorState = 0;
            private double _lastDerivative = 0;
            private double _integratorMax;
            private double _integratorMin;
            private double _pGain;
            private double _iGain;
            private double _dGain;
            private double _lastPTerm = 0;
            private double _lastITerm = 0;
            private double _lastDTerm = 0;
            private double _lastControl = 0;
            private double _lastPosition = 0;

            public PidController(double p, double i, double d, double iMin, double iMax)
            {
                _pGain = p;
                _iGain = i;
                _dGain = d;
                _integratorMin = iMin;
                _integratorMax = iMax;
            }

            public void reset()
            {
                _integratorState = 0;
                _lastPosition = 0;
            }

            public double update(TimeSpan timeSinceLastUpdate, double error, double position)
            {
                double p = _pGain * error;

                double seconds = timeSinceLastUpdate.TotalSeconds;
                _integratorState += error * seconds;
                _integratorState = Math.Min(Math.Max(_integratorState, _integratorMin), _integratorMax);
                double i = _integratorState * _iGain;

                double d = 0;
                if (seconds != 0)
                {
                    double derivative = (_lastError - error) / seconds;
                    d = _dGain * derivative;
                    _lastDerivative = derivative;
                }

                _lastPosition = position;
                _lastError = error;

                _lastPTerm = p;
                _lastITerm = i;
                _lastDTerm = d;

                _lastControl = p + i + d;
                return _lastControl;
            }

            public double getError()
            {
                return _lastError;
            }

            public double getPosition()
            {
                return _lastPosition;
            }

            public double getErrorIntegral()
            {
                return _integratorState;
            }

            public double getPositionDerivative()
            {
                return _lastDerivative;
            }

            public double getITerm()
            {
                return _lastITerm;
            }

            public double getDTerm()
            {
                return _lastDTerm;
            }

            public double getPTerm()
            {
                return _lastPTerm;
            }

            public double getControl()
            {
                return _lastControl;
            }
        }
    }
}
