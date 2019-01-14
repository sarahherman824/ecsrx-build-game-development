using System;
using System.Collections.Generic;
using System.Linq;
using EcsRx.Attributes;
using EcsRx.Collections;
using EcsRx.Entities;
using EcsRx.Executor.Handlers;
using EcsRx.Extensions;
using EcsRx.Groups;
using EcsRx.MicroRx.Extensions;
using EcsRx.Plugins.ReactiveSystems.Extensions;
using EcsRx.Systems;
using EcsRx.Threading;

namespace EcsRx.Plugins.ReactiveSystems.Handlers
{
    [Priority(6)]
    public class ReactToGroupSystemHandler : IConventionalSystemHandler
    {
        public readonly IEntityCollectionManager _entityCollectionManager;       
        public readonly IDictionary<ISystem, IDisposable> _systemSubscriptions;
        public readonly IThreadHandler _threadHandler;
        
        public ReactToGroupSystemHandler(IEntityCollectionManager entityCollectionManager, IThreadHandler threadHandler)
        {
            _entityCollectionManager = entityCollectionManager;
            _threadHandler = threadHandler;
            _systemSubscriptions = new Dictionary<ISystem, IDisposable>();
        }

        public bool CanHandleSystem(ISystem system)
        { return system is IReactToGroupSystem; }

        public void SetupSystem(ISystem system)
        {
            var affinities = system.GetGroupAffinities();
            var observableGroup = _entityCollectionManager.GetObservableGroup(system.Group, affinities);
            var hasEntityPredicate = system.Group is IHasPredicate;
            var castSystem = (IReactToGroupSystem)system;
            var reactObservable = castSystem.ReactToGroup(observableGroup);
            var runParallel = system.ShouldMutliThread();
                
            if (!hasEntityPredicate)
            {
                var noPredicateSub = reactObservable.Subscribe(x => ExecuteForGroup(x, castSystem, runParallel));
                _systemSubscriptions.Add(system, noPredicateSub);
                return;
            }

            var groupPredicate = system.Group as IHasPredicate;
            var subscription = reactObservable.Subscribe(x => ExecuteForGroup(x.Where(groupPredicate.CanProcessEntity).ToList(), castSystem, runParallel));
            _systemSubscriptions.Add(system, subscription);
        }

        private void ExecuteForGroup(IReadOnlyList<IEntity> entities, IReactToGroupSystem castSystem, bool runParallel = false)
        {
            if (runParallel)
            {
                _threadHandler.For(0, entities.Count, i =>
                { castSystem.Process(entities[i]); });
                return;
            }
            
            for (var i = entities.Count - 1; i >= 0; i--)
            { castSystem.Process(entities[i]); }
        }

        public void DestroySystem(ISystem system)
        { _systemSubscriptions.RemoveAndDispose(system); }

        public void Dispose()
        {
            _systemSubscriptions.Values.DisposeAll();
            _systemSubscriptions.Clear();
        }
    }
}
