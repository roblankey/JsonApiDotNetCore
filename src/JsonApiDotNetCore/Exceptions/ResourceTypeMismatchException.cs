using System.Net;
using System.Net.Http;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Models.JsonApiDocuments;

namespace JsonApiDotNetCore.Exceptions
{
    /// <summary>
    /// The error that is thrown when the resource type in the request body does not match the type expected at the current endpoint URL.
    /// </summary>
    public sealed class ResourceTypeMismatchException : JsonApiException
    {
        public ResourceTypeMismatchException(HttpMethod method, string requestPath, ResourceContext expected, ResourceContext actual) 
            : base(new Error(HttpStatusCode.Conflict)
        {
            Title = "Resource type mismatch between request body and endpoint URL.",
            Detail = $"Expected resource of type '{expected.ResourceName}' in {method} request body at endpoint '{requestPath}', instead of '{actual.ResourceName}'."
        })
        {
        }
    }
}
