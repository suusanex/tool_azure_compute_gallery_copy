using Azure;
using Azure.Core;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using AzureComputeGalleryCopy.Services.Gallery;
using Moq;
using NUnit.Framework;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Tests.Services.Gallery;

/// <summary>
/// GalleryQueryService のユニットテスト
/// </summary>
[TestFixture]
public class GalleryQueryServiceTests
{
    private Mock<ILogger<GalleryQueryService>> _mockLogger = null!;
    private GalleryQueryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<GalleryQueryService>>();
        _service = new GalleryQueryService(_mockLogger.Object);
    }

    /// <summary>
    /// T022: EnumerateImageDefinitionsAsync は空のリストを正常に返す
    /// </summary>
    [Test]
    public async Task EnumerateImageDefinitionsAsync_WhenGalleryHasNoImages_ReturnsEmptyList()
    {
        // Arrange
        var mockGallery = new Mock<GalleryResource>();
        var galleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery");
        mockGallery.SetupGet(g => g.Id).Returns(galleryId);
        
        var mockImageDefCollection = new Mock<GalleryImageCollection>();

        // モックを設定：非同期列挙が空を返す
        var imageDefList = new List<GalleryImageResource>();
        mockImageDefCollection.Setup(c => c.GetAllAsync())
            .Returns(new TestAsyncPageable<GalleryImageResource>(imageDefList));

        mockGallery.Setup(g => g.GetGalleryImages())
            .Returns(mockImageDefCollection.Object);

        // Act
        var result = await _service.EnumerateImageDefinitionsAsync(mockGallery.Object);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.Empty);
    }

    /// <summary>
    /// T022: EnumerateImageVersionsAsync は複数バージョンを正常に列挙
    /// </summary>
    [Test]
    public async Task EnumerateImageVersionsAsync_WhenImageHasMultipleVersions_ReturnsAllVersions()
    {
        // Arrange
        var mockImageDef = new Mock<GalleryImageResource>();
        var imageId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery/images/test-image");
        mockImageDef.SetupGet(i => i.Id).Returns(imageId);
        
        var mockVersionCollection = new Mock<GalleryImageVersionCollection>();

        // モック版オブジェクト
        var version1 = CreateMockImageVersion("1.0.0");
        var version2 = CreateMockImageVersion("1.0.1");
        var version3 = CreateMockImageVersion("1.1.0");
        var versionList = new List<GalleryImageVersionResource> { version1.Object, version2.Object, version3.Object };

        mockVersionCollection.Setup(c => c.GetAllAsync())
            .Returns(new TestAsyncPageable<GalleryImageVersionResource>(versionList));

        mockImageDef.Setup(i => i.GetGalleryImageVersions())
            .Returns(mockVersionCollection.Object);

        // Act
        var result = await _service.EnumerateImageVersionsAsync(mockImageDef.Object);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[0].Id.ToString(), Does.EndWith("/1.0.0"));
        Assert.That(result[1].Id.ToString(), Does.EndWith("/1.0.1"));
        Assert.That(result[2].Id.ToString(), Does.EndWith("/1.1.0"));
    }

    /// <summary>
    /// T022: ImageDefinitionExistsAsync は存在チェックを正常に実行
    /// </summary>
    [Test]
    public async Task ImageDefinitionExistsAsync_WhenImageExists_ReturnsTrue()
    {
        // Arrange
        var mockGallery = new Mock<GalleryResource>();
        var mockImageDefCollection = new Mock<GalleryImageCollection>();

        var mockResponse = new Mock<Response<bool>>();
        mockResponse.Setup(r => r.Value).Returns(true);
        mockImageDefCollection
            .Setup(c => c.ExistsAsync("ubuntu-2204", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse.Object);

        mockGallery.Setup(g => g.GetGalleryImages())
            .Returns(mockImageDefCollection.Object);

        // Act
        var result = await _service.ImageDefinitionExistsAsync(mockGallery.Object, "ubuntu-2204");

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// T022: ImageVersionExistsAsync は未存在を正常に検出
    /// </summary>
    [Test]
    public async Task ImageVersionExistsAsync_WhenVersionDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var mockImageDef = new Mock<GalleryImageResource>();
        var mockVersionCollection = new Mock<GalleryImageVersionCollection>();

        var mockResponse = new Mock<Response<bool>>();
        mockResponse.Setup(r => r.Value).Returns(false);
        mockVersionCollection
            .Setup(c => c.ExistsAsync("1.0.0", default))
            .ReturnsAsync(mockResponse.Object);

        mockImageDef.Setup(i => i.GetGalleryImageVersions())
            .Returns(mockVersionCollection.Object);

        // Act
        var result = await _service.ImageVersionExistsAsync(mockImageDef.Object, "1.0.0");

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// T022: EnumerateAllImagesAndVersionsAsync は複合結果を正常に返す
    /// </summary>
    [Test]
    public async Task EnumerateAllImagesAndVersionsAsync_WithMultipleImagesAndVersions_ReturnsCompleteStructure()
    {
        // Arrange
        var mockGallery = new Mock<GalleryResource>();
        var galleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery");
        mockGallery.SetupGet(g => g.Id).Returns(galleryId);
        
        var mockImageDefCollection = new Mock<GalleryImageCollection>();

        // イメージ定義を作成
        var imageDef1 = CreateMockImageDefinition("ubuntu-2204");
        var imageDef2 = CreateMockImageDefinition("windows-server-2022");

        var imageDefList = new List<GalleryImageResource> { imageDef1.Object, imageDef2.Object };
        mockImageDefCollection.Setup(c => c.GetAllAsync())
            .Returns(new TestAsyncPageable<GalleryImageResource>(imageDefList));

        // バージョンを作成
        var version1 = CreateMockImageVersion("1.0.0");
        var version2 = CreateMockImageVersion("1.0.1");
        var versionList = new List<GalleryImageVersionResource> { version1.Object, version2.Object };

        // バージョン取得をモック
        var mockVersionCollection = new Mock<GalleryImageVersionCollection>();
        mockVersionCollection.Setup(c => c.GetAllAsync())
            .Returns(new TestAsyncPageable<GalleryImageVersionResource>(versionList));

        imageDef1.Setup(i => i.GetGalleryImageVersions())
            .Returns(mockVersionCollection.Object);
        imageDef2.Setup(i => i.GetGalleryImageVersions())
            .Returns(mockVersionCollection.Object);

        mockGallery.Setup(g => g.GetGalleryImages())
            .Returns(mockImageDefCollection.Object);

        // Act
        var result = await _service.EnumerateAllImagesAndVersionsAsync(mockGallery.Object);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[imageDef1.Object].Count, Is.EqualTo(2));
        Assert.That(result[imageDef2.Object].Count, Is.EqualTo(2));
    }

    // ヘルパーメソッド

    private Mock<GalleryImageResource> CreateMockImageDefinition(string name)
    {
        var mock = new Mock<GalleryImageResource>();
        var imageId = new ResourceIdentifier($"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery/images/{name}");
        mock.SetupGet(i => i.Id).Returns(imageId);
        return mock;
    }

    private Mock<GalleryImageVersionResource> CreateMockImageVersion(string name)
    {
        var mock = new Mock<GalleryImageVersionResource>();
        var versionId = new ResourceIdentifier($"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery/images/someimage/versions/{name}");
        mock.SetupGet(v => v.Id).Returns(versionId);
        return mock;
    }

    // 旧実装の非同期列挙ヘルパーは不要となったため削除（SDKメソッド GetAllAsync を直接モック）
}

/// <summary>
/// 非同期列挙モック用の拡張メソッド
/// </summary>
internal static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return await Task.FromResult(item);
        }
    }
}

/// <summary>
/// 単一ページの AsyncPageable 実装（テスト用）
/// </summary>
internal class TestAsyncPageable<T> : AsyncPageable<T> where T : notnull
{
    private readonly IReadOnlyList<T> _items;
    public TestAsyncPageable(IEnumerable<T> items) => _items = items.ToList();

    public override async IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
    {
        var mockResponse = new Mock<Response>();
        mockResponse.Setup(r => r.Status).Returns(200);
        var page = Page<T>.FromValues(_items, null, mockResponse.Object);
        yield return await Task.FromResult(page);
    }
}


