using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.RequestServices;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.Services
{
    public sealed class EntityResourceService_Tests
    {
        private readonly Mock<IResourceRepository<TodoItem>> _repositoryMock = new Mock<IResourceRepository<TodoItem>>();
        private readonly IResourceGraph _resourceGraph;
        private readonly Mock<IIncludeService> _includeService;
        private readonly Mock<ISparseFieldsService> _sparseFieldsService;
        private readonly Mock<IPageService> _pageService;

        public Mock<ISortService> _sortService { get; }
        public Mock<IFilterService> _filterService { get; }

        public EntityResourceService_Tests()
        {
            _includeService = new Mock<IIncludeService>();
            _includeService.Setup(m => m.Get()).Returns(new List<List<RelationshipAttribute>>());
            _sparseFieldsService = new Mock<ISparseFieldsService>();
            _pageService = new Mock<IPageService>();
            _sortService = new Mock<ISortService>();
            _filterService = new Mock<IFilterService>();
            _resourceGraph = new ResourceGraphBuilder(new JsonApiOptions(), NullLoggerFactory.Instance)
                                .AddResource<TodoItem>()
                                .AddResource<TodoItemCollection, Guid>()
                                .Build();
        }

        [Fact]
        public async Task GetRelationshipAsync_Passes_Public_ResourceName_To_Repository()
        {
            // Arrange
            const int id = 1;
            const string relationshipName = "collection";
            var relationship = new RelationshipAttribute[]
            {
                new HasOneAttribute(relationshipName)
                {
                    LeftType = typeof(TodoItem),
                    RightType = typeof(TodoItemCollection)
                }
            };

            var todoItem = new TodoItem();
            var query = new List<TodoItem> { todoItem }.AsQueryable();

            _repositoryMock.Setup(m => m.Get(id)).Returns(query);
            _repositoryMock.Setup(m => m.Include(query, relationship)).Returns(query);
            _repositoryMock.Setup(m => m.FirstOrDefaultAsync(query)).ReturnsAsync(todoItem);

            var service = GetService();

            // Act
            await service.GetRelationshipAsync(id, relationshipName);

            // Assert
            _repositoryMock.Verify(m => m.Get(id), Times.Once);
            _repositoryMock.Verify(m => m.Include(query, relationship), Times.Once);
            _repositoryMock.Verify(m => m.FirstOrDefaultAsync(query), Times.Once);
        }

        [Fact]
        public async Task GetRelationshipAsync_Returns_Relationship_Value()
        {
            // Arrange
            const int id = 1;
            const string relationshipName = "collection";
            var relationships = new RelationshipAttribute[]
            {
                new HasOneAttribute(relationshipName)
                {
                    LeftType = typeof(TodoItem),
                    RightType = typeof(TodoItemCollection)
                }
            };

            var todoItem = new TodoItem
            {
                Collection = new TodoItemCollection { Id = Guid.NewGuid() }
            };

            var query = new List<TodoItem> { todoItem }.AsQueryable();

            _repositoryMock.Setup(m => m.Get(id)).Returns(query);
            _repositoryMock.Setup(m => m.Include(query, relationships)).Returns(query);
            _repositoryMock.Setup(m => m.FirstOrDefaultAsync(query)).ReturnsAsync(todoItem);

            var service = GetService();

            // Act
            var result = await service.GetRelationshipAsync(id, relationshipName);

            // Assert
            Assert.NotNull(result);
            var collection = Assert.IsType<TodoItemCollection>(result);
            Assert.Equal(todoItem.Collection.Id, collection.Id);
        }

        private DefaultResourceService<TodoItem> GetService()
        {
            var queryParamServices = new List<IQueryParameterService>
            {
                _includeService.Object, _pageService.Object, _filterService.Object,
                _sortService.Object, _sparseFieldsService.Object
            };

            var options = new JsonApiOptions();
            var changeTracker = new DefaultResourceChangeTracker<TodoItem>(options, _resourceGraph, new TargetedFields());

            return new DefaultResourceService<TodoItem>(queryParamServices, options, NullLoggerFactory.Instance, _repositoryMock.Object, _resourceGraph, changeTracker, new DefaultResourceFactory(new ServiceContainer()));
        }
    }
}
