using Dav.AspNetCore.Server.Handlers;
using Dav.AspNetCore.Server.Http.Headers;
using Dav.AspNetCore.Server.Store;
using Xunit;

namespace Dav.AspNetCore.Server.Tests.Handlers;

public class PropFindHandlerTest
{
    [Fact]
    public async Task AddItemsRecursive_DepthOne_AddsRootAndDirectChildrenOnly()
    {
        // arrange
        var file = new TestStoreItem(UriHelper.CreateUri("/file.txt"));
        var subCollectionFile = new TestStoreItem(UriHelper.CreateUri("/sub/inner.txt"));
        var subCollection = new FakeCollection(UriHelper.CreateUri("/sub"), new IStoreItem[] { subCollectionFile });
        var root = new FakeCollection(UriHelper.CreateUri("/"), new IStoreItem[] { subCollection, file });

        var results = new List<IStoreItem>();

        // act
        await PropFindHandler.AddItemsRecursive(root, Depth.One, results);

        // assert
        Assert.Equal(new IStoreItem[] { root, subCollection, file }, results);
    }

    [Fact]
    public async Task AddItemsRecursive_KeepsSubCollectionSubtreeOrder()
    {
        // arrange
        var a1 = new TestStoreItem(UriHelper.CreateUri("/a/a1.txt"));
        var a = new FakeCollection(UriHelper.CreateUri("/a"), new IStoreItem[] { a1 });
        var b = new FakeCollection(UriHelper.CreateUri("/b"));
        var f1 = new TestStoreItem(UriHelper.CreateUri("/f1.txt"));
        var f2 = new TestStoreItem(UriHelper.CreateUri("/f2.txt"));
        var root = new FakeCollection(UriHelper.CreateUri("/"), new IStoreItem[] { a, f1, b, f2 });

        var results = new List<IStoreItem>();

        // act
        await PropFindHandler.AddItemsRecursive(root, Depth.Infinity, results);

        // assert: the whole sub collection subtrees come first, then the remaining items.
        Assert.Equal(new IStoreItem[] { root, a, a1, b, f1, f2 }, results);
    }

    [Fact]
    public async Task AddItemsRecursive_VeryDeepTree_DoesNotOverflowTheStack()
    {
        // arrange: 10_000 nested collections. The traversal used to recurse once per level.
        IStoreCollection current = new FakeCollection(UriHelper.CreateUri("/level9999"));
        for (var i = 9998; i >= 0; i--)
            current = new FakeCollection(UriHelper.CreateUri($"/level{i}"), new IStoreItem[] { current });

        var results = new List<IStoreItem>();

        // act
        await PropFindHandler.AddItemsRecursive(current, Depth.Infinity, results);

        // assert
        Assert.Equal(10_000, results.Count);
    }

    private sealed class FakeCollection : IStoreCollection
    {
        private readonly IReadOnlyCollection<IStoreItem> items;

        public FakeCollection(Uri uri, IReadOnlyCollection<IStoreItem>? items = null)
        {
            Uri = uri;
            this.items = items ?? Array.Empty<IStoreItem>();
        }

        public Uri Uri { get; }

        public Task<Stream> GetReadableStreamAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DavStatusCode> WriteDataAsync(Stream stream, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ItemResult> CopyAsync(
            IStoreCollection destination,
            string name,
            bool overwrite,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IStoreItem?> GetItemAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<IStoreItem>> GetItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(items);

        public Task<CollectionResult> CreateCollectionAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ItemResult> CreateItemAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ItemResult> MoveItemAsync(
            string name,
            IStoreCollection destination,
            string destinationName,
            bool overwrite,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DavStatusCode> DeleteItemAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
