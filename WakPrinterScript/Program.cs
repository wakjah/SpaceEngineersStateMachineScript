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

        class DeferredBlockIterator
        {
            public HashSet<IMyCubeGrid> _grids = new HashSet<IMyCubeGrid>();
            public List<IMyCubeGrid> _gridsList = new List<IMyCubeGrid>(16);
            public List<IMyMechanicalConnectionBlock> _temp = new List<IMyMechanicalConnectionBlock>(16);
            public List<IMySlimBlock> _blocks;
            public int _blocksPerUpdate;
            public int _currentGridIndex;
            public Vector3I _min = new Vector3I();
            public Vector3I _max = new Vector3I();
            public Vector3I _cur = new Vector3I();

            public DeferredBlockIterator(int blocksPerUpdate)
            {
                _blocksPerUpdate = blocksPerUpdate;
                _blocks = new List<IMySlimBlock>(_blocksPerUpdate);
            }

            public void begin(IMyCubeGrid grid, IMyGridTerminalSystem terminal)
            {
                _grids.Clear();
                GridUtils.findAllGrids(grid, _grids, _temp, terminal);

                _gridsList.Clear();
                foreach (IMyCubeGrid g in _grids)
                {
                    _gridsList.Add(g);
                }

                setCurrentGrid(0);
            }

            private void setCurrentGrid(int i)
            {
                _currentGridIndex = i;

                if (i >= _gridsList.Count)
                {
                    return;
                }

                IMyCubeGrid grid = _gridsList[i];
                copyVector3I(grid.Min, ref _min);
                copyVector3I(grid.Max, ref _max);
                copyVector3I(_min, ref _cur);
            }

            private static void copyVector3I(Vector3I src, ref Vector3I dst)
            {
                dst.X = src.X;
                dst.Y = src.Y;
                dst.Z = src.Z;
            }

            public List<IMySlimBlock> next()
            {
                if (_currentGridIndex >= _gridsList.Count)
                {
                    return null;
                }

                _blocks.Clear();

                for (int i = 0; i < _blocksPerUpdate; ++i)
                {
                    IMySlimBlock block = _gridsList[_currentGridIndex].GetCubeBlock(_cur);
                    if (block != null)
                    {
                        _blocks.Add(block);
                    }

                    _cur.X += 1;
                    if (_cur.X > _max.X)
                    {
                        _cur.X = _min.X;
                        _cur.Y += 1;
                    }

                    if (_cur.Y >= _max.Y)
                    {
                        _cur.X = _min.X;
                        _cur.Y = _min.Y;
                        _cur.Z += 1;
                    }

                    if (_cur.Z >= _max.Z)
                    {
                        setCurrentGrid(_currentGridIndex + 1);
                        if (_currentGridIndex >= _gridsList.Count)
                        {
                            break;
                        }
                    }
                }

                return _blocks;
            }
        }

        class MyContext : Context<MyContext>
        {
            public static readonly StoppedState Stopped = new StoppedState();

            public List<IMyTextPanel> _textPanels = new List<IMyTextPanel>(1);
            private DeferredBlockIterator _blockIterator = new DeferredBlockIterator(200);
            private int _blocksCount = 0;
            private int _pendingBlocksCount = 0;

            public MyContext(StateMachineProgram<MyContext> program) : base(program, new MyOptions(), Stopped)
            {
                
            }

            public override void update(TimeSpan timeSinceLastUpdate)
            {
                List<IMySlimBlock> blocks = _blockIterator.next();
                if (blocks == null)
                {
                    _blockIterator.begin(Program.Me.CubeGrid, Program.GridTerminalSystem);
                    _blocksCount = _pendingBlocksCount;
                    _pendingBlocksCount = 0;
                }
                else
                {
                    _pendingBlocksCount += blocks.Count();
                }

                Program.Echo("blocks: " + blocks);

                base.update(timeSinceLastUpdate);
            }

            protected override bool updateBlocksImpl()
            {
                log("Update blocks");

                Program.GridTerminalSystem.GetBlocksOfType(_textPanels, b => b.CustomName.Contains("Printer"));

                return true;
            }

            protected override void updateDisplayImpl()
            {
                HashSet<IMyCubeGrid> grids = new HashSet<IMyCubeGrid>();
                List<IMyMechanicalConnectionBlock> temp = new List<IMyMechanicalConnectionBlock>(16);
                GridUtils.findAllGrids(Program.Me.CubeGrid, grids, temp, Program.GridTerminalSystem);

                string s = "BLOCKS: " + _blocksCount + " (pending: " + _pendingBlocksCount + ")\nGrids: " + _blockIterator._grids.Count + "\n"
                    + "g=" + _blockIterator._currentGridIndex + "; cur=" + _blockIterator._cur;

                s += "\n";
                foreach (IMyCubeGrid g in grids)
                {
                    s += g + ":\n" + "    Min=" + g.Min + "\n" + "    Max=" + g.Max + "\n";
                }

                foreach (IMyTextPanel panel in _textPanels)
                {
                    panel.WriteText(s);
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
            if (command == "start")
            {
                _impl.Context.log("START");
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
