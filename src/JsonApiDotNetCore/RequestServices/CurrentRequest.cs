using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Managers.Contracts;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Managers
{
    internal sealed class CurrentRequest : ICurrentRequest
    {
        private ResourceContext _resourceContext;
        public string BasePath { get; set; }
        public bool IsRelationshipPath { get; set; }
        public RelationshipAttribute RequestRelationship { get; set; }
        public string BaseId { get; set; }
        public string RelationshipId { get; set; }

        /// <summary>
        /// The main resource of the request.
        /// </summary>
        /// <returns></returns>
        public ResourceContext GetRequestResource()
        {
            return _resourceContext;
        }

        public void SetRequestResource(ResourceContext primaryResource)
        {
            _resourceContext = primaryResource;
        }
    }
}
