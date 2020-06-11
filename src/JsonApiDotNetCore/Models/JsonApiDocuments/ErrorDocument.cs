using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace JsonApiDotNetCore.Models.JsonApiDocuments
{
    public sealed class ErrorDocument
    {
        public IReadOnlyList<Error> Errors { get; }

        public ErrorDocument()
        {
            Errors = new List<Error>();
        }

        public ErrorDocument(Error error)
        {
            Errors = new List<Error> {error};
        }

        public ErrorDocument(IEnumerable<Error> errors)
        {
            Errors = errors.ToList();
        }

        public HttpStatusCode GetErrorStatusCode()
        {
            var statusCodes = Errors
                .Select(e => (int)e.StatusCode)
                .Distinct()
                .ToList();

            if (statusCodes.Count == 1)
                return (HttpStatusCode)statusCodes[0];

            var statusCode = int.Parse(statusCodes.Max().ToString()[0] + "00");
            return (HttpStatusCode)statusCode;
        }
    }
}
