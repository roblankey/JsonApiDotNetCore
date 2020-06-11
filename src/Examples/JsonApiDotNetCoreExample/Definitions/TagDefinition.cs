using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Hooks;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCoreExample.Models;

namespace JsonApiDotNetCoreExample.Definitions
{
    public class TagDefinition : ResourceDefinition<Tag>
    {
        public TagDefinition(IResourceGraph resourceGraph) : base(resourceGraph) { }

        public override IEnumerable<Tag> BeforeCreate(IEntityHashSet<Tag> affected, ResourcePipeline pipeline)
        {
            return base.BeforeCreate(affected, pipeline);
        }

        public override IEnumerable<Tag> OnReturn(HashSet<Tag> entities, ResourcePipeline pipeline)
        {
            return entities.Where(t => t.Name != "This should not be included");
        }
    }
}
