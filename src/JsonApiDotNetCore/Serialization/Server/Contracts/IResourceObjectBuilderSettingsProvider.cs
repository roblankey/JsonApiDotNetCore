﻿namespace JsonApiDotNetCore.Serialization.Server
{
    /// <summary>
    /// Service that provides the server serializer with <see cref="ResourceObjectBuilderSettings"/> 
    /// </summary>
    public interface IResourceObjectBuilderSettingsProvider
    {
        /// <summary>
        /// Gets the behaviour for the serializer it is injected in.
        /// </summary>
        ResourceObjectBuilderSettings Get();
    }
}
