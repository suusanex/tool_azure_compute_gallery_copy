using Azure;
using Azure.Core;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Filtering;
using AzureComputeGalleryCopy.Services.Gallery;
using AzureComputeGalleryCopy.Logging;
using Moq;
using NUnit.Framework;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Tests.Services.Gallery;

/// <summary>
/// GalleryCopyService のユニットテスト
/// </summary>
[TestFixture]
public class GalleryCopyServiceTests
{
    private Mock<IGalleryQueryService> _mockQueryService = null!;
    private Mock<IFilterMatcher> _mockFilterMatcher = null!;
    private Mock<ILogger<GalleryCopyService>> _mockLogger = null!;
    private Mock<IOperationLogger> _mockOperationLogger = null!;
    private GalleryCopyService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _mockQueryService = new Mock<IGalleryQueryService>();
        _mockFilterMatcher = new Mock<IFilterMatcher>();
        _mockLogger = new Mock<ILogger<GalleryCopyService>>();
        _mockOperationLogger = new Mock<IOperationLogger>();
        
        // デフォルトではすべてのフィルタマッチングを許可
        _mockFilterMatcher
            .Setup(f => f.MatchesImageDefinition(It.IsAny<string>(), It.IsAny<FilterCriteria>()))
            .Returns(true);
        _mockFilterMatcher
            .Setup(f => f.MatchesVersion(It.IsAny<string>(), It.IsAny<FilterCriteria>()))
            .Returns(true);
        
