using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Graph;
using JsonApiDotNetCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using JsonApiDotNetCore.Serialization.Client;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Internal.Contracts;
using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCore
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Configures JsonApiDotNetCore by registering resources manually.
        /// </summary>
        public static IServiceCollection AddJsonApi(this IServiceCollection services,
            Action<JsonApiOptions> options = null,
            Action<IServiceDiscoveryFacade> discovery = null,
            Action<IResourceGraphBuilder> resources = null,
            IMvcCoreBuilder mvcBuilder = null)
        {
            SetupApplicationBuilder(services, options, discovery, resources, mvcBuilder, null);
            ResolveInverseRelationships(services);

            return services;
        }

        /// <summary>
        /// Configures JsonApiDotNetCore by registering resources from an Entity Framework Core model.
        /// </summary>
        public static IServiceCollection AddJsonApi<TDbContext>(this IServiceCollection services,
            Action<JsonApiOptions> options = null,
            Action<IServiceDiscoveryFacade> discovery = null,
            Action<IResourceGraphBuilder> resources = null,
            IMvcCoreBuilder mvcBuilder = null)
            where TDbContext : DbContext
        {
            SetupApplicationBuilder(services, options, discovery, resources, mvcBuilder, typeof(TDbContext));
            ResolveInverseRelationships(services);

            return services;
        }

        private static void SetupApplicationBuilder(IServiceCollection services, Action<JsonApiOptions> options,
            Action<IServiceDiscoveryFacade> discovery,
            Action<IResourceGraphBuilder> resources, IMvcCoreBuilder mvcBuilder, Type dbContextType)
        {
            var applicationBuilder = new JsonApiApplicationBuilder(services, mvcBuilder ?? services.AddMvcCore());

            applicationBuilder.ConfigureJsonApiOptions(options);
            applicationBuilder.ConfigureMvc(dbContextType);
            applicationBuilder.AutoDiscover(discovery);
            applicationBuilder.ConfigureResources(resources);
            applicationBuilder.ConfigureServices();
        }

        private static void ResolveInverseRelationships(IServiceCollection services)
        {
            using var intermediateProvider = services.BuildServiceProvider();
            using var scope = intermediateProvider.CreateScope();

            var inverseRelationshipResolver = scope.ServiceProvider.GetService<IInverseRelationships>();
            inverseRelationshipResolver?.Resolve();
        }

        /// <summary>
        /// Enables client serializers for sending requests and receiving responses
        /// in json:api format. Internally only used for testing.
        /// Will be extended in the future to be part of a JsonApiClientDotNetCore package.
        /// </summary>
        public static IServiceCollection AddClientSerialization(this IServiceCollection services)
        {
            services.AddSingleton<IResponseDeserializer, ResponseDeserializer>();
            services.AddSingleton<IRequestSerializer>(sp =>
            {
                var graph = sp.GetService<IResourceGraph>();
                return new RequestSerializer(graph, new ResourceObjectBuilder(graph, new ResourceObjectBuilderSettings()));
            });
            return services;
        }

        /// <summary>
        /// Adds all required registrations for the service to the container
        /// </summary>
        /// <exception cref="JsonApiSetupException"/>
        public static IServiceCollection AddResourceService<T>(this IServiceCollection services)
        {
            var typeImplementsAnExpectedInterface = false;

            var serviceImplementationType = typeof(T);

            // it is _possible_ that a single concrete type could be used for multiple resources...
            var resourceDescriptors = GetResourceTypesFromServiceImplementation(serviceImplementationType);

            foreach (var resourceDescriptor in resourceDescriptors)
            {
                foreach (var openGenericType in ServiceDiscoveryFacade.ServiceInterfaces)
                {
                    // A shorthand interface is one where the id type is omitted
                    // e.g. IResourceService<T> is the shorthand for IResourceService<T, TId>
                    var isShorthandInterface = openGenericType.GetTypeInfo().GenericTypeParameters.Length == 1;
                    if (isShorthandInterface && resourceDescriptor.IdType != typeof(int))
                        continue; // we can't create a shorthand for id types other than int

                    var concreteGenericType = isShorthandInterface
                        ? openGenericType.MakeGenericType(resourceDescriptor.ResourceType)
                        : openGenericType.MakeGenericType(resourceDescriptor.ResourceType, resourceDescriptor.IdType);

                    if (concreteGenericType.IsAssignableFrom(serviceImplementationType))
                    {
                        services.AddScoped(concreteGenericType, serviceImplementationType);
                        typeImplementsAnExpectedInterface = true;
                    }
                }
            }

            if (typeImplementsAnExpectedInterface == false)
                throw new JsonApiSetupException($"{serviceImplementationType} does not implement any of the expected JsonApiDotNetCore interfaces.");

            return services;
        }

        private static HashSet<ResourceDescriptor> GetResourceTypesFromServiceImplementation(Type type)
        {
            var resourceDescriptors = new HashSet<ResourceDescriptor>();
            var interfaces = type.GetInterfaces();
            foreach (var i in interfaces)
            {
                if (i.IsGenericType)
                {
                    var firstGenericArgument = i.GenericTypeArguments.FirstOrDefault();
                    if (TypeLocator.TryGetResourceDescriptor(firstGenericArgument, out var resourceDescriptor))
                    {
                        resourceDescriptors.Add(resourceDescriptor);
                    }
                }
            }
            return resourceDescriptors;
        }
    }
}
