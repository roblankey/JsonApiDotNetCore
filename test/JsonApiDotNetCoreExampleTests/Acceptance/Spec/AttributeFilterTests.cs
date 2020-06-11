using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bogus;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Newtonsoft.Json;
using Xunit;
using Person = JsonApiDotNetCoreExample.Models.Person;

namespace JsonApiDotNetCoreExampleTests.Acceptance.Spec
{
    [Collection("WebHostCollection")]
    public sealed class AttributeFilterTests
    {
        private readonly TestFixture<TestStartup> _fixture;
        private readonly Faker<TodoItem> _todoItemFaker;
        private readonly Faker<Person> _personFaker;

        public AttributeFilterTests(TestFixture<TestStartup> fixture)
        {
            _fixture = fixture;
            _todoItemFaker = new Faker<TodoItem>()
                .RuleFor(t => t.Description, f => f.Lorem.Sentence())
                .RuleFor(t => t.Ordinal, f => f.Random.Number())
                .RuleFor(t => t.CreatedDate, f => f.Date.Past());

            _personFaker = new Faker<Person>()
                .RuleFor(p => p.FirstName, f => f.Name.FirstName())
                .RuleFor(p => p.LastName, f => f.Name.LastName());
        }

        [Fact]
        public async Task Can_Filter_On_Guid_Properties()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            var todoItem = _todoItemFaker.Generate();
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();

            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?filter[guidProperty]={todoItem.GuidProperty}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var list =  _fixture.GetDeserializer().DeserializeList<TodoItem>(body).Data;
 

            var todoItemResponse = list.Single();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(todoItem.Id, todoItemResponse.Id);
            Assert.Equal(todoItem.GuidProperty, todoItemResponse.GuidProperty);
        }

