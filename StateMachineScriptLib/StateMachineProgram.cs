using Sandbox.ModAPI.Ingame;
using System;

namespace IngameScript
{
    partial class Program
    {
        class StateMachineProgram<ContextImpl> where ContextImpl : Context<ContextImpl>
        {
            private ContextImpl _context;

            public MyGridProgram Program { get; }
            private Action<string> _storageSetter;
            private Action<string, string[]> _trigger;

            public StateMachineProgram(
                MyGridProgram program,
                Action<string, string[]> trigger,
                Action<string> storageSetter
            )
            {
                Program = program;
                _storageSetter = storageSetter;
                _trigger = trigger;

                Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
            }

            public void init(ContextImpl context)
            {
                if (_context != null)
                {
                    throw new Exception("Can only init once");
                }

                _context = context;
            }

            public void Save()
            {
                _context.saveOptions();
            }

            public void Main(string argument, UpdateType updateSource)
            {
                if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
                {
                    trigger(argument);
                }

                if ((updateSource & (UpdateType.Update100 | UpdateType.Update10 | UpdateType.Update1)) != 0)
                {
                    update(Program.Runtime.TimeSinceLastRun);
                }
            }

            private void trigger(string argument)
            {
                string[] parts = argument.Split(' ');

                if (parts.Length == 0)
                {
                    _context.logError("Missing command");
                    return;
                }

                string command = parts[0];
                _trigger.Invoke(command, SubArray(parts, 1, parts.Length - 1));

            }

            private static T[] SubArray<T>(T[] data, int index, int length)
            {
                T[] result = new T[length];
                Array.Copy(data, index, result, 0, length);
                return result;
            }


            private void update(TimeSpan timeSinceLastUpdate)
            {
                _context.update(timeSinceLastUpdate);
            }

            public ContextImpl Context { get { return _context; } }

            public string Storage { get { return Program.Storage; } set { _storageSetter.Invoke(value); } }

            public void Echo(string s)
            {
                Program.Echo(s);
            }
        }
    }
}