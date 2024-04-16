﻿using System.Collections;
using System.Timers;
using AllianceDM.Init;
using Rcl;
using Rcl.Logging;
namespace AllianceDM
{


    static class DecisionMaker
    {
        static Dictionary<uint, ComponentCell> Components = [];
        static List<List<Action>> UpdateFuncs = new();
        static uint PoolDim = 0;
        static Dictionary<string, GameObject> GameObjects = [];

        static readonly object _lock = new();
        static bool _lockWasTaken = false;
        static void Main(string[] args)
        {
            Ros2Def.context = new RclContext(args);
            Ros2Def.node = Ros2Def.context.CreateNode("decision_maker");

            InitGameObject.Init(ref GameObjects);

            InitComponent.Init(ref Components);

            double delay_time = 1000.0 / DecisionMakerDef.fps;

            StageConstruct();

            Awake();

            Ros2Def.node.Logger.LogInformation("Ready");
            System.Timers.Timer timer = new System.Timers.Timer
            {
                Enabled = true,
                Interval = delay_time //执行间隔时间,单位为毫秒; 这里实际间隔为10分钟  
            };
            timer.Elapsed += new ElapsedEventHandler(LifeCycle);
            timer.Start();
        }
        static void Awake()
        {
            foreach (var i in Components)
                i.Value.Awake();
        }

        static void LifeCycle(object? source, ElapsedEventArgs e)
        {
            if (_lockWasTaken)
            {
                Ros2Def.node.Logger.LogWarning("Warning: did not fix in fps : " + DecisionMakerDef.fps);
            }
            lock (_lock)
            {
                _lockWasTaken = true;
                InputUpdate();
                Update();
                OutputUpdate();
            }
            _lockWasTaken = false;
        }

        static void InputUpdate()
        {
            UpdateFuncs[0][0]();
        }

        static void Update()
        {
            List<Task> tasks = new();
            for (int i = 1; i < PoolDim; i++)
            {
                foreach (var a in UpdateFuncs[i])
                {
                    tasks.Add(Task.Run(a));
                }
                Task.WaitAll([.. tasks]);
            }
        }
        static void OutputUpdate()
        {

        }

        static void FindPath(ref ComponentCell cell, in Hashtable colored)
        {
            try
            {
                if (colored.ContainsKey(cell.ID))
                    throw new Exception("There is a loop,path is:");
                colored.Add(cell.ID, null);
                uint max = 1;
                for (var i = 0; i < cell.Forward.Count; i++)
                {
                    ComponentCell c = cell.Forward[i];
                    if (c.Dim == 0)
                        FindPath(ref c, in colored);
                    max = Math.Max(c.Dim, max);
                }
                cell.Dim = max;
                return;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message + "<-" + cell.ID.ToString());
            }
        }
        public static void StageConstruct()
        {
            var l = Components.Values.ToArray();

            for (int k = 0; k < l.Length; k++)
            {
                try
                {
                    foreach (var i in l[k].RecieveID)
                    {
                        if (i == 0)
                        {
                            Ros2Def.node.Logger.LogWarning("0 should not be inputID" + "@:" + l[k].ID.ToString());
                            continue;
                        }
                        l[k].Forward.Add(Components[i]);
                    }
                }
                catch (Exception e)
                {
                    Ros2Def.node.Logger.LogFatal(e.Message + "\twhen:Set ID:" + l[k].ID + " Forward node At Program.cs");
                    Environment.Exit(-1);
                }
            }
            for (int k = 0; k < l.Length; k++)
            {
                if (l[k].Dim != 0)
                    continue;
                Hashtable colored = [];
                FindPath(ref l[k], in colored);
                PoolDim = Math.Max(l[k].Dim, PoolDim);
            }
            PoolDim += 1;
            for (int i = 0; i < PoolDim; i++)
                UpdateFuncs.Add([]);
            Components[0].Dim = 0;
            foreach (var i in l)
                UpdateFuncs[(int)i.Dim].Add(i.Update);
        }

        //=====================

        public static GameObject FindObject(string name)
        {
            return GameObjects[name];
        }
        public static Component FindComponent(uint id)
        {
            return Components[id].Component;
        }
    }
}