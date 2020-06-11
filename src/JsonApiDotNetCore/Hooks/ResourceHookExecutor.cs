using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using LeftType = System.Type;
using RightType = System.Type;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Query;

namespace JsonApiDotNetCore.Hooks
{
    /// <inheritdoc/>
    internal sealed class ResourceHookExecutor : IResourceHookExecutor
    {
        private readonly IHookExecutorHelper _executorHelper;
        private readonly ITraversalHelper _traversalHelper;
        private readonly IIncludeService _includeService;
        private readonly ITargetedFields _targetedFields;
        private readonly IResourceGraph _resourceGraph;
        private readonly IResourceFactory _resourceFactory;

        public ResourceHookExecutor(
            IHookExecutorHelper executorHelper,
            ITraversalHelper traversalHelper,
            ITargetedFields targetedFields,
            IIncludeService includedRelationships,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory)
        {
            _executorHelper = executorHelper;
            _traversalHelper = traversalHelper;
            _targetedFields = targetedFields;
            _includeService = includedRelationships;
            _resourceGraph = resourceGraph;
            _resourceFactory = resourceFactory;
        }

        /// <inheritdoc/>
        public void BeforeRead<TResource>(ResourcePipeline pipeline, string stringId = null) where TResource : class, IIdentifiable
        {
            var hookContainer = _executorHelper.GetResourceHookContainer<TResource>(ResourceHook.BeforeRead);
            hookContainer?.BeforeRead(pipeline, false, stringId);
            var calledContainers = new List<LeftType> { typeof(TResource) };
            foreach (var chain in _includeService.Get())
                RecursiveBeforeRead(chain, pipeline, calledContainers);
        }

