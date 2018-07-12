﻿using System;
using System.Collections;
using System.Collections.Generic;
using EcsRx.Blueprints;
using EcsRx.Entities;
using EcsRx.Events;
using EcsRx.Exceptions;
using EcsRx.Extensions;
using EcsRx.Polyfills;

namespace EcsRx.Collections
{
    public class EntityCollection : IEntityCollection, IDisposable
    {
        public readonly IDictionary<Guid, IEntity> EntityLookup;
        public readonly IDictionary<Guid, IDisposable> EntitySubscriptions;

        public IObservable<CollectionEntityEvent> EntityAdded => _onEntityAdded;
        public IObservable<CollectionEntityEvent> EntityRemoved => _onEntityRemoved;
        public IObservable<ComponentsChangedEvent> EntityComponentsAdded => _onEntityComponentsAdded;
        public IObservable<ComponentsChangedEvent> EntityComponentsRemoving => _onEntityComponentsRemoving;
        public IObservable<ComponentsChangedEvent> EntityComponentsRemoved => _onEntityComponentsRemoved;
        
        public string Name { get; }
        public IEntityFactory EntityFactory { get; }

        private readonly Subject<CollectionEntityEvent> _onEntityAdded;
        private readonly Subject<CollectionEntityEvent> _onEntityRemoved;
        private readonly Subject<ComponentsChangedEvent> _onEntityComponentsAdded;
        private readonly Subject<ComponentsChangedEvent> _onEntityComponentsRemoving;
        private readonly Subject<ComponentsChangedEvent> _onEntityComponentsRemoved;
        
        public EntityCollection(string name, IEntityFactory entityFactory)
        {
            EntityLookup = new Dictionary<Guid, IEntity>();
            EntitySubscriptions = new Dictionary<Guid, IDisposable>();
            Name = name;
            EntityFactory = entityFactory;

            _onEntityAdded = new Subject<CollectionEntityEvent>();
            _onEntityRemoved = new Subject<CollectionEntityEvent>();
            _onEntityComponentsAdded = new Subject<ComponentsChangedEvent>();
            _onEntityComponentsRemoving = new Subject<ComponentsChangedEvent>();
            _onEntityComponentsRemoved = new Subject<ComponentsChangedEvent>();
        }

        public void SubscribeToEntity(IEntity entity)
        {
            var entityDisposable = new CompositeDisposable();
            entity.ComponentsAdded.Subscribe(x => _onEntityComponentsAdded.OnNext(new ComponentsChangedEvent(this, entity, x))).AddTo(entityDisposable);
            entity.ComponentsRemoving.Subscribe(x => _onEntityComponentsRemoving.OnNext(new ComponentsChangedEvent(this, entity, x))).AddTo(entityDisposable);
            entity.ComponentsRemoved.Subscribe(x => _onEntityComponentsRemoved.OnNext(new ComponentsChangedEvent(this, entity, x))).AddTo(entityDisposable);
            EntitySubscriptions.Add(entity.Id, entityDisposable);
        }

        public void UnsubscribeFromEntity(Guid entityId)
        { EntitySubscriptions.RemoveAndDispose(entityId); }
        
        public IEntity CreateEntity(IBlueprint blueprint = null)
        {
            var entity = EntityFactory.Create(null);

            EntityLookup.Add(entity.Id, entity);
            _onEntityAdded.OnNext(new CollectionEntityEvent(entity, this));
            SubscribeToEntity(entity);
           
            blueprint?.Apply(entity);

            return entity;
        }

        public IEntity GetEntity(Guid id)
        { return EntityLookup[id]; }

        public void RemoveEntity(Guid id, bool disposeOnRemoval = true)
        {
            var entity = GetEntity(id);
            EntityLookup.Remove(id);

            var entityId = entity.Id;

            if(disposeOnRemoval)
            { entity.Dispose(); }
            
            UnsubscribeFromEntity(entityId);
            _onEntityRemoved.OnNext(new CollectionEntityEvent(entity, this));
        }

        public void AddEntity(IEntity entity)
        {
            if(entity.Id == Guid.Empty)
            { throw new InvalidEntityException("Entity provided does not have an assigned Id"); }

            EntityLookup.Add(entity.Id, entity);
            _onEntityAdded.OnNext(new CollectionEntityEvent(entity, this));
            SubscribeToEntity(entity);
        }

        public bool ContainsEntity(Guid id)
        { return EntityLookup.ContainsKey(id); }

        public IEnumerator<IEntity> GetEnumerator()
        { return EntityLookup.Values.GetEnumerator(); }

        IEnumerator IEnumerable.GetEnumerator()
        { return GetEnumerator(); }

        public void Dispose()
        {
            _onEntityAdded.Dispose();
            _onEntityRemoved.Dispose();
            _onEntityComponentsAdded.Dispose();
            _onEntityComponentsRemoving.Dispose();
            _onEntityComponentsRemoved.Dispose();

            EntityLookup.Clear();
            EntitySubscriptions.RemoveAndDisposeAll();
        }
    }
}
