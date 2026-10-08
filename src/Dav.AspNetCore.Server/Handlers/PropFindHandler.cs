using System.Xml.Linq;
using Dav.AspNetCore.Server.Http.Headers;
using Dav.AspNetCore.Server.Store;
using Dav.AspNetCore.Server.Store.Properties;
using Microsoft.AspNetCore.Http;

namespace Dav.AspNetCore.Server.Handlers;

internal class PropFindHandler : RequestHandler
{
    /// <summary>
    /// Handles the web dav request async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    protected override async Task HandleRequestAsync(CancellationToken cancellationToken = default)
    {
        if (Item == null)
        {
            Context.SetResult(DavStatusCode.NotFound);
            return;
        }

        var items = new List<IStoreItem>();
        if (Item is IStoreCollection collection)
        {
            var headers = Context.Request.GetTypedWebDavHeaders();
            if (headers.Depth == Depth.Infinity && Options.DisallowInfinityDepth)
            {
                Context.SetResult(DavStatusCode.Forbidden);
                return;
            }

            var depth = headers.Depth ?? (Options.DisallowInfinityDepth ? Depth.One : Depth.Infinity);
            await AddItemsRecursive(collection, depth, items, cancellationToken);
        }
        else
        {
            items.Add(Item);
        }

        var requestedProperties = await GetRequestedPropertiesAsync(Context, cancellationToken);
        
        var multiStatus = new XElement(XmlNames.MultiStatus);
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            multiStatus);
        
        foreach (var item in items)
        {
            var response = new XElement(XmlNames.Response);
            response.Add(new XElement(XmlNames.Href, $"{Context.Request.PathBase}{item.Uri.AbsolutePath}"));

            var propertyValues = await GetPropertiesAsync(
                item,
                requestedProperties,
                cancellationToken);

            foreach (var statusGrouping in propertyValues
                         .GroupBy(x => x.Value.StatusCode))
            {
                var propStat = new XElement(XmlNames.PropertyStatus);
                var prop = new XElement(XmlNames.Property);   
                
                foreach (var property in statusGrouping)
                {
                    prop.Add(new XElement(property.Key, property.Value.Value));
                }

                propStat.Add(prop);
                propStat.Add(new XElement(XmlNames.Status, $"HTTP/1.1 {(int)statusGrouping.Key} {statusGrouping.Key.GetDisplayName()}"));
                
                response.Add(propStat);
            }
            
            multiStatus.Add(response);
        }

        await Context.WriteDocumentAsync(DavStatusCode.MultiStatus, document, cancellationToken);
    }

    /// <summary>
    /// Adds the collection and, depending on the requested depth, its members to the result.
    /// </summary>
    /// <remarks>
    /// The traversal uses an explicit work stack instead of recursion: a very deep tree (or a link
    /// cycle created outside the service) could otherwise overflow the stack. The result order is
    /// unchanged: the collection, then every sub collection subtree in order, then the remaining items.
    /// </remarks>
    internal static async Task AddItemsRecursive(
        IStoreCollection collection, 
        Depth depth,
        ICollection<IStoreItem> results,
        CancellationToken cancellationToken = default)
    {
        var maxIteration = depth == Depth.Infinity ? int.MaxValue : (int)depth;

        var work = new Stack<WorkItem>();
        work.Push(new CollectionWork(collection, 0));

        while (work.Count > 0)
        {
            switch (work.Pop())
            {
                case CollectionWork collectionWork:
                {
                    results.Add(collectionWork.Collection);

                    if (collectionWork.Iteration >= maxIteration)
                        break;

                    var items = await collectionWork.Collection.GetItemsAsync(cancellationToken);
                    var subCollections = items.OfType<IStoreCollection>().ToArray();
                    var remainingItems = items.Where(x => x is not IStoreCollection).ToArray();

                    // The stack is LIFO: push the remaining items first so every sub collection
                    // subtree is visited before them, and push the sub collections in reverse order.
                    work.Push(new ItemsWork(remainingItems));
                    for (var i = subCollections.Length - 1; i >= 0; i--)
                        work.Push(new CollectionWork(subCollections[i], collectionWork.Iteration + 1));

                    break;
                }
                case ItemsWork itemsWork:
                {
                    foreach (var item in itemsWork.Items)
                        results.Add(item);

                    break;
                }
            }
        }
    }

    private abstract record WorkItem;

    private sealed record CollectionWork(IStoreCollection Collection, int Iteration) : WorkItem;

    private sealed record ItemsWork(IReadOnlyCollection<IStoreItem> Items) : WorkItem;

    private async Task<Dictionary<XName, PropertyResult>> GetPropertiesAsync(
        IStoreItem item,
        PropFindRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.OnlyPropertyNames)
        {
            var propertyNames = await PropertyManager.GetPropertyNamesAsync(item, cancellationToken);
            return propertyNames.ToDictionary(x => x, _ => new PropertyResult(DavStatusCode.Ok));
        }

        var propertyValues = new Dictionary<XName, PropertyResult>();
        var properties = new List<XName>();

        // add all non-expensive properties
        if (request.AllProperties) 
        {
            var propertyNames = await PropertyManager.GetPropertyNamesAsync(item, cancellationToken);
            foreach (var propertyName in propertyNames)
            {
                var propertyMetadata = PropertyManager.GetPropertyMetadata(item, propertyName);
                if (propertyMetadata == null || !propertyMetadata.Expensive)
                    properties.Add(propertyName);
            }
        }

        // this will also contain properties which are included explicitly
        foreach (var propertyName in request.Properties)
        {
            if (properties.All(x => x != propertyName))
                properties.Add(propertyName);
        }
        
        foreach (var propertyName in properties)
        {
            try
            {
                var propertyValue = await PropertyManager.GetPropertyAsync(item, propertyName, cancellationToken);
                propertyValues[propertyName] = propertyValue;
            }
            catch
            {
                propertyValues.Add(propertyName, new PropertyResult(DavStatusCode.InternalServerError));
            }
        }

        return propertyValues;
    }

    private async Task<PropFindRequest> GetRequestedPropertiesAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        var document = await context.ReadDocumentAsync(cancellationToken);
        if (document == null)
        {
            return new PropFindRequest(
                Array.Empty<XName>(),
                false,
                true);
        }

        var propfind = document.Element(XmlNames.PropertyFind);
        if (propfind == null)
        {
            return new PropFindRequest(
                Array.Empty<XName>(),
                false,
                true);
        }
        
        var properties = propfind.Element(XmlNames.Property)?.Elements().ToList() ?? new List<XElement>();
        var allProp = propfind.Element(XmlNames.AllProperties);
        var propNames = propfind.Element(XmlNames.PropertyName);

        var include = propfind.Element(XmlNames.Include);
        if (include != null)
        {
            properties.AddRange(include.Elements());
        }

        return new PropFindRequest(
            properties.Select(x => x.Name).ToList(),
            propNames != null,
            allProp != null);
    }

    private record PropFindRequest(
        IEnumerable<XName> Properties,
        bool OnlyPropertyNames,
        bool AllProperties);
}