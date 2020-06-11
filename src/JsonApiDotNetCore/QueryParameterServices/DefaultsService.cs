using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Exceptions;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Query
{
    /// <inheritdoc/>
    public class DefaultsService : QueryParameterService, IDefaultsService
    {
        private readonly IJsonApiOptions _options;

        public DefaultsService(IJsonApiOptions options)
        {
            SerializerDefaultValueHandling = options.SerializerSettings.DefaultValueHandling;
            _options = options;
        }

        /// <inheritdoc/>
        public DefaultValueHandling SerializerDefaultValueHandling { get; private set; }

        /// <inheritdoc/>
        public bool IsEnabled(DisableQueryAttribute disableQueryAttribute)
        {
            return _options.AllowQueryStringOverrideForSerializerDefaultValueHandling &&
                   !disableQueryAttribute.ContainsParameter(StandardQueryStringParameters.Defaults);
        }

        /// <inheritdoc/>
        public bool CanParse(string parameterName)
        {
            return parameterName == "defaults";
        }

        /// <inheritdoc/>
        public virtual void Parse(string parameterName, StringValues parameterValue)
        {
            if (!bool.TryParse(parameterValue, out var result))
            {
                throw new InvalidQueryStringParameterException(parameterName,
                    "The specified query string value must be 'true' or 'false'.",
                    $"The value '{parameterValue}' for parameter '{parameterName}' is not a valid boolean value.");
            }

            SerializerDefaultValueHandling = result ? DefaultValueHandling.Include : DefaultValueHandling.Ignore;
        }
    }
}