        _service = new GalleryCopyService(_mockQueryService.Object, _mockFilterMatcher.Object, _mockLogger.Object, _mockOperationLogger.Object);
    }

    /// <summary>
    /// T023: CopyAllImagesAsync は空のギャラリーでも成功
    /// </summary>
    [Test]
    public async Task CopyAllImagesAsync_WhenSourceGalleryIsEmpty_ReturnsEmptySummary()
    {
        // Arrange
        var sourceContext = new AzureContext
        {
            TenantId = "tenant-1",
            SubscriptionId = "source-sub",
            ResourceGroupName = "source-rg",
            GalleryName = "source-gallery"
        };

        var targetContext = new AzureContext
        {
            TenantId = "tenant-1",
            SubscriptionId = "target-sub",
            ResourceGroupName = "target-rg",
            GalleryName = "target-gallery"
        };

        var sourceGallery = new Mock<GalleryResource>();
        var targetGallery = new Mock<GalleryResource>();

        // 空のギャラリーをシミュレート
        _mockQueryService
            .Setup(q => q.EnumerateAllImagesAndVersionsAsync(It.IsAny<GalleryResource>()))
            .ReturnsAsync(new Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>());

        // Act
        var summary = await _service.CopyAllImagesAsync(
            sourceGallery.Object,
            targetGallery.Object,
            sourceContext,
            targetContext,
            isDryRun: false);

        // Assert
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.CreatedImageDefinitions, Is.EqualTo(0));
        Assert.That(summary.CreatedImageVersions, Is.EqualTo(0));
        Assert.That(summary.FailedOperations, Is.EqualTo(0));
    }

    /// <summary>
    /// T023: CopyAllImagesAsync は冪等性を保証（既存バージョンはスキップ）
    /// </summary>
    [Test]
    public async Task CopyAllImagesAsync_WhenVersionExists_SkipsExistingVersion()
    {
        // Arrange
        var sourceContext = CreateSourceContext();
        var targetContext = CreateTargetContext();

        var sourceGallery = new Mock<GalleryResource>();
        var targetGallery = new Mock<GalleryResource>();

        // ギャラリーデータをセットアップ
        var sourceGalleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/source-gallery");
        sourceGallery.SetupGet(g => g.Id).Returns(sourceGalleryId);

        var targetGalleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/target-gallery");
        targetGallery.SetupGet(g => g.Id).Returns(targetGalleryId);

        // イメージ定義とバージョンを作成
        var imageDef = CreateMockImageDefinition("ubuntu-2204");
        var version = CreateMockImageVersion("1.0.0");

        var imageDefDict = new Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>
        {
            { imageDef.Object, new List<GalleryImageVersionResource> { version.Object } }
        };

        _mockQueryService
            .Setup(q => q.EnumerateAllImagesAndVersionsAsync(sourceGallery.Object))
            .ReturnsAsync(imageDefDict);

        // QueryService による存在チェック（サービス内はIGalleryQueryService経由で確認）
        _mockQueryService
            .Setup(q => q.ImageDefinitionExistsAsync(targetGallery.Object, "ubuntu-2204"))
            .ReturnsAsync(true);

        // ターゲット側で既存チェック
        var mockTargetImageDefCollection = new Mock<GalleryImageCollection>();
        var mockExistsResponse1 = new Mock<Response<bool>>();
        mockExistsResponse1.Setup(r => r.Value).Returns(true);
        mockTargetImageDefCollection
            .Setup(c => c.ExistsAsync("ubuntu-2204", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse1.Object);
        targetGallery.Setup(g => g.GetGalleryImages())
            .Returns(mockTargetImageDefCollection.Object);

        var targetImageDef = CreateMockImageDefinition("ubuntu-2204");
        var mockResponse = new Mock<Response>();
        mockResponse.Setup(r => r.Status).Returns(200);
        mockTargetImageDefCollection
            .Setup(c => c.GetAsync("ubuntu-2204", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(targetImageDef.Object, mockResponse.Object));

        // バージョン既存チェック
        var mockTargetVersionCollection = new Mock<GalleryImageVersionCollection>();
        var mockExistsResponse2 = new Mock<Response<bool>>();
        mockExistsResponse2.Setup(r => r.Value).Returns(true);
        mockTargetVersionCollection
            .Setup(c => c.ExistsAsync("1.0.0", default))
            .ReturnsAsync(mockExistsResponse2.Object);

        targetImageDef.Setup(i => i.GetGalleryImageVersions())
            .Returns(mockTargetVersionCollection.Object);

        // バージョンの存在はIGalleryQueryService経由でチェックされる
        _mockQueryService
            .Setup(q => q.ImageVersionExistsAsync(It.IsAny<GalleryImageResource>(), "1.0.0"))
            .ReturnsAsync(true);

        // Act
        var summary = await _service.CopyAllImagesAsync(
            sourceGallery.Object,
            targetGallery.Object,
            sourceContext,
            targetContext,
            isDryRun: false);

        // Assert
        Assert.That(summary.CreatedImageVersions, Is.EqualTo(0));
        Assert.That(summary.SkippedImageVersions, Is.GreaterThan(0));
    }

    /// <summary>
    /// T023 (更新): 属性不整合は制限事項 L-001 により事前検証されず失敗しない
    /// </summary>
    [Test]
    public async Task CopyAllImagesAsync_ImageAttributesMismatch_IsNotValidated_PreValidationSkipped()
    {
        var sourceContext = CreateSourceContext();
        var targetContext = CreateTargetContext();
        var sourceGallery = new Mock<GalleryResource>();
        var targetGallery = new Mock<GalleryResource>();
        
        var sourceGalleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/source-gallery"); sourceGallery.SetupGet(g => g.Id).Returns(sourceGalleryId);
        var targetGalleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/target-gallery"); targetGallery.SetupGet(g => g.Id).Returns(targetGalleryId);

        var imageDef = CreateMockImageDefinition("ubuntu-2204");
        var dict = new Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>
        {
            { imageDef.Object, new List<GalleryImageVersionResource>() }
        };
        _mockQueryService.Setup(q => q.EnumerateAllImagesAndVersionsAsync(sourceGallery.Object))
            .ReturnsAsync(dict);

        var mockTargetImageDefCollection = new Mock<GalleryImageCollection>();
        var mockExistsResponse = new Mock<Response<bool>>();
        mockExistsResponse.Setup(r => r.Value).Returns(false);
        mockTargetImageDefCollection.Setup(c => c.ExistsAsync("ubuntu-2204", It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockExistsResponse.Object);
        targetGallery.Setup(g => g.GetGalleryImages()).Returns(mockTargetImageDefCollection.Object);

        var summary = await _service.CopyAllImagesAsync(
            sourceGallery.Object,
            targetGallery.Object,
            sourceContext,
            targetContext,
            isDryRun: true);

        Assert.That(summary.FailedOperations, Is.EqualTo(0));
    }

    /// <summary>
    /// T023: CopyAllImagesAsync はドライランモードで実際の操作を行わない
    /// </summary>
    [Test]
    public async Task CopyAllImagesAsync_InDryRunMode_DoesNotCreateResources()
    {
        // Arrange
        var sourceContext = CreateSourceContext();
        var targetContext = CreateTargetContext();

        var sourceGallery = new Mock<GalleryResource>();
        var targetGallery = new Mock<GalleryResource>();

        var sourceGalleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/source-gallery"); sourceGallery.SetupGet(g => g.Id).Returns(sourceGalleryId);
        var targetGalleryId = new ResourceIdentifier("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/target-gallery"); targetGallery.SetupGet(g => g.Id).Returns(targetGalleryId);

        var imageDef = CreateMockImageDefinition("ubuntu-2204");
        var version = CreateMockImageVersion("1.0.0");

        var imageDefDict = new Dictionary<GalleryImageResource, List<GalleryImageVersionResource>>
        {
            { imageDef.Object, new List<GalleryImageVersionResource> { version.Object } }
        };

        _mockQueryService
            .Setup(q => q.EnumerateAllImagesAndVersionsAsync(sourceGallery.Object))
            .ReturnsAsync(imageDefDict);

        _mockQueryService
            .Setup(q => q.ImageDefinitionExistsAsync(targetGallery.Object, "ubuntu-2204"))
            .ReturnsAsync(false);

        _mockQueryService
            .Setup(q => q.ImageVersionExistsAsync(It.IsAny<GalleryImageResource>(), "1.0.0"))
            .ReturnsAsync(false);

        var mockTargetImageDefCollection = new Mock<GalleryImageCollection>();
        targetGallery.Setup(g => g.GetGalleryImages())
            .Returns(mockTargetImageDefCollection.Object);

        // Act
        var summary = await _service.CopyAllImagesAsync(
            sourceGallery.Object,
            targetGallery.Object,
            sourceContext,
            targetContext,
            isDryRun: true);

        // Assert
        Assert.That(summary.IsDryRun, Is.True);
        // CreateOrUpdateAsync は呼ばれないはず
        mockTargetImageDefCollection.Verify(
            c => c.CreateOrUpdateAsync(It.IsAny<WaitUntil>(), It.IsAny<string>(), It.IsAny<GalleryImageData>()),
            Times.Never);
    }

    // ヘルパーメソッド

    private AzureContext CreateSourceContext() => new()
    {
        TenantId = "tenant-1",
        SubscriptionId = "source-sub",
        ResourceGroupName = "source-rg",
        GalleryName = "source-gallery"
    };

    private AzureContext CreateTargetContext() => new()
    {
        TenantId = "tenant-1",
        SubscriptionId = "target-sub",
        ResourceGroupName = "target-rg",
        GalleryName = "target-gallery"
    };

    private Mock<GalleryImageResource> CreateMockImageDefinition(string name)
    {
        var mock = new Mock<GalleryImageResource>();
        var imageId = new ResourceIdentifier($"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery/images/{name}");
        mock.SetupGet(i => i.Id).Returns(imageId);
        return mock;
    }

    private Mock<GalleryImageResource> CreateMockImageDefinitionWithOsType(
        string name, OperatingSystemType osType)
    {
        var mock = CreateMockImageDefinition(name);
        // mock.Setup(i => i.Data.OsType).Returns(osType);
        return mock;
    }

    private Mock<GalleryImageVersionResource> CreateMockImageVersion(string name)
    {
        var mock = new Mock<GalleryImageVersionResource>();
        var versionId = new ResourceIdentifier($"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/galleries/test-gallery/images/someimage/versions/{name}");
        mock.SetupGet(v => v.Id).Returns(versionId);
        return mock;
    }

    private Mock<GalleryImageVersionCollection> mockVersionCollection =>
        new Mock<GalleryImageVersionCollection>();
}

