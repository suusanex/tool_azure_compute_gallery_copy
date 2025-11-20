using System.CommandLine;
using AzureComputeGalleryCopy.Models;
using AzureComputeGalleryCopy.Validation;
using AzureComputeGalleryCopy.Configuration;
using AzureComputeGalleryCopy.Services.Gallery;
using Microsoft.Extensions.Logging;

namespace AzureComputeGalleryCopy.Cli.Validate;

/// <summary>
/// 設定検証コマンド
/// </summary>
public class ValidateCommand
{
    private readonly IConfigurationLoader _configurationLoader;
    private readonly IConfigurationValidator _configurationValidator;
    private readonly IGalleryClientFactory _clientFactory;
    private readonly ILogger<ValidateCommand> _logger;

    /// <summary>
    /// ValidateCommandのコンストラクタ
    /// </summary>
    public ValidateCommand(
        IConfigurationLoader configurationLoader,
        IConfigurationValidator configurationValidator,
        IGalleryClientFactory clientFactory,
        ILogger<ValidateCommand> logger)
    {
        ArgumentNullException.ThrowIfNull(configurationLoader);
        ArgumentNullException.ThrowIfNull(configurationValidator);
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _configurationLoader = configurationLoader;
        _configurationValidator = configurationValidator;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <summary>
    /// コマンド定義を作成
    /// </summary>
    public Command CreateCommand()
    {
        var command = new Command("validate", "Validate configuration and connectivity to Azure resources");

        // 設定ファイルオプション（オプション）
        var configFileOption = new Option<string?>("--config", "-c")
        {
            Description = "Configuration file path (optional, can be read from environment)",
            Required = false
        };

        // オプションをコマンドに追加
        command.Add(configFileOption);

        // ハンドラーを設定
        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var configFile = parseResult.GetValue(configFileOption);
            await ExecuteAsync(configFile);
        });

        return command;
    }

    /// <summary>
    /// コマンド実行
    /// </summary>
    private async Task ExecuteAsync(string? configFile)
    {
        try
        {
            _logger.LogInformation("Starting configuration validation");

            // 設定を読み込む
            var configuration = await _configurationLoader.LoadAsync();

            // 設定をバリデーション
            var validationErrors = await _configurationValidator.ValidateAsync(configuration);

            if (validationErrors.Count > 0)
            {
                _logger.LogError("Configuration validation failed with {ErrorCount} error(s)", validationErrors.Count);

                Console.WriteLine("\n❌ Configuration validation failed:");
                Console.WriteLine();

                foreach (var error in validationErrors)
                {
                    Console.WriteLine($"  ❌ {error}");
                    Console.WriteLine();
                }

                Environment.Exit(2); // 検証エラー
            }

            // 接続テスト（オプション）
            await TestConnectivityAsync(configuration);

            Console.WriteLine("\n✅ Configuration validation passed successfully!");
            Console.WriteLine();
            Environment.Exit(0); // 正常終了
        }
        catch (Exception ex)
        {
            _logger.LogError("Validation command failed: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
            Console.WriteLine($"\n❌ Validation failed: {ex.Message}");
            Environment.Exit(4); // エラー
        }
    }

    /// <summary>
    /// Azure リソースへの接続をテスト
    /// </summary>
    private async Task TestConnectivityAsync(ToolConfiguration configuration)
    {
        try
        {
            Console.WriteLine("Testing connectivity to Azure resources...\n");

            if (configuration.Source == null || configuration.Target == null || configuration.Authentication == null)
            {
                throw new InvalidOperationException("Configuration is incomplete");
            }

            var tenantId = configuration.Source.TenantId;
            var sourceSubscription = configuration.Source.SubscriptionId;
            var targetSubscription = configuration.Target.SubscriptionId;

            // ソースクライアントを作成
            _logger.LogInformation("Testing source subscription connection");
            Console.Write("  Testing source subscription connection... ");
            var sourceClient = _clientFactory.CreateArmClient(tenantId, sourceSubscription);
            var sourceContext = new AzureContext
            {
                TenantId = tenantId,
                SubscriptionId = sourceSubscription,
                ResourceGroupName = configuration.Source.ResourceGroupName,
                GalleryName = configuration.Source.GalleryName
            };
            var sourceGalleryCollection = _clientFactory.GetGalleryCollection(sourceContext, sourceClient);
            await sourceGalleryCollection.GetAsync(configuration.Source.GalleryName);
            Console.WriteLine("✅");

            // ターゲットクライアントを作成
            _logger.LogInformation("Testing target subscription connection");
            Console.Write("  Testing target subscription connection... ");
            var targetClient = _clientFactory.CreateArmClient(tenantId, targetSubscription);
            var targetContext = new AzureContext
            {
                TenantId = tenantId,
                SubscriptionId = targetSubscription,
                ResourceGroupName = configuration.Target.ResourceGroupName,
                GalleryName = configuration.Target.GalleryName
            };
            var targetGalleryCollection = _clientFactory.GetGalleryCollection(targetContext, targetClient);
            await targetGalleryCollection.GetAsync(configuration.Target.GalleryName);
            Console.WriteLine("✅");

            Console.WriteLine();
            _logger.LogInformation("Connectivity test passed");
        }
        catch (Exception ex)
        {
            _logger.LogError("Connectivity test failed: {Message}", ex.Message);
            throw;
        }
    }
}
