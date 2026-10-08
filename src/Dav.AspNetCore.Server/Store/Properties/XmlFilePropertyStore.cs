using System.Xml;
using System.Xml.Linq;

namespace Dav.AspNetCore.Server.Store.Properties;

public class XmlFilePropertyStore : IPropertyStore
{
    private const string Namespace = "https://github.com/ThuCommix/Dav.AspNetCore.Server";
    private static readonly XName PropertyStore = XName.Get("PropertyStore", Namespace);
    private static readonly XName Property = XName.Get("Property", Namespace);
    
    private readonly XmlFilePropertyStoreOptions options;
    private readonly Dictionary<IStoreItem, Dictionary<XName, PropertyData>> propertyCache = new();
    private readonly Dictionary<IStoreItem, bool> writeLookup = new();

    /// <summary>
    /// Initializes a new <see cref="XmlFilePropertyStore"/> class.
    /// </summary>
    /// <param name="options">The xml file property store options.</param>
    public XmlFilePropertyStore(XmlFilePropertyStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
        if (string.IsNullOrWhiteSpace(options.RootPath))
            throw new InvalidOperationException(
                "The xml file property store root path must be configured via XmlFilePropertyStoreOptions.RootPath.");
        this.options = options;
    }

    /// <summary>
    /// Commits the property store changes async.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in propertyCache)
        {
            if (!writeLookup.ContainsKey(entry.Key))
                continue;
            
            var propertyStore = new XElement(PropertyStore);
            var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
                propertyStore);

            foreach (var propertyData in entry.Value)
            {
                propertyStore.Add(new XElement(Property, new XElement(propertyData.Value.Name, propertyData.Value.CurrentValue)));
            }
            
            var xmlFilePath = StorePath.Resolve(options.RootPath, entry.Key.Uri.LocalPath + ".xml");
            var fileInfo = new FileInfo(xmlFilePath);
            if (fileInfo.Directory?.Exists == false)
                fileInfo.Directory.Create();

            // FileMode.Create truncates the file: File.OpenWrite keeps the trailing bytes of a
            // longer previous document, which leaves invalid XML behind after a shorter rewrite.
            await using var fileStream = new FileStream(xmlFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await document.SaveAsync(fileStream, SaveOptions.None, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes all properties of the specified item async.
    /// </summary>
    /// <param name="item">The store item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public ValueTask DeletePropertiesAsync(
        IStoreItem item, 
        CancellationToken cancellationToken = default)
    {
        var xmlFilePath = StorePath.Resolve(options.RootPath, item.Uri.LocalPath + ".xml");
        if (File.Exists(xmlFilePath))
            File.Delete(xmlFilePath);

        propertyCache.Remove(item);
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Copies all properties of the specified item to the destination async.
    /// </summary>
    /// <param name="source">The source store item.</param>
    /// <param name="destination">The destination store item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public ValueTask CopyPropertiesAsync(
        IStoreItem source, 
        IStoreItem destination, 
        CancellationToken cancellationToken = default)
    {
        var sourceXmlFilePath = StorePath.Resolve(options.RootPath, source.Uri.LocalPath + ".xml");
        var destinationXmlFilePath = StorePath.Resolve(options.RootPath, destination.Uri.LocalPath + ".xml");
        
        if (File.Exists(sourceXmlFilePath))
        {
            var fileInfo = new FileInfo(destinationXmlFilePath);
            if (fileInfo.Directory?.Exists == false)
                fileInfo.Directory.Create();

            File.Copy(sourceXmlFilePath, destinationXmlFilePath, true);
        }

        if (propertyCache.TryGetValue(source, out var propertyMap))
        {
            propertyCache[destination] = propertyMap.ToDictionary(x => x.Key, x => x.Value);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Sets a property of the specified item async.
    /// </summary>
    /// <param name="item">The store item.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyMetadata">The property metadata.</param>
    /// <param name="isRegistered">A value indicating whether the property is registered.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    public async ValueTask<bool> SetPropertyAsync(
        IStoreItem item, 
        XName propertyName, 
        PropertyMetadata propertyMetadata,
        bool isRegistered,
        object? propertyValue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(item));
        ArgumentNullException.ThrowIfNull(propertyName, nameof(propertyName));
        
        if (!propertyCache.TryGetValue(item, out var propertyMap))
        {
            await GetPropertiesAsync(item, cancellationToken);
            propertyMap = propertyCache[item];
        }

        var propertyExists = propertyMap.ContainsKey(propertyName);
        if (propertyExists || options.AcceptCustomProperties || isRegistered)
        {
            propertyMap[propertyName] = new PropertyData(propertyName, propertyValue);
            writeLookup[item] = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the properties of the specified item async.
    /// </summary>
    /// <param name="item">The store item.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of stored properties.</returns>
    public async ValueTask<IReadOnlyCollection<PropertyData>> GetPropertiesAsync(
        IStoreItem item, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item, nameof(item));
        
        if (propertyCache.TryGetValue(item, out var propertyMap))
            return propertyMap.Values;
        
        var xmlFilePath = StorePath.Resolve(options.RootPath, item.Uri.LocalPath + ".xml");
        if (!File.Exists(xmlFilePath))
        {
            propertyCache[item] = new Dictionary<XName, PropertyData>();
            return Array.Empty<PropertyData>();
        }

        var fileStream = File.OpenRead(xmlFilePath);
        var propertyDataList = new List<PropertyData>();
        
        try
        {
            var document = await XDocument.LoadAsync(fileStream, LoadOptions.None, cancellationToken);
            var propertyStore = document.Element(PropertyStore);
            if (propertyStore == null)
            {
                propertyCache[item] = new Dictionary<XName, PropertyData>();
                return propertyDataList;
            }

            foreach (var propertyElement in propertyStore.Elements(Property))
            {
                // A property element without a value ("<name/>") is valid and must not break the
                // whole file; it round-trips as a property with a null value.
                var property = propertyElement.Elements().FirstOrDefault();
                if (property == null)
                    continue;

                object? propertyValue = null;
                if (property.FirstNode != null)
                {
                    propertyValue = property.FirstNode.NodeType switch
                    {
                        XmlNodeType.Text => property.Value,
                        XmlNodeType.Element => property.Elements().ToArray(),
                        _ => propertyValue
                    };
                }

                var propertyData = new PropertyData(
                    property.Name,
                    propertyValue);
                
                propertyDataList.Add(propertyData);
            }
        }
        catch (XmlException)
        {
            // A file that is not valid XML (for example written by an older version that did not
            // truncate) is treated as empty, but the result is not cached so a later write cannot
            // silently persist the empty state over recoverable data.
            return propertyDataList;
        }
        finally
        {
            await fileStream.DisposeAsync();
        }
        
        propertyCache[item] = new Dictionary<XName, PropertyData>();
        foreach (var propertyData in propertyDataList)
        {
            propertyCache[item][propertyData.Name] = propertyData;
        }
        
        return propertyDataList;
    }
}