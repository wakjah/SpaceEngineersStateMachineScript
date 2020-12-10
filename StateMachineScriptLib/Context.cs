using Sandbox.ModAPI.Ingame;
using System;

namespace IngameScript
{
    partial class Program
    {
        abstract class Context<ContextImpl> where ContextImpl : Context<ContextImpl>
        {
            private static readonly TimeSpan DISPLAY_UPDATE_INTERVAL = new TimeSpan(0, 0, 2);
            private static readonly TimeSpan BLOCK_UPDATE_INTERVAL = new TimeSpan(0, 0, 10);

            private StateMachineProgram<ContextImpl> _program;
            private State<ContextImpl> _state;
            private TimeSpan _timeSinceLastDisplayUpdate = new TimeSpan();
            private TimeSpan _timeSinceLastBlocksUpdate = new TimeSpan();
            public State<ContextImpl> State { get { return _state; } }
            private bool _initialised = false;
            private State<ContextImpl> _initialState;

            public Context(StateMachineProgram<ContextImpl> program, Options options, State<ContextImpl> initial)
            {
                _program = program;
                _initialState = initial;
                Options = options;

                loadOptions();

                // For initialisation, update on next tick
                program.Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            }

            public Options Options { get; }

            public bool FoundAllBlocks { get; private set; } = false;

            public MyGridProgram Program { get { return _program.Program; } }

            public StateMachineProgram<ContextImpl> StateMachineProgram { get { return _program; } }

            public void transition(State<ContextImpl> toState)
            {
                if (_state != toState)
                {
                    logInfo("Transition from state " + _state + " to state " + toState);

                    if (_state != null)
                    {
                        _state.leave((ContextImpl)this);
                    }

                    _state = toState;

                    _state.enter((ContextImpl)this);
                }
            }

            public void log(string msg)
            {
                _program.Echo(msg);
            }

            public void logInfo(string msg)
            {
                // Logging of 'info' messages disabled by default
                //log(msg);
            }

            public void logError(string msg)
            {
                log(msg);
            }

            public virtual void update(TimeSpan timeSinceLastUpdate)
            {
                if (!_initialised)
                {
                    Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    _initialised = true;

                    updateBlocks();
                    transition(_initialState);
                    updateDisplay();
                }

                maybeUpdateBlocks(timeSinceLastUpdate);
                _state.update((ContextImpl)this);
                maybeUpdateDisplay(timeSinceLastUpdate);
            }

            public void saveOptions()
            {
                _program.Storage = Options.getSaveString();
            }

            public void loadOptions()
            {
                Options.parse(_program.Storage);
            }

            private void maybeUpdateBlocks(TimeSpan timeSinceLastUpdate)
            {
                _timeSinceLastBlocksUpdate += timeSinceLastUpdate;

                if (_timeSinceLastBlocksUpdate > BLOCK_UPDATE_INTERVAL)
                {
                    updateBlocks();
                }
            }

            public void updateBlocks()
            {
                FoundAllBlocks = updateBlocksImpl();
                _timeSinceLastBlocksUpdate = new TimeSpan();
            }

            // Return true if all required blocks are found
            protected abstract bool updateBlocksImpl();

            private void maybeUpdateDisplay(TimeSpan timeSinceLastUpdate)
            {
                _timeSinceLastDisplayUpdate += timeSinceLastUpdate;

                if (_timeSinceLastDisplayUpdate > DISPLAY_UPDATE_INTERVAL)
                {
                    updateDisplay();
                }
            }

            public void updateDisplay()
            {
                updateDisplayImpl();
                _timeSinceLastDisplayUpdate = new TimeSpan();
            }

            protected abstract void updateDisplayImpl();
        }
    }
}