using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Hooks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace JsonApiDotNetCore.Models
{
    public interface IResourceDefinition
    {
        List<AttrAttribute> GetAllowedAttributes();
        List<RelationshipAttribute> GetAllowedRelationships();
        object GetCustomQueryFilter(string key);
        List<(AttrAttribute Attribute, SortDirection SortDirection)> DefaultSort();
    }

    /// <summary>
    /// exposes developer friendly hooks into how their resources are exposed. 
    /// It is intended to improve the experience and reduce boilerplate for commonly required features.
    /// The goal of this class is to reduce the frequency with which developers have to override the
    /// service and repository layers.
    /// </summary>
    /// <typeparam name="TResource">The resource type</typeparam>
    public class ResourceDefinition<TResource> : IResourceDefinition, IResourceHookContainer<TResource> where TResource : class, IIdentifiable
    {
        private readonly IResourceGraph _resourceGraph;
        private List<AttrAttribute> _allowedAttributes;
        private List<RelationshipAttribute> _allowedRelationships;
        public ResourceDefinition(IResourceGraph resourceGraph)
        {
            var resourceContext = resourceGraph.GetResourceContext(typeof(TResource));
            _allowedAttributes = resourceContext.Attributes;
            _allowedRelationships = resourceContext.Relationships;
            _resourceGraph = resourceGraph;
        }

        public List<RelationshipAttribute> GetAllowedRelationships() => _allowedRelationships;
        public List<AttrAttribute> GetAllowedAttributes() => _allowedAttributes;

        /// <summary>
        /// Hides specified attributes and relationships from the serialized output. Can be called directly in a resource definition implementation or
        /// in any resource hook to combine it with eg authorization.
        /// </summary>
        /// <param name="selector">Should be of the form: (TResource e) => new { e.Attribute1, e.Attribute2, e.Relationship1, e.Relationship2 }</param>
        public void HideFields(Expression<Func<TResource, dynamic>> selector)
        {
            var fieldsToHide = _resourceGraph.GetFields(selector);
            _allowedAttributes = _allowedAttributes.Except(fieldsToHide.Where(f => f is AttrAttribute)).Cast<AttrAttribute>().ToList();
            _allowedRelationships = _allowedRelationships.Except(fieldsToHide.Where(f => f is RelationshipAttribute)).Cast<RelationshipAttribute>().ToList();
        }

        /// <summary>
        /// Define a set of custom query expressions that can be applied
        /// instead of the default query behavior. A common use-case for this
        /// is including related resources and filtering on them.
        /// </summary>
        ///
        /// <returns>
        /// A set of custom queries that will be applied instead of the default
        /// queries for the given key. Null will be returned if default behavior
        /// is desired.
        /// </returns>
        ///
        /// <example>
        /// <code>
        /// protected override QueryFilters GetQueryFilters() =>  { 
        ///     { "facility", (t, value) => t.Include(t => t.Tenant)
        ///                                   .Where(t => t.Facility == value) }
        ///  }
        /// </code>
        /// 
        /// If the logic is simply too complex for an in-line expression, you can
        /// delegate to a private method:
        /// <code>
        /// protected override QueryFilters GetQueryFilters()
        ///     => new QueryFilters {
        ///         { "is-active", FilterIsActive }
        ///     };
        /// 
        /// private IQueryable&lt;Model&gt; FilterIsActive(IQueryable&lt;Model&gt; query, string value)
        /// {
        ///     // some complex logic goes here...
        ///     return query.Where(x => x.IsActive == computedValue);
        /// }
        /// </code>
        /// </example>
        public virtual QueryFilters GetQueryFilters() => null;

        public object GetCustomQueryFilter(string key)
        {
            var customFilters = GetQueryFilters();
            if (customFilters != null && customFilters.TryGetValue(key, out var query))
                return query;
            return null;
        }

        /// <inheritdoc/>
        public virtual void AfterCreate(HashSet<TResource> entities, ResourcePipeline pipeline) { }
        /// <inheritdoc/>
        public virtual void AfterRead(HashSet<TResource> entities, ResourcePipeline pipeline, bool isIncluded = false) { }
        /// <inheritdoc/>
        public virtual void AfterUpdate(HashSet<TResource> entities, ResourcePipeline pipeline) { }
        /// <inheritdoc/>
        public virtual void AfterDelete(HashSet<TResource> entities, ResourcePipeline pipeline, bool succeeded) { }
        /// <inheritdoc/>
        public virtual void AfterUpdateRelationship(IRelationshipsDictionary<TResource> entitiesByRelationship, ResourcePipeline pipeline) { }
        /// <inheritdoc/>
        public virtual IEnumerable<TResource> BeforeCreate(IEntityHashSet<TResource> entities, ResourcePipeline pipeline) { return entities; }
        /// <inheritdoc/>
        public virtual void BeforeRead(ResourcePipeline pipeline, bool isIncluded = false, string stringId = null) { }
        /// <inheritdoc/>
        public virtual IEnumerable<TResource> BeforeUpdate(IDiffableEntityHashSet<TResource> entities, ResourcePipeline pipeline) { return entities; }
        /// <inheritdoc/>
        public virtual IEnumerable<TResource> BeforeDelete(IEntityHashSet<TResource> entities, ResourcePipeline pipeline) { return entities; }
        /// <inheritdoc/>
        public virtual IEnumerable<string> BeforeUpdateRelationship(HashSet<string> ids, IRelationshipsDictionary<TResource> entitiesByRelationship, ResourcePipeline pipeline) { return ids; }
        /// <inheritdoc/>
        public virtual void BeforeImplicitUpdateRelationship(IRelationshipsDictionary<TResource> entitiesByRelationship, ResourcePipeline pipeline) { }
        /// <inheritdoc/>
        public virtual IEnumerable<TResource> OnReturn(HashSet<TResource> entities, ResourcePipeline pipeline) { return entities; }


        /// <summary>
        /// This is an alias type intended to simplify the implementation's
        /// method signature.
        /// See <see cref="GetQueryFilters" /> for usage details.
        /// </summary>
        public sealed class QueryFilters : Dictionary<string, Func<IQueryable<TResource>, FilterQuery, IQueryable<TResource>>> { }

        /// <summary>
        /// Define the default sort order if no sort key is provided.
        /// </summary>
        /// <returns>
        /// A list of properties and the direction they should be sorted.
        /// </returns>
        /// <example>
        /// <code>
        /// public override PropertySortOrder GetDefaultSortOrder()
        ///     => new PropertySortOrder {
        ///         (t => t.Prop1, SortDirection.Ascending),
        ///         (t => t.Prop2, SortDirection.Descending),
        ///     };
        /// </code>
        /// </example>
        public virtual PropertySortOrder GetDefaultSortOrder() => null;

        public List<(AttrAttribute Attribute, SortDirection SortDirection)> DefaultSort()
        {
            var defaultSortOrder = GetDefaultSortOrder();
            if (defaultSortOrder != null && defaultSortOrder.Count > 0)
            {
                var order = new List<(AttrAttribute Attribute, SortDirection SortDirection)>();
                foreach (var sortProp in defaultSortOrder)
                {
                    order.Add((_resourceGraph.GetAttributes(sortProp.Attribute).Single(), sortProp.SortDirection));
                }

                return order;
            }

            return null;
        }

        /// <summary>
        /// This is an alias type intended to simplify the implementation's
        /// method signature.
        /// See <see cref="GetQueryFilters" /> for usage details.
        /// </summary>
        public sealed class PropertySortOrder : List<(Expression<Func<TResource, dynamic>> Attribute, SortDirection SortDirection)> { }
    }
}
