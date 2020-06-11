using System.Net;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.JsonApiDocuments;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExample.Controllers
{
    public abstract class AbstractTodoItemsController<T> 
    : BaseJsonApiController<T> where T : class, IIdentifiable<int>
    {
        protected AbstractTodoItemsController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceService<T, int> service)
            : base(jsonApiOptions, loggerFactory, service)
        { }
    }

    [DisableRoutingConvention]
    [Route("/abstract")]
    public class TodoItemsTestController : AbstractTodoItemsController<TodoItem>
    {
        public TodoItemsTestController(
            IJsonApiOptions jsonApiOptions,
            ILoggerFactory loggerFactory,
            IResourceService<TodoItem> service)
            : base(jsonApiOptions, loggerFactory, service)
        { }

        [HttpGet]
        public override async Task<IActionResult> GetAsync() => await base.GetAsync();

        [HttpGet("{id}")]
        public override async Task<IActionResult> GetAsync(int id) => await base.GetAsync(id);

        [HttpGet("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> GetRelationshipsAsync(int id, string relationshipName)
            => await base.GetRelationshipsAsync(id, relationshipName);

        [HttpGet("{id}/{relationshipName}")]
        public override async Task<IActionResult> GetRelationshipAsync(int id, string relationshipName)
            => await base.GetRelationshipAsync(id, relationshipName);

        [HttpPost]
        public override async Task<IActionResult> PostAsync(TodoItem entity)
        {
            await Task.Yield();

            return NotFound(new Error(HttpStatusCode.NotFound)
            {
                Title = "NotFound ActionResult with explicit error object."
            });
        }

        [HttpPatch("{id}")]
        public override async Task<IActionResult> PatchAsync(int id, [FromBody] TodoItem entity)
        {
            await Task.Yield();

            return Conflict("Something went wrong");
        }

        [HttpPatch("{id}/relationships/{relationshipName}")]
        public override async Task<IActionResult> PatchRelationshipsAsync(
            int id, string relationshipName, [FromBody] object relationships)
            => await base.PatchRelationshipsAsync(id, relationshipName, relationships);

        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(int id)
        {
            await Task.Yield();

            return NotFound();
        }
    }
}
