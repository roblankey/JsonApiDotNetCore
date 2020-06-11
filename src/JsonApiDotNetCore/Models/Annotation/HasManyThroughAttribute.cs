using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Extensions;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models.Links;

namespace JsonApiDotNetCore.Models
{
    /// <summary>
    /// Create a HasMany relationship through a many-to-many join relationship.
    /// This type can only be applied on types that implement ICollection.
    /// </summary>
    /// 
    /// <example>
    /// In the following example, we expose a relationship named "tags"
    /// through the navigation property `ArticleTags`.
    /// The `Tags` property is decorated as `NotMapped` so that EF does not try
    /// to map this to a database relationship.
    /// <code>
    /// [NotMapped]
    /// [HasManyThrough("tags", nameof(ArticleTags))]
    /// public ICollection&lt;Tag&gt; Tags { get; set; }
    /// public ICollection&lt;ArticleTag&gt; ArticleTags { get; set; }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class HasManyThroughAttribute : HasManyAttribute
    {
        /// <summary>
        /// Create a HasMany relationship through a many-to-many join relationship.
        /// The public name exposed through the API will be based on the configured convention.
        /// </summary>
        /// 
        /// <param name="throughPropertyName">The name of the navigation property that will be used to get the HasMany relationship</param>
        /// <param name="relationshipLinks">Which links are available. Defaults to <see cref="Link.All"/></param>
        /// <param name="canInclude">Whether or not this relationship can be included using the <c>?include=public-name</c> query string</param>
        /// 
        /// <example>
        /// <code>
        /// [HasManyThrough(nameof(ArticleTags), relationshipLinks: Link.All, canInclude: true)]
        /// </code>
        /// </example>
        public HasManyThroughAttribute(string throughPropertyName, Link relationshipLinks = Link.All, bool canInclude = true)
        : base(null, relationshipLinks, canInclude)
        {
            ThroughPropertyName = throughPropertyName;
        }

        /// <summary>
        /// Create a HasMany relationship through a many-to-many join relationship.
        /// </summary>
        /// 
        /// <param name="publicName">The relationship name as exposed by the API</param>
        /// <param name="throughPropertyName">The name of the navigation property that will be used to get the HasMany relationship</param>
        /// <param name="relationshipLinks">Which links are available. Defaults to <see cref="Link.All"/></param>
        /// <param name="canInclude">Whether or not this relationship can be included using the <c>?include=public-name</c> query string</param>
        /// 
        /// <example>
        /// <code>
        /// [HasManyThrough("tags", nameof(ArticleTags), relationshipLinks: Link.All, canInclude: true)]
        /// </code>
        /// </example>
        public HasManyThroughAttribute(string publicName, string throughPropertyName, Link relationshipLinks = Link.All, bool canInclude = true)
        : base(publicName, relationshipLinks, canInclude)
        {
            ThroughPropertyName = throughPropertyName;
        }

        /// <summary>
        /// Traverses through the provided entity and returns the 
        /// value of the relationship on the other side of a join entity
        /// (e.g. Articles.ArticleTags.Tag).
        /// </summary>
        public override object GetValue(object entity)
        {
            IEnumerable joinEntities = (IEnumerable)ThroughProperty.GetValue(entity) ?? Array.Empty<object>();

            IEnumerable<object> rightEntities = joinEntities
                .Cast<object>()
                .Select(rightEntity =>  RightProperty.GetValue(rightEntity));

            return rightEntities.CopyToTypedCollection(PropertyInfo.PropertyType);
        }

        /// <inheritdoc />
        public override void SetValue(object entity, object newValue, IResourceFactory resourceFactory)
        {
            base.SetValue(entity, newValue, resourceFactory);

            if (newValue == null)
            {
                ThroughProperty.SetValue(entity, null);
            }
            else
            {
                List<object> joinEntities = new List<object>();
                foreach (IIdentifiable resource in (IEnumerable)newValue)
                {
                    object joinEntity = resourceFactory.CreateInstance(ThroughType);
                    LeftProperty.SetValue(joinEntity, entity);
                    RightProperty.SetValue(joinEntity, resource);
                    joinEntities.Add(joinEntity);
                }

                var typedCollection = joinEntities.CopyToTypedCollection(ThroughProperty.PropertyType);
                ThroughProperty.SetValue(entity, typedCollection);
            }
        }

        /// <summary>
        /// The name of the join property on the parent resource.
        /// </summary>
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would be "ArticleTags".
        /// </example>
        internal string ThroughPropertyName { get; }

        /// <summary>
        /// The join type.
        /// </summary>
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would be `ArticleTag`.
        /// </example>
        public Type ThroughType { get; internal set; }

        /// <summary>
        /// The navigation property back to the parent resource from the join type.
        /// </summary>
        /// 
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would point to the `Article.ArticleTags.Article` property
        ///
        /// <code>
        /// public Article Article { get; set; }
        /// </code>
        ///
        /// </example>
        public PropertyInfo LeftProperty { get; internal set; }

        /// <summary>
        /// The id property back to the parent resource from the join type.
        /// </summary>
        /// 
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would point to the `Article.ArticleTags.ArticleId` property
        ///
        /// <code>
        /// public int ArticleId { get; set; }
        /// </code>
        ///
        /// </example>
        public PropertyInfo LeftIdProperty { get; internal set; }

        /// <summary>
        /// The navigation property to the related resource from the join type.
        /// </summary>
        /// 
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would point to the `Article.ArticleTags.Tag` property
        ///
        /// <code>
        /// public Tag Tag { get; set; }
        /// </code>
        ///
        /// </example>
        public PropertyInfo RightProperty { get; internal set; }

        /// <summary>
        /// The id property to the related resource from the join type.
        /// </summary>
        /// 
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would point to the `Article.ArticleTags.TagId` property
        ///
        /// <code>
        /// public int TagId { get; set; }
        /// </code>
        ///
        /// </example>
        public PropertyInfo RightIdProperty { get; internal set; }

        /// <summary>
        /// The join entity property on the parent resource.
        /// </summary>
        /// 
        /// <example>
        /// In the `[HasManyThrough("tags", nameof(ArticleTags))]` example
        /// this would point to the `Article.ArticleTags` property
        ///
        /// <code>
        /// public ICollection&lt;ArticleTags&gt; ArticleTags { get; set; }
        /// </code>
        ///
        /// </example>
        public PropertyInfo ThroughProperty { get; internal set; }

        /// <inheritdoc />
        /// <example>
        /// "ArticleTags.Tag"
        /// </example>
        public override string RelationshipPath => $"{ThroughProperty.Name}.{RightProperty.Name}";
    }
}