        [Fact]
        public async Task Can_Filter_On_Related_Attrs()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            var person = _personFaker.Generate();
            var todoItem = _todoItemFaker.Generate();
            todoItem.Owner = person;
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();

            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?include=owner&filter[owner.firstName]={person.FirstName}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var list = _fixture.GetDeserializer().DeserializeList<TodoItem>(body).Data.First();


            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            list.Owner.FirstName = person.FirstName;
        }

        [Fact]
        public async Task Can_Filter_On_Related_Attrs_From_GetById()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            var person = _personFaker.Generate();
            var todoItem = _todoItemFaker.Generate();
            todoItem.Owner = person;
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();

            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems/{todoItem.Id}?include=owner&filter[owner.firstName]=SOMETHING-ELSE";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Cannot_Filter_On_Related_ToMany_Attrs()
        {
            // Arrange
            var httpMethod = new HttpMethod("GET");
            var route = "/api/v1/todoItems?include=childrenTodos&filter[childrenTodos.ordinal]=1";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.BadRequest, errorDocument.Errors[0].StatusCode);
            Assert.Equal("Filtering on one-to-many and many-to-many relationships is currently not supported.", errorDocument.Errors[0].Title);
            Assert.Equal("Filtering on the relationship 'childrenTodos.ordinal' is currently not supported.", errorDocument.Errors[0].Detail);
            Assert.Equal("filter[childrenTodos.ordinal]", errorDocument.Errors[0].Source.Parameter);
        }

        [Fact]
        public async Task Cannot_Filter_If_Explicitly_Forbidden()
        {
            // Arrange
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?include=owner&filter[achievedDate]={new DateTime(2002, 2, 2).ToShortDateString()}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.BadRequest, errorDocument.Errors[0].StatusCode);
            Assert.Equal("Filtering on the requested attribute is not allowed.", errorDocument.Errors[0].Title);
            Assert.Equal("Filtering on attribute 'achievedDate' is not allowed.", errorDocument.Errors[0].Detail);
            Assert.Equal("filter[achievedDate]", errorDocument.Errors[0].Source.Parameter);
        }

        [Fact]
        public async Task Cannot_Filter_Equality_If_Type_Mismatch()
        {
            // Arrange
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?filter[ordinal]=ABC";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.BadRequest, errorDocument.Errors[0].StatusCode);
            Assert.Equal("Mismatch between query string parameter value and resource attribute type.", errorDocument.Errors[0].Title);
            Assert.Equal("Failed to convert 'ABC' to 'Int64' for filtering on 'ordinal' attribute.", errorDocument.Errors[0].Detail);
            Assert.Equal("filter", errorDocument.Errors[0].Source.Parameter);
        }

        [Fact]
        public async Task Cannot_Filter_In_Set_If_Type_Mismatch()
        {
            // Arrange
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?filter[ordinal]=in:1,ABC,2";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);

            // Assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var errorDocument = JsonConvert.DeserializeObject<ErrorDocument>(body);
            Assert.Single(errorDocument.Errors);
            Assert.Equal(HttpStatusCode.BadRequest, errorDocument.Errors[0].StatusCode);
            Assert.Equal("Mismatch between query string parameter value and resource attribute type.", errorDocument.Errors[0].Title);
            Assert.Equal("Failed to convert 'ABC' in set '1,ABC,2' to 'Int64' for filtering on 'ordinal' attribute.", errorDocument.Errors[0].Detail);
            Assert.Equal("filter", errorDocument.Errors[0].Source.Parameter);
        }

        [Fact]
        public async Task Can_Filter_On_Not_Equal_Values()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            var todoItem = _todoItemFaker.Generate();
            context.TodoItems.Add(todoItem);
            await context.SaveChangesAsync();

            var totalCount = context.TodoItems.Count();
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?page[size]={totalCount}&filter[ordinal]=ne:{todoItem.Ordinal}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var list = _fixture.GetDeserializer().DeserializeList<TodoItem>(body).Data;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(list, x => x.Ordinal == todoItem.Ordinal);
        }

        [Fact]
        public async Task Can_Filter_On_In_Array_Values()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            var todoItems = _todoItemFaker.Generate(5);
            var guids = new List<Guid>();
            var notInGuids = new List<Guid>();
            foreach (var item in todoItems)
            {
                context.TodoItems.Add(item);
                // Exclude 2 items
                if (guids.Count < (todoItems.Count - 2))
                    guids.Add(item.GuidProperty);
                else 
                    notInGuids.Add(item.GuidProperty);
            }
            context.SaveChanges();

            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?filter[guidProperty]=in:{string.Join(",", guids)}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedTodoItems = _fixture
                .GetDeserializer()
                .DeserializeList<TodoItem>(body).Data;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(guids.Count, deserializedTodoItems.Count);
            foreach (var item in deserializedTodoItems)
            {
                Assert.Contains(item.GuidProperty, guids);
                Assert.DoesNotContain(item.GuidProperty, notInGuids);
            }
        }

        [Fact]
        public async Task Can_Filter_On_Related_In_Array_Values()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            var todoItems = _todoItemFaker.Generate(3);
            var ownerFirstNames = new List<string>();
            foreach (var item in todoItems)
            {
                var person = _personFaker.Generate();
                ownerFirstNames.Add(person.FirstName);
                item.Owner = person;
                context.TodoItems.Add(item);               
            }
            context.SaveChanges();

            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?include=owner&filter[owner.firstName]=in:{string.Join(",", ownerFirstNames)}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var documents = JsonConvert.DeserializeObject<Document>(body);
            var included = documents.Included;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(ownerFirstNames.Count, documents.ManyData.Count);
            Assert.NotNull(included);
            Assert.NotEmpty(included);
            foreach (var item in included)
                Assert.Contains(item.Attributes["firstName"], ownerFirstNames);

        }

        [Fact]
        public async Task Can_Filter_On_Not_In_Array_Values()
        {
            // Arrange
            var context = _fixture.GetService<AppDbContext>();
            context.TodoItems.RemoveRange(context.TodoItems);
            context.SaveChanges();
            var todoItems = _todoItemFaker.Generate(5);
            var guids = new List<Guid>();
            var notInGuids = new List<Guid>();
            foreach (var item in todoItems)
            {
                context.TodoItems.Add(item);
                // Exclude 2 items
                if (guids.Count < (todoItems.Count - 2))
                    guids.Add(item.GuidProperty);
                else
                    notInGuids.Add(item.GuidProperty);
            }
            context.SaveChanges();

            var totalCount = context.TodoItems.Count();
            var httpMethod = new HttpMethod("GET");
            var route = $"/api/v1/todoItems?page[size]={totalCount}&filter[guidProperty]=nin:{string.Join(",", notInGuids)}";
            var request = new HttpRequestMessage(httpMethod, route);

            // Act
            var response = await _fixture.Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var deserializedTodoItems = _fixture
                .GetDeserializer()
                .DeserializeList<TodoItem>(body).Data;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(totalCount - notInGuids.Count, deserializedTodoItems.Count);
            foreach (var item in deserializedTodoItems)
            {
                Assert.DoesNotContain(item.GuidProperty, notInGuids);
            }
        }
    }
}
