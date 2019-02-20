using System;
using System.Collections.Generic;
using Improbable.Gdk.Core;
using Unity.Entities;

namespace Improbable.Gdk.ReactiveComponents
{
    [DisableAutoCreation]
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(SpatialOSReceiveGroup.InternalSpatialOSReceiveGroup))]
    [UpdateAfter(typeof(SpatialOSReceiveSystem))]
    public class ReactiveCommandComponentSystem : ComponentSystem
    {
        private readonly List<IReactiveCommandComponentManager> managers = new List<IReactiveCommandComponentManager>();

        private EntityManager entityManager;
        private CommandSystem commandSystem;
        private WorkerSystem workerSystem;

        protected override void OnCreateManager()
        {
            base.OnCreateManager();

            entityManager = World.GetExistingManager<EntityManager>();
            commandSystem = World.GetExistingManager<CommandSystem>();
            workerSystem = World.GetExistingManager<WorkerSystem>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(IReactiveCommandComponentManager).IsAssignableFrom(type) || type.IsAbstract)
                    {
                        continue;
                    }

                    var instance = (IReactiveCommandComponentManager) Activator.CreateInstance(type);
                    managers.Add(instance);
                }
            }
        }

        protected override void OnDestroyManager()
        {
            foreach (var manager in managers)
            {
                manager.Clean(World);
            }

            base.OnDestroyManager();
        }

        protected override void OnUpdate()
        {
            foreach (var manager in managers)
            {
                manager.PopulateReactiveCommandComponents(commandSystem, entityManager, workerSystem, World);
            }
        }
    }
}