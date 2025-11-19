using System.CommandLine;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Gallery;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Cli.List;

/// <summary>
/// イメージバージョン一覧表示コマンド
/// </summary>
public class ListVersionsCommand
{
    private readonly IGalleryClientFactory _clientFactory;
    private readonly IGalleryQueryService _queryService;
    private readonly ILogger<ListVersionsCommand> _logger;

    /// <summary>
    /// ListVersionsCommandのコンストラクタ
    /// </summary>
    public ListVersionsCommand(
        IGalleryClientFactory clientFactory,
        IGalleryQueryService queryService,
        ILogger<ListVersionsCommand> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(queryService);
        ArgumentNullException.ThrowIfNull(logger);

        _clientFactory = clientFactory;
        _queryService = queryService;
        _logger = logger;
    }

    /// <summary>
    /// コマンド定義を作成
    /// </summary>
    public Command CreateCommand()
    {
        var command = new Command("versions", "List all versions of a specific image definition");

        // サブスクリプションオプション
        var subscriptionOption = new Option<string>("--subscription", "-s")
        {
            Description = "Subscription ID",
            Required = true
        };

        // リソースグループオプション
        var resourceGroupOption = new Option<string>("--resource-group", "-rg")
        {
            Description = "Resource group name",
            Required = true
        };

        // ギャラリー名オプション
        var galleryOption = new Option<string>("--gallery", "-g")
        {
            Description = "Gallery name",
            Required = true
        };

        // イメージ定義オプション
        var imageOption = new Option<string>("--image", "-i")
        {
            Description = "Image definition name",
            Required = true
        };

        // 認証オプション
        var tenantIdOption = new Option<string?>("--tenant-id", "-t")
        {
            Description = "Azure tenant ID (optional, can be read from config or environment)",
            Required = false
        };

        // オプションをコマンドに追加
        command.Add(subscriptionOption);
        command.Add(resourceGroupOption);
        command.Add(galleryOption);
        command.Add(imageOption);
        command.Add(tenantIdOption);

        // ハンドラーを設定
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var subscription = parseResult.GetValue(subscriptionOption) ?? "";
            var resourceGroup = parseResult.GetValue(resourceGroupOption) ?? "";
            var gallery = parseResult.GetValue(galleryOption) ?? "";
            var image = parseResult.GetValue(imageOption) ?? "";
            var tenantId = parseResult.GetValue(tenantIdOption);

            await ExecuteAsync(subscription, resourceGroup, gallery, image, tenantId);
        });

        return command;
    }

    /// <summary>
    /// コマンド実行
    /// </summary>
    private async Task ExecuteAsync(
        string subscription,
        string resourceGroup,
        string gallery,
        string image,
        string? tenantId)
    {
        try
        {
            _logger.LogInformation("Listing versions for image '{Image}' in gallery '{Gallery}' (subscription: {Subscription}, resource group: {ResourceGroup})",
                image, gallery, subscription, resourceGroup);

            if (string.IsNullOrEmpty(tenantId))
            {
                throw new InvalidOperationException("Tenant ID is required (via --tenant-id, environment, or config)");
            }

            // コンテキストを構築
            var context = new AzureContext
            {
                TenantId = tenantId,
                SubscriptionId = subscription,
                ResourceGroupName = resourceGroup,
                GalleryName = gallery
            };

            // クライアントを作成
            var client = _clientFactory.CreateArmClient(tenantId, subscription);

            // ギャラリーコレクションを取得
            var galleryCollection = _clientFactory.GetGalleryCollection(context, client);
            var galleryResponse = await galleryCollection.GetAsync(gallery);
            var galleryResource = galleryResponse.Value;

            // イメージ定義コレクションを取得
            var imageCollection = galleryResource.GetGalleryImages();
            var imageResponse = await imageCollection.GetAsync(image);
            var imageDefinition = imageResponse.Value;

            // イメージバージョンを列挙
            var imageVersions = await _queryService.EnumerateImageVersionsAsync(imageDefinition);

            // 結果を出力
            PrintImageVersions(imageVersions);

            Environment.Exit(0); // 正常終了
        }
        catch (Exception ex)
        {
            _logger.LogError("List versions command failed: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            Environment.Exit(4); // エラー
        }
    }

    /// <summary>
    /// イメージバージョン一覧を出力
    /// </summary>
    private void PrintImageVersions(List<Azure.ResourceManager.Compute.GalleryImageVersionResource> imageVersions)
    {
        if (imageVersions.Count == 0)
        {
            Console.WriteLine("No image versions found.");
            return;
        }

        Console.WriteLine($"\nFound {imageVersions.Count} image version/versions:");
        Console.WriteLine();

        foreach (var version in imageVersions.OrderBy(v => ExtractNameFromId(v.Id.ToString())))
        {
            var versionName = ExtractNameFromId(version.Id.ToString());
            Console.WriteLine($"  - {versionName}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// ARM ID 文字列から末尾の名前部分を抽出
    /// </summary>
    private string ExtractNameFromId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return id;
        var segments = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? id : segments[^1];
    }
}
