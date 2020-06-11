using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Hooks
{
    internal interface ITraversalHelper
    {
        /// <summary>
        /// Crates the next layer
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        NodeLayer CreateNextLayer(INode node);
        /// <summary>
        /// Creates the next layer based on the nodes provided
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        NodeLayer CreateNextLayer(IEnumerable<INode> nodes);
        /// <summary>
        /// Creates a root node for breadth-first-traversal (BFS). Note that typically, in
        /// JADNC, the root layer will be homogeneous. Also, because it is the first layer,
        /// there can be no relationships to previous layers, only to next layers.
        /// </summary>
        /// <returns>The root node.</returns>
        /// <param name="rootEntities">Root entities.</param>
        /// <typeparam name="TResource">The 1st type parameter.</typeparam>
        RootNode<TResource> CreateRootNode<TResource>(IEnumerable<TResource> rootEntities) where TResource : class, IIdentifiable;
    }
}
