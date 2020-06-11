using System;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Serialization;
using System.Collections.Generic;
using JsonApiDotNetCore.Internal;

namespace UnitTests.Serialization
{
    public class DeserializerTestsSetup : SerializationTestsSetupBase
    {
        protected sealed class TestDocumentParser : BaseDocumentParser
        {
            public TestDocumentParser(IResourceGraph resourceGraph, IResourceFactory resourceFactory) : base(resourceGraph, resourceFactory) { }

            public new object Deserialize(string body)
            {
                return base.Deserialize(body);
            }

            protected override void AfterProcessField(IIdentifiable entity, IResourceField field, RelationshipEntry data = null) { }
        }

        protected Document CreateDocumentWithRelationships(string mainType, string relationshipMemberName, string relatedType = null, bool isToManyData = false)
        {
            var content = CreateDocumentWithRelationships(mainType);
            content.SingleData.Relationships.Add(relationshipMemberName, CreateRelationshipData(relatedType, isToManyData));
            return content;
        }

        protected Document CreateDocumentWithRelationships(string mainType)
        {
            return new Document
            {
                Data = new ResourceObject
                {
                    Id = "1",
                    Type = mainType,
                    Relationships = new Dictionary<string, RelationshipEntry>()
                }
            };
        }

        protected RelationshipEntry CreateRelationshipData(string relatedType = null, bool isToManyData = false)
        {
            var data = new RelationshipEntry();
            var rio = relatedType == null ? null : new ResourceIdentifierObject { Id = "10", Type = relatedType };

            if (isToManyData)
            {
                data.Data = new List<ResourceIdentifierObject>();
                if (relatedType != null) ((List<ResourceIdentifierObject>)data.Data).Add(rio);
            }
            else
            {
                data.Data = rio;
            }
            return data;
        }

        protected Document CreateTestResourceDocument()
        {
            return new Document
            {
                Data = new ResourceObject
                {
                    Type = "testResource",
                    Id = "1",
                    Attributes = new Dictionary<string, object>
                    {
                        { "stringField", "some string" },
                        { "intField", 1 },
                        { "nullableIntField", null },
                        { "guidField", "1a68be43-cc84-4924-a421-7f4d614b7781" },
                        { "dateTimeField", "9/11/2019 11:41:40 AM" }
                    }
                }
            };
        }
    }
}
