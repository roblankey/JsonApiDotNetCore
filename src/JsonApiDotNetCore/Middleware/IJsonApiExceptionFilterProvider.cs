using System;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// Provides the type of the global exception filter that is configured in MVC during startup.
    /// This can be overridden to let JADNC use your own exception filter. The default exception filter used
    /// is <see cref="DefaultExceptionFilter"/>
    /// </summary>
    public interface IJsonApiExceptionFilterProvider
    {
        Type Get();
    }

    /// <inheritdoc/>
    public class JsonApiExceptionFilterProvider : IJsonApiExceptionFilterProvider
    {
        public Type Get() => typeof(DefaultExceptionFilter);
    }
}
