using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.Services;

namespace JsonApiDotNetCore.Serialization.Server.Builders
{
    /// <inheritdoc/>
    public class MetaBuilder<T> : IMetaBuilder<T> where T : class, IIdentifiable
    {
        private Dictionary<string, object> _meta = new Dictionary<string, object>();
        private readonly IPageService _pageService;
        private readonly IJsonApiOptions _options;
        private readonly IRequestMeta _requestMeta;
        private readonly IHasMeta _resourceMeta;

        public MetaBuilder(IPageService pageService,
                           IJsonApiOptions options,
                           IRequestMeta requestMeta = null,
                           ResourceDefinition<T> resourceDefinition = null)
        {
            _pageService = pageService;
            _options = options;
            _requestMeta = requestMeta;
            _resourceMeta = resourceDefinition as IHasMeta;
        }
        /// <inheritdoc/>
        public void Add(string key, object value)
        {
            _meta[key] = value;
        }

        /// <inheritdoc/>
        public void Add(Dictionary<string,object> values)
        {
            _meta = values.Keys.Union(_meta.Keys)
                .ToDictionary(key => key, 
                    key => values.ContainsKey(key) ? values[key] : _meta[key]);
        }

        /// <inheritdoc/>
        public Dictionary<string, object> GetMeta()
        {
            if (_options.IncludeTotalRecordCount && _pageService.TotalRecords != null)
                _meta.Add("total-records", _pageService.TotalRecords);

            if (_requestMeta != null)
                Add(_requestMeta.GetMeta());

            if (_resourceMeta != null)
                Add(_resourceMeta.GetMeta());

            if (_meta.Any()) return _meta;
            return null;
        }
    }
}