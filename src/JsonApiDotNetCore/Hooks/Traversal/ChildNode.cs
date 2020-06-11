using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models;
using RightType = System.Type;

namespace JsonApiDotNetCore.Hooks
{
    /// <summary>
    /// Child node in the tree
    /// </summary>
    /// <typeparam name="TResource"></typeparam>
    internal sealed class ChildNode<TResource> : INode where TResource : class, IIdentifiable
    {
        private readonly IdentifiableComparer _comparer = IdentifiableComparer.Instance;
        /// <inheritdoc />
        public RightType ResourceType { get; }
        /// <inheritdoc />
        public RelationshipProxy[] RelationshipsToNextLayer { get; }
        /// <inheritdoc />
        public IEnumerable UniqueEntities
        {
            get
            {
                return new HashSet<TResource>(_relationshipsFromPreviousLayer.SelectMany(rfpl => rfpl.RightEntities));
            }
        }

        /// <inheritdoc />
        public IRelationshipsFromPreviousLayer RelationshipsFromPreviousLayer => _relationshipsFromPreviousLayer;

        private readonly RelationshipsFromPreviousLayer<TResource> _relationshipsFromPreviousLayer;

        public ChildNode(RelationshipProxy[] nextLayerRelationships, RelationshipsFromPreviousLayer<TResource> prevLayerRelationships)
        {
            ResourceType = typeof(TResource);
            RelationshipsToNextLayer = nextLayerRelationships;
            _relationshipsFromPreviousLayer = prevLayerRelationships;
        }

        /// <inheritdoc />
       public void UpdateUnique(IEnumerable updated)
        {
            List<TResource> cast = updated.Cast<TResource>().ToList();
            foreach (var group in _relationshipsFromPreviousLayer)
            {
                group.RightEntities = new HashSet<TResource>(group.RightEntities.Intersect(cast, _comparer).Cast<TResource>());
            }
        }

        /// <summary>
        /// Reassignment is done according to provided relationships
        /// </summary>
        public void Reassign(IResourceFactory resourceFactory, IEnumerable updated = null)
        {
            var unique = (HashSet<TResource>)UniqueEntities;
            foreach (var group in _relationshipsFromPreviousLayer)
            {
                var proxy = group.Proxy;
                var leftEntities = group.LeftEntities;

                foreach (IIdentifiable left in leftEntities)
                {
                    var currentValue = proxy.GetValue(left);

                    if (currentValue is IEnumerable<IIdentifiable> relationshipCollection)
                    {
                        var intersection = relationshipCollection.Intersect(unique, _comparer);
                        IEnumerable typedCollection = intersection.CopyToTypedCollection(relationshipCollection.GetType());
                        proxy.SetValue(left, typedCollection, resourceFactory);
                    }
                    else if (currentValue is IIdentifiable relationshipSingle)
                    {
                        if (!unique.Intersect(new HashSet<IIdentifiable> { relationshipSingle }, _comparer).Any())
                        {
                            proxy.SetValue(left, null, resourceFactory);
                        }
                    }
                }
            }
        }
    }
}