        /// <inheritdoc/>
        public IEnumerable<TResource> BeforeUpdate<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.BeforeUpdate, entities, out var container, out var node))
            {
                var relationships = node.RelationshipsToNextLayer.Select(p => p.Attribute).ToArray();
                var dbValues = LoadDbValues(typeof(TResource), (IEnumerable<TResource>)node.UniqueEntities, ResourceHook.BeforeUpdate, relationships);
                var diff = new DiffableEntityHashSet<TResource>(node.UniqueEntities, dbValues, node.LeftsToNextLayer(), _targetedFields);
                IEnumerable<TResource> updated = container.BeforeUpdate(diff, pipeline);
                node.UpdateUnique(updated);
                node.Reassign(_resourceFactory, entities);
            }

            FireNestedBeforeUpdateHooks(pipeline, _traversalHelper.CreateNextLayer(node));
            return entities;
        }

        /// <inheritdoc/>
        public IEnumerable<TResource> BeforeCreate<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.BeforeCreate, entities, out var container, out var node))
            {
                var affected = new EntityHashSet<TResource>((HashSet<TResource>)node.UniqueEntities, node.LeftsToNextLayer());
                IEnumerable<TResource> updated = container.BeforeCreate(affected, pipeline);
                node.UpdateUnique(updated);
                node.Reassign(_resourceFactory, entities);
            }
            FireNestedBeforeUpdateHooks(pipeline, _traversalHelper.CreateNextLayer(node));
            return entities;
        }

        /// <inheritdoc/>
        public IEnumerable<TResource> BeforeDelete<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.BeforeDelete, entities, out var container, out var node))
            {
                var relationships = node.RelationshipsToNextLayer.Select(p => p.Attribute).ToArray();
                var targetEntities = LoadDbValues(typeof(TResource), (IEnumerable<TResource>)node.UniqueEntities, ResourceHook.BeforeDelete, relationships) ?? node.UniqueEntities;
                var affected = new EntityHashSet<TResource>(targetEntities, node.LeftsToNextLayer());

                IEnumerable<TResource> updated = container.BeforeDelete(affected, pipeline);
                node.UpdateUnique(updated);
                node.Reassign(_resourceFactory, entities);
            }

            // If we're deleting an article, we're implicitly affected any owners related to it.
            // Here we're loading all relations onto the to-be-deleted article
            // if for that relation the BeforeImplicitUpdateHook is implemented,
            // and this hook is then executed
            foreach (var entry in node.LeftsToNextLayerByRelationships())
            {
                var rightType = entry.Key;
                var implicitTargets = entry.Value;
                FireForAffectedImplicits(rightType, implicitTargets, pipeline);
            }
            return entities;
        }

        /// <inheritdoc/>
        public IEnumerable<TResource> OnReturn<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.OnReturn, entities, out var container, out var node) && pipeline != ResourcePipeline.GetRelationship)
            {
                IEnumerable<TResource> updated = container.OnReturn((HashSet<TResource>)node.UniqueEntities, pipeline);
                ValidateHookResponse(updated);
                node.UpdateUnique(updated);
                node.Reassign(_resourceFactory, entities);
            }

            Traverse(_traversalHelper.CreateNextLayer(node), ResourceHook.OnReturn, (nextContainer, nextNode) =>
            {
                var filteredUniqueSet = CallHook(nextContainer, ResourceHook.OnReturn, new object[] { nextNode.UniqueEntities, pipeline });
                nextNode.UpdateUnique(filteredUniqueSet);
                nextNode.Reassign(_resourceFactory);
            });
            return entities;
        }

        /// <inheritdoc/>
        public void AfterRead<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterRead, entities, out var container, out var node))
            {
                container.AfterRead((HashSet<TResource>)node.UniqueEntities, pipeline);
            }

            Traverse(_traversalHelper.CreateNextLayer(node), ResourceHook.AfterRead, (nextContainer, nextNode) =>
            {
                CallHook(nextContainer, ResourceHook.AfterRead, new object[] { nextNode.UniqueEntities, pipeline, true });
            });
        }

        /// <inheritdoc/>
        public void AfterCreate<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterCreate, entities, out var container, out var node))
            {
                container.AfterCreate((HashSet<TResource>)node.UniqueEntities, pipeline);
            }

            Traverse(_traversalHelper.CreateNextLayer(node),
                ResourceHook.AfterUpdateRelationship,
                (nextContainer, nextNode) => FireAfterUpdateRelationship(nextContainer, nextNode, pipeline));
        }

        /// <inheritdoc/>
        public void AfterUpdate<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterUpdate, entities, out var container, out var node))
            {
                container.AfterUpdate((HashSet<TResource>)node.UniqueEntities, pipeline);
            }

            Traverse(_traversalHelper.CreateNextLayer(node),
                ResourceHook.AfterUpdateRelationship,
                (nextContainer, nextNode) => FireAfterUpdateRelationship(nextContainer, nextNode, pipeline));
        }

        /// <inheritdoc/>
        public void AfterDelete<TResource>(IEnumerable<TResource> entities, ResourcePipeline pipeline, bool succeeded) where TResource : class, IIdentifiable
        {
            if (GetHook(ResourceHook.AfterDelete, entities, out var container, out var node))
            {
                container.AfterDelete((HashSet<TResource>)node.UniqueEntities, pipeline, succeeded);
            }
        }

        /// <summary>
        /// For a given <see cref="ResourceHook"/> target and for a given type 
        /// <typeparamref name="TResource"/>, gets the hook container if the target
        /// hook was implemented and should be executed.
        /// <para />
        /// Along the way, creates a traversable node from the root entity set.
        /// </summary>
        /// <returns><c>true</c>, if hook was implemented, <c>false</c> otherwise.</returns>
        private bool GetHook<TResource>(ResourceHook target, IEnumerable<TResource> entities,
            out IResourceHookContainer<TResource> container,
            out RootNode<TResource> node) where TResource : class, IIdentifiable
        {
            node = _traversalHelper.CreateRootNode(entities);
            container = _executorHelper.GetResourceHookContainer<TResource>(target);
            return container != null;
        }

        /// <summary>
        /// Traverses the nodes in a <see cref="NodeLayer"/>.
        /// </summary>
        private void Traverse(NodeLayer currentLayer, ResourceHook target, Action<IResourceHookContainer, INode> action)
        {
            if (!currentLayer.AnyEntities()) return;
            foreach (INode node in currentLayer)
            {
                var entityType = node.ResourceType;
                var hookContainer = _executorHelper.GetResourceHookContainer(entityType, target);
                if (hookContainer == null) continue;
                action(hookContainer, node);
            }

            Traverse(_traversalHelper.CreateNextLayer(currentLayer.ToList()), target, action);
        }

        /// <summary>
        /// Recursively goes through the included relationships from JsonApiContext,
        /// translates them to the corresponding hook containers and fires the 
        /// BeforeRead hook (if implemented)
        /// </summary>
        private void RecursiveBeforeRead(List<RelationshipAttribute> relationshipChain, ResourcePipeline pipeline, List<LeftType> calledContainers)
        {
            var relationship = relationshipChain.First();
            if (!calledContainers.Contains(relationship.RightType))
            {
                calledContainers.Add(relationship.RightType);
                var container = _executorHelper.GetResourceHookContainer(relationship.RightType, ResourceHook.BeforeRead);
                if (container != null)
                    CallHook(container, ResourceHook.BeforeRead, new object[] { pipeline, true, null });
            }
            relationshipChain.RemoveAt(0);
            if (relationshipChain.Any())
                RecursiveBeforeRead(relationshipChain, pipeline, calledContainers);
        }

        /// <summary>
        /// Fires the nested before hooks for entities in the current <paramref name="layer"/>
        /// </summary>
        /// <remarks>
        /// For example: consider the case when the owner of article1 (one-to-one) 
        /// is being updated from owner_old to owner_new, where owner_new is currently already 
        /// related to article2. Then, the following nested hooks need to be fired in the following order. 
        /// First the BeforeUpdateRelationship should be for owner1, then the 
        /// BeforeImplicitUpdateRelationship hook should be fired for
        /// owner2, and lastly the BeforeImplicitUpdateRelationship for article2.</remarks>
        private void FireNestedBeforeUpdateHooks(ResourcePipeline pipeline, NodeLayer layer)
        {
            foreach (INode node in layer)
            {
                var nestedHookContainer = _executorHelper.GetResourceHookContainer(node.ResourceType, ResourceHook.BeforeUpdateRelationship);
                IEnumerable uniqueEntities = node.UniqueEntities;
                RightType entityType = node.ResourceType;
                Dictionary<RelationshipAttribute, IEnumerable> currentEntitiesGrouped;
                Dictionary<RelationshipAttribute, IEnumerable> currentEntitiesGroupedInverse;

                // fire the BeforeUpdateRelationship hook for owner_new
                if (nestedHookContainer != null)
                {
                    if (uniqueEntities.Cast<IIdentifiable>().Any())
                    {
                        var relationships = node.RelationshipsToNextLayer.Select(p => p.Attribute).ToArray();
                        var dbValues = LoadDbValues(entityType, uniqueEntities, ResourceHook.BeforeUpdateRelationship, relationships);

                        // these are the entities of the current node grouped by 
                        // RelationshipAttributes that occured in the previous layer
                        // so it looks like { HasOneAttribute:owner  =>  owner_new }.
                        // Note that in the BeforeUpdateRelationship hook of Person, 
                        // we want want inverse relationship attribute:
                        // we now have the one pointing from article -> person, ]
                        // but we require the the one that points from person -> article             
                        currentEntitiesGrouped = node.RelationshipsFromPreviousLayer.GetRightEntities();
                        currentEntitiesGroupedInverse = ReplaceKeysWithInverseRelationships(currentEntitiesGrouped);

                        var resourcesByRelationship = CreateRelationshipHelper(entityType, currentEntitiesGroupedInverse, dbValues);
                        var allowedIds = CallHook(nestedHookContainer, ResourceHook.BeforeUpdateRelationship, new object[] { GetIds(uniqueEntities), resourcesByRelationship, pipeline }).Cast<string>();
                        var updated = GetAllowedEntities(uniqueEntities, allowedIds);
                        node.UpdateUnique(updated);
                        node.Reassign(_resourceFactory);
                    }
                }

                // Fire the BeforeImplicitUpdateRelationship hook for owner_old.
                // Note: if the pipeline is Post it means we just created article1,
                // which means we are sure that it isn't related to any other entities yet.
                if (pipeline != ResourcePipeline.Post)
                {
                    // To fire a hook for owner_old, we need to first get a reference to it.
                    // For this, we need to query the database for the  HasOneAttribute:owner 
                    // relationship of article1, which is referred to as the 
                    // left side of the HasOneAttribute:owner relationship.
                    var leftEntities = node.RelationshipsFromPreviousLayer.GetLeftEntities();
                    if (leftEntities.Any())
                    {
                        // owner_old is loaded, which is an "implicitly affected entity"
                        FireForAffectedImplicits(entityType, leftEntities, pipeline, uniqueEntities);
                    }
                }

                // Fire the BeforeImplicitUpdateRelationship hook for article2
                // For this, we need to query the database for the current owner 
                // relationship value of owner_new.
                currentEntitiesGrouped = node.RelationshipsFromPreviousLayer.GetRightEntities();
                if (currentEntitiesGrouped.Any())
                {
                    // rightEntities is grouped by relationships from previous 
                    // layer, ie { HasOneAttribute:owner  =>  owner_new }. But 
                    // to load article2 onto owner_new, we need to have the 
                    // RelationshipAttribute from owner to article, which is the
                    // inverse of HasOneAttribute:owner
                    currentEntitiesGroupedInverse = ReplaceKeysWithInverseRelationships(currentEntitiesGrouped);
                    // Note that currently in the JADNC implementation of hooks, 
                    // the root layer is ALWAYS homogenous, so we safely assume 
                    // that for every relationship to the previous layer, the 
                    // left type is the same.
                    LeftType leftType = currentEntitiesGrouped.First().Key.LeftType;
                    FireForAffectedImplicits(leftType, currentEntitiesGroupedInverse, pipeline);
                }
            }
        }

        /// <summary>
        /// replaces the keys of the <paramref name="entitiesByRelationship"/> dictionary
        /// with its inverse relationship attribute.
        /// </summary>
        /// <param name="entitiesByRelationship">Entities grouped by relationship attribute</param>
        private Dictionary<RelationshipAttribute, IEnumerable> ReplaceKeysWithInverseRelationships(Dictionary<RelationshipAttribute, IEnumerable> entitiesByRelationship)
        {
            // when Article has one Owner (HasOneAttribute:owner) is set, there is no guarantee
            // that the inverse attribute was also set (Owner has one Article: HasOneAttr:article).
            // If it isn't, JADNC currently knows nothing about this relationship pointing back, and it 
            // currently cannot fire hooks for entities resolved through inverse relationships.
            var inversableRelationshipAttributes = entitiesByRelationship.Where(kvp => kvp.Key.InverseNavigation != null);
            return inversableRelationshipAttributes.ToDictionary(kvp => _resourceGraph.GetInverse(kvp.Key), kvp => kvp.Value);
        }

        /// <summary>
        /// Given a source of entities, gets the implicitly affected entities 
        /// from the database and calls the BeforeImplicitUpdateRelationship hook.
        /// </summary>
        private void FireForAffectedImplicits(Type entityTypeToInclude, Dictionary<RelationshipAttribute, IEnumerable> implicitsTarget, ResourcePipeline pipeline, IEnumerable existingImplicitEntities = null)
        {
            var container = _executorHelper.GetResourceHookContainer(entityTypeToInclude, ResourceHook.BeforeImplicitUpdateRelationship);
            if (container == null) return;
            var implicitAffected = _executorHelper.LoadImplicitlyAffected(implicitsTarget, existingImplicitEntities);
            if (!implicitAffected.Any()) return;
            var inverse = implicitAffected.ToDictionary(kvp => _resourceGraph.GetInverse(kvp.Key), kvp => kvp.Value);
            var resourcesByRelationship = CreateRelationshipHelper(entityTypeToInclude, inverse);
            CallHook(container, ResourceHook.BeforeImplicitUpdateRelationship, new object[] { resourcesByRelationship, pipeline, });
        }

        /// <summary>
        /// checks that the collection does not contain more than one item when
        /// relevant (eg AfterRead from GetSingle pipeline).
        /// </summary>
        /// <param name="returnedList"> The collection returned from the hook</param>
        /// <param name="pipeline">The pipeline from which the hook was fired</param>
        private void ValidateHookResponse<T>(IEnumerable<T> returnedList, ResourcePipeline pipeline = 0)
        {
            if (pipeline == ResourcePipeline.GetSingle && returnedList.Count() > 1)
            {
                throw new ApplicationException("The returned collection from this hook may contain at most one item in the case of the" +
                    pipeline.ToString("G") + "pipeline");
            }
        }

        /// <summary>
        /// A helper method to call a hook on <paramref name="container"/> reflectively.
        /// </summary>
        private IEnumerable CallHook(IResourceHookContainer container, ResourceHook hook, object[] arguments)
        {
            var method = container.GetType().GetMethod(hook.ToString("G"));
            // note that some of the hooks return "void". When these hooks, the 
            // are called reflectively with Invoke like here, the return value
            // is just null, so we don't have to worry about casting issues here.
            return (IEnumerable)ThrowJsonApiExceptionOnError(() => method.Invoke(container, arguments));
        }

        /// <summary>
        /// If the <see cref="CallHook"/> method, unwrap and throw the actual exception.
        /// </summary>
        private object ThrowJsonApiExceptionOnError(Func<object> action)
        {
            try
            {
                return action();
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        /// <summary>
        /// Helper method to instantiate AffectedRelationships for a given <paramref name="entityType"/>
        /// If <paramref name="dbValues"/> are included, the values of the entries in <paramref name="prevLayerRelationships"/> need to be replaced with these values.
        /// </summary>
        /// <returns>The relationship helper.</returns>
        private IRelationshipsDictionary CreateRelationshipHelper(RightType entityType, Dictionary<RelationshipAttribute, IEnumerable> prevLayerRelationships, IEnumerable dbValues = null)
        {
            if (dbValues != null) prevLayerRelationships = ReplaceWithDbValues(prevLayerRelationships, dbValues.Cast<IIdentifiable>());
            return (IRelationshipsDictionary)TypeHelper.CreateInstanceOfOpenType(typeof(RelationshipsDictionary<>), entityType, true, prevLayerRelationships);
        }

        /// <summary>
        /// Replaces the entities in the values of the prevLayerRelationships dictionary 
        /// with the corresponding entities loaded from the db.
        /// </summary>
        private Dictionary<RelationshipAttribute, IEnumerable> ReplaceWithDbValues(Dictionary<RelationshipAttribute, IEnumerable> prevLayerRelationships, IEnumerable<IIdentifiable> dbValues)
        {
            foreach (var key in prevLayerRelationships.Keys.ToList())
            {
                var replaced = prevLayerRelationships[key].Cast<IIdentifiable>().Select(entity => dbValues.Single(dbEntity => dbEntity.StringId == entity.StringId)).CopyToList(key.LeftType);
                prevLayerRelationships[key] = TypeHelper.CreateHashSetFor(key.LeftType, replaced);
            }
            return prevLayerRelationships;
        }

        /// <summary>
        /// Filter the source set by removing the entities with id that are not 
        /// in <paramref name="allowedIds"/>.
        /// </summary>
        private HashSet<IIdentifiable> GetAllowedEntities(IEnumerable source, IEnumerable<string> allowedIds)
        {
            return new HashSet<IIdentifiable>(source.Cast<IIdentifiable>().Where(ue => allowedIds.Contains(ue.StringId)));
        }

        /// <summary>
        /// given the set of <paramref name="uniqueEntities"/>, it will load all the 
        /// values from the database of these entities.
        /// </summary>
        /// <returns>The db values.</returns>
        /// <param name="entityType">type of the entities to be loaded</param>
        /// <param name="uniqueEntities">The set of entities to load the db values for</param>
        /// <param name="targetHook">The hook in which the db values will be displayed.</param>
        /// <param name="relationshipsToNextLayer">Relationships from <paramref name="entityType"/> to the next layer: 
        /// this indicates which relationships will be included on <paramref name="uniqueEntities"/>.</param>
        private IEnumerable LoadDbValues(Type entityType, IEnumerable uniqueEntities, ResourceHook targetHook, RelationshipAttribute[] relationshipsToNextLayer)
        {
            // We only need to load database values if the target hook of this hook execution
            // cycle is compatible with displaying database values and has this option enabled.
            if (!_executorHelper.ShouldLoadDbValues(entityType, targetHook)) return null;
            return _executorHelper.LoadDbValues(entityType, uniqueEntities, targetHook, relationshipsToNextLayer);
        }

        /// <summary>
        /// Fires the AfterUpdateRelationship hook
        /// </summary>
        private void FireAfterUpdateRelationship(IResourceHookContainer container, INode node, ResourcePipeline pipeline)
        {

            Dictionary<RelationshipAttribute, IEnumerable> currentEntitiesGrouped = node.RelationshipsFromPreviousLayer.GetRightEntities();
            // the relationships attributes in currenEntitiesGrouped will be pointing from a 
            // resource in the previouslayer to a resource in the current (nested) layer.
            // For the nested hook we need to replace these attributes with their inverse.
            // See the FireNestedBeforeUpdateHooks method for a more detailed example.
            var resourcesByRelationship = CreateRelationshipHelper(node.ResourceType, ReplaceKeysWithInverseRelationships(currentEntitiesGrouped));
            CallHook(container, ResourceHook.AfterUpdateRelationship, new object[] { resourcesByRelationship, pipeline });
        }

        /// <summary>
        /// Returns a list of StringIds from a list of IIdentifiable entities (<paramref name="entities"/>).
        /// </summary>
        /// <returns>The ids.</returns>
        /// <param name="entities">IIdentifiable entities.</param>
        private HashSet<string> GetIds(IEnumerable entities)
        {
            return new HashSet<string>(entities.Cast<IIdentifiable>().Select(e => e.StringId));
        }
    }
}
