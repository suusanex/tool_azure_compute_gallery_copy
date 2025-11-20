using System.CommandLine;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Services.Gallery;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Cli.List;

/// <summary>
/// ギャラリー一覧表示コマンド
/// </summary>
public class ListGalleriesCommand
{
    private readonly IGalleryClientFactory _clientFactory;
    private readonly ILogger<ListGalleriesCommand> _logger;

    /// <summary>
    /// ListGalleriesCommandのコンストラクタ
    /// </summary>
    public ListGalleriesCommand(
        IGalleryClientFactory clientFactory,
        ILogger<ListGalleriesCommand> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <summary>
    /// コマンド定義を作成
    /// </summary>
    public Command CreateCommand()
    {
        var command = new Command("galleries", "List all galleries in a subscription and resource group");

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

        // 認証オプション
        var tenantIdOption = new Option<string>("--tenant-id", "-t")
        {
            Description = "Azure tenant ID (required for authentication)",
            Required = true
        };

        // オプションをコマンドに追加
        command.Add(subscriptionOption);
        command.Add(resourceGroupOption);
        command.Add(tenantIdOption);

        // ハンドラーを設定
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var subscription = parseResult.GetValue(subscriptionOption) ?? "";
            var resourceGroup = parseResult.GetValue(resourceGroupOption) ?? "";
            var tenantId = parseResult.GetValue(tenantIdOption) ?? "";

            await ExecuteAsync(subscription, resourceGroup, tenantId);
        });

        return command;
    }

    /// <summary>
    /// コマンド実行
    /// </summary>
    private async Task ExecuteAsync(
        string subscription,
        string resourceGroup,
        string tenantId)
    {
        try
        {
            _logger.LogInformation("Listing galleries in subscription '{Subscription}' and resource group '{ResourceGroup}'",
                subscription, resourceGroup);

            // tenantIdは必須パラメータとしてCLIで検証済み

            // クライアントを作成
            var client = _clientFactory.CreateArmClient(tenantId, subscription);

            // ギャラリーコレクションを取得
            var context = new AzureContext
            {
                TenantId = tenantId,
                SubscriptionId = subscription,
                ResourceGroupName = resourceGroup,
                GalleryName = string.Empty // リスト操作では不要
            };

            var galleryCollection = _clientFactory.GetGalleryCollection(context, client);

            var galleries = new List<string>();
            await foreach (var gallery in galleryCollection.GetAllAsync())
            {
                var galleryName = ExtractNameFromId(gallery.Id.ToString());
                galleries.Add(galleryName);
                _logger.LogInformation("Found gallery: {GalleryName}", galleryName);
            }

            // 結果を出力
            PrintGalleries(galleries);

            Environment.Exit(0); // 正常終了
        }
        catch (Exception ex)
        {
            _logger.LogError("List galleries command failed: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            Environment.Exit(4); // エラー
        }
    }

    /// <summary>
    /// ギャラリー一覧を出力
    /// </summary>
    private void PrintGalleries(List<string> galleries)
    {
        if (galleries.Count == 0)
        {
            Console.WriteLine("No galleries found.");
            return;
        }

        Console.WriteLine($"\nFound {galleries.Count} gallery/galleries:");
        Console.WriteLine();

        foreach (var gallery in galleries.OrderBy(g => g))
        {
            Console.WriteLine($"  - {gallery}");
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
