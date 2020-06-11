# Resource Services

The `IResourceService` acts as a service layer between the controller and the data access layer.
This allows you to customize it however you want and not be dependent upon Entity Framework Core.
This is also a good place to implement custom business logic.

## Supplementing Default Behavior
If you don't need to alter the actual persistence mechanism, you can inherit from the DefaultResourceService<TModel> and override the existing methods.
In simple cases, you can also just wrap the base implementation with your custom logic.

A simple example would be to send notifications when an entity gets created.

```c#
public class TodoItemService : DefaultResourceService<TodoItem>
{
    private readonly INotificationService _notificationService;

    public TodoItemService(
        INotificationService notificationService,
        IEnumerable<IQueryParameterService> queryParameters,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IResourceRepository<TodoItem> repository,
        IResourceContextProvider provider,
        IResourceChangeTracker<TodoItem> resourceChangeTracker,
        IResourceFactory resourceFactory,
        IResourceHookExecutor hookExecutor)
        : base(queryParameters, options, loggerFactory, repository, provider, resourceChangeTracker, resourceFactory, hookExecutor)
    {
        _notificationService = notificationService;
    }

    public override async Task<TodoItem> CreateAsync(TodoItem entity)
    {
        // Call the base implementation which uses Entity Framework Core
        var newEntity = await base.CreateAsync(entity);

        // Custom code
        _notificationService.Notify($"Entity created: {newEntity.Id}");

        // Don't forget to return the new entity
        return newEntity;
    }
}
```

## Not Using Entity Framework Core?

As previously discussed, this library uses Entity Framework Core by default.
If you'd like to use another ORM that does not implement `IQueryable`, you can use a custom `IResourceService<TModel>` implementation.

```c#
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // add the service override for MyModel
    services.AddScoped<IResourceService<MyModel>, MyModelService>();

    // add your own Data Access Object
    services.AddScoped<IMyModelDao, MyModelDao>();
    // ...
}

// MyModelService.cs
public class MyModelService : IResourceService<MyModel>
{
    private readonly IMyModelDao _dao;

    public MyModelService(IMyModelDao dao)
    {
        _dao = dao;
    }

    public Task<IEnumerable<MyModel>> GetAsync()
    {
        return await _dao.GetModelAsync();
    }

    // ...
}
```

## Limited Requirements

In some cases it may be necessary to only expose a few methods on the resource. For this reason, we have created a hierarchy of service interfaces that can be used to get the exact implementation you require.

This interface hierarchy is defined by this tree structure.

```
IResourceService
|
+-- IResourceQueryService
|   |
|   +-- IGetAllService
|   |   GET /
|   |
|   +-- IGetByIdService
|   |   GET /{id}
|   |
|   +-- IGetRelationshipService
|   |   GET /{id}/{relationship}
|   |
|   +-- IGetRelationshipsService
|       GET /{id}/relationships/{relationship}
|
+-- IResourceCommandService
    |
    +-- ICreateService
    |   POST /
    |
    +-- IDeleteService
    |   DELETE /{id}
    |
    +-- IUpdateService
    |   PATCH /{id}
    |
    +-- IUpdateRelationshipService
        PATCH /{id}/relationships/{relationship}
```

In order to take advantage of these interfaces you first need to inject the service for each implemented interface.

```c#
public class ArticleService : ICreateService<Article>, IDeleteService<Article>
{
  // ...
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICreateService<Article>, ArticleService>();
        services.AddScoped<IDeleteService<Article>, ArticleService>();
    }
}
```

Other dependency injection frameworks such as Autofac can be used to simplify this syntax.

```c#
builder.RegisterType<ArticleService>().AsImplementedInterfaces();
```

Then in the controller, you should inherit from the base controller and pass the services into the named, optional base parameters:

```c#
public class ArticlesController : BaseJsonApiController<Article>
{
    public ArticlesController(
        IJsonApiOptions jsonApiOptions,
        ILoggerFactory loggerFactory,
        ICreateService<Article, int> create,
        IDeleteService<Article, int> delete)
        : base(jsonApiOptions, loggerFactory, create: create, delete: delete)
    { }

    [HttpPost]
    public override async Task<IActionResult> PostAsync([FromBody] Article entity)
    {
        return await base.PostAsync(entity);
    }

    [HttpDelete("{id}")]
    public override async Task<IActionResult>DeleteAsync(int id)
    {
        return await base.DeleteAsync(id);
    }
}
```
