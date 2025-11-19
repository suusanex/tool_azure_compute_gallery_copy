// Entry point for Azure Compute Gallery Copy CLI tool
// エントリーポイント: Azure Compute Gallery クロスサブスクリプション コピー ツール

using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AzureComputeGalleryCopy.Configuration;
using AzureComputeGalleryCopy.Logging;
using AzureComputeGalleryCopy.Services.Authentication;
using AzureComputeGalleryCopy.Services.Filtering;
using AzureComputeGalleryCopy.Validation;
using AzureComputeGalleryCopy.Services.Gallery;
using AzureComputeGalleryCopy.Cli;
using AzureComputeGalleryCopy.Cli.Output;
using AzureComputeGalleryCopy.Cli.List;
using AzureComputeGalleryCopy.Cli.Validate;
using Azure.Core;
using Azure.Identity;

namespace AzureComputeGalleryCopy;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            // 設定を読み込み
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables("ACG_COPY_");

            var configuration = configBuilder.Build();

            // 設定ローダーで設定を取得
            var configLoader = new ConfigurationLoader(configuration);
            var toolConfig = await configLoader.LoadAsync();

            // バリデーション
            var validator = new ConfigurationValidator();
            var validationErrors = await validator.ValidateAsync(toolConfig);
            if (validationErrors.Count > 0)
            {
                Console.Error.WriteLine("[ERROR] Configuration validation failed");
                foreach (var error in validationErrors)
                {
                    Console.Error.WriteLine($"  {error}");
                }
                return 3; // 設定エラー
            }

            // ロギングレベルを解析
            var logLevel = LogLevelExtensions.ParseLogLevel(toolConfig.LogLevel);

            // ロガーファクトリを構築
            var loggerFactoryBuilder = new LoggerFactoryBuilder();
            var loggerFactory = loggerFactoryBuilder.Build(logLevel);
            var logger = loggerFactory.CreateLogger<Program>();
            var operationLogger = new OperationLogger(loggerFactory.CreateLogger<OperationLogger>());

            logger.LogInformation("Azure Compute Gallery Copy Tool started");

            var configMetadata = new Dictionary<string, string>
            {
                { "LogLevel", toolConfig.LogLevel },
                { "DryRun", toolConfig.DryRun.ToString() }
            };

            operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.ConfigLoadSuccess,
                "Configuration loaded successfully",
                LogLevel.Information,
                metadata: configMetadata);
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton(toolConfig);
            services.AddSingleton<IConfigurationLoader>(new ConfigurationLoader(configuration));
            services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
            services.AddSingleton<IAuthenticator>(_ => 
                new WebView2Authenticator(toolConfig.Authentication));
            
            // Azure Resource Manager API用のTokenCredentialを登録
            // スコープ: https://management.azure.com/.default
            // InteractiveBrowserCredentialはWebView2を使用（Windows 10以降）
            services.AddSingleton<TokenCredential>(_ => 
                new InteractiveBrowserCredential(new Azure.Identity.InteractiveBrowserCredentialOptions
                {
                    TenantId = toolConfig.Authentication.TenantId,
                    ClientId = toolConfig.Authentication.ClientId,
                    // ローカルホストリダイレクトURI（標準的なパブリッククライアントアプリ設定）
                    RedirectUri = new Uri("http://localhost")
                }));
            services.AddSingleton<ILoggerFactoryBuilder, LoggerFactoryBuilder>();
            
            // T025: Filter matcher service
            services.AddSingleton<IFilterMatcher, FilterMatcher>();
            
            // T037: Operation logger
            services.AddSingleton<IOperationLogger, OperationLogger>();
            
            // T020, T021: Gallery services DI登録
            services.AddSingleton<IGalleryClientFactory, GalleryClientFactory>();
            services.AddSingleton<IGalleryQueryService, GalleryQueryService>();
            services.AddSingleton<IGalleryCopyService, GalleryCopyService>();
            services.AddSingleton<CopyCommand>();
            services.AddSingleton<SummaryPrinter>();
            services.AddSingleton<DryRunPrinter>();
            
            // T033-T036: List and validate commands
            services.AddSingleton<ListGalleriesCommand>();
            services.AddSingleton<ListImagesCommand>();
            services.AddSingleton<ListVersionsCommand>();
            services.AddSingleton<ValidateCommand>();

            var serviceProvider = services.BuildServiceProvider();

            logger.LogInformation("Azure Compute Gallery Copy Tool started");

            // T020: CLI root command setup
            var rootCommand = new RootCommand("Azure Compute Gallery Cross-Subscription Copy Tool");
            
            // T039: Version option
            var versionOption = AzureComputeGalleryCopy.Cli.VersionOption.CreateOption();
            rootCommand.Add(versionOption);
            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                // バージョン表示要求のみの場合は終了
                if (parseResult.GetValue(versionOption))
                {
                    AzureComputeGalleryCopy.Cli.VersionOption.PrintAndExit();
                }

                // ルートコマンド単体実行（サブコマンドなし）の場合は設定ファイルに基づくデフォルトコピーを実行
                // FR-007: 設定ファイル・環境変数・CLI 引数の上書き可能性を満たすため、
                // 引数未指定時は設定ファイルを用いてコピー（DryRunも反映）を行う。
                if (args.Length == 0)
                {
                    logger.LogInformation("No subcommand specified. Executing configuration-based copy (DryRun: {DryRun})", toolConfig.DryRun);
                    var exitCode = await ExecuteConfigurationCopyAsync(serviceProvider, toolConfig, logger);
                    Environment.Exit(exitCode);
                }
            });

            // Global options
            var configOption = new Option<string?>("--config", "-c")
            {
                Description = "Path to configuration file (default: appsettings.json)",
                Required = false
            };
            
            var logLevelOption = new Option<string?>("--log-level", "-l")
            {
                Description = "Log level (Trace, Debug, Information, Warning, Error, Critical)",
                Required = false
            };

            rootCommand.Add(configOption);
            rootCommand.Add(logLevelOption);

            // T019: Register copy command
            var copyCommandHandler = serviceProvider.GetRequiredService<CopyCommand>();
            rootCommand.Add(copyCommandHandler.CreateCommand());

            // T033-T036: Register list and validate subcommands
            var listCommand = new Command("list", "List galleries, images, or versions");
            var listGalleriesCommand = serviceProvider.GetRequiredService<ListGalleriesCommand>();
            var listImagesCommand = serviceProvider.GetRequiredService<ListImagesCommand>();
            var listVersionsCommand = serviceProvider.GetRequiredService<ListVersionsCommand>();
            
            listCommand.Add(listGalleriesCommand.CreateCommand());
            listCommand.Add(listImagesCommand.CreateCommand());
            listCommand.Add(listVersionsCommand.CreateCommand());
            
            rootCommand.Add(listCommand);

            var validateCommand = serviceProvider.GetRequiredService<ValidateCommand>();
            rootCommand.Add(validateCommand.CreateCommand());

            logger.LogInformation("CLI root command initialized with all subcommands");

            // Execute the command (System.CommandLine beta pattern: Parse -> InvokeAsync)
            return await rootCommand.Parse(args).InvokeAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Unexpected error: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    /// 設定ファイルベースでコピー（またはドライラン）を実行するデフォルト動作
    /// </summary>
    /// <param name="serviceProvider">DIコンテナ</param>
    /// <param name="toolConfig">ツール設定</param>
    /// <param name="logger">プログラムロガー</param>
    /// <returns>終了コード</returns>
    private static async Task<int> ExecuteConfigurationCopyAsync(IServiceProvider serviceProvider, Models.ToolConfiguration toolConfig, ILogger logger)
    {
        try
        {
            // 必須項目の簡易チェック（設定バリデーションは既に Main 冒頭で済）
            if (string.IsNullOrWhiteSpace(toolConfig.Authentication.TenantId))
            {
                logger.LogError("TenantId is missing in configuration");
                return 3; // 設定エラー
            }

            var operationLogger = serviceProvider.GetRequiredService<IOperationLogger>();
            var clientFactory = serviceProvider.GetRequiredService<Services.Gallery.IGalleryClientFactory>();
            var copyService = serviceProvider.GetRequiredService<Services.Gallery.IGalleryCopyService>();
            var summaryPrinter = serviceProvider.GetRequiredService<Cli.Output.SummaryPrinter>();
            var dryRunPrinter = serviceProvider.GetRequiredService<Cli.Output.DryRunPrinter>();

            logger.LogInformation("Starting configuration-based copy (DryRun: {DryRun})", toolConfig.DryRun);

            // ARMクライアント作成
            var sourceClient = clientFactory.CreateArmClient(toolConfig.Authentication.TenantId, toolConfig.Source.SubscriptionId);
            var targetClient = clientFactory.CreateArmClient(toolConfig.Authentication.TenantId, toolConfig.Target.SubscriptionId);

            var clientMetadata = new Dictionary<string, string>
            {
                { "SourceSubscription", toolConfig.Source.SubscriptionId },
                { "TargetSubscription", toolConfig.Target.SubscriptionId }
            };

            operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.AuthenticationSuccess,
                "ARM clients created for source and target",
                LogLevel.Information,
                metadata: clientMetadata);

            // ギャラリー取得
            var sourceGalleryCollection = clientFactory.GetGalleryCollection(toolConfig.Source, sourceClient);
            var targetGalleryCollection = clientFactory.GetGalleryCollection(toolConfig.Target, targetClient);
            
            logger.LogInformation("Retrieving source gallery: {SourceGallery}", toolConfig.Source.GalleryName);
            var sourceGalleryResponse = await sourceGalleryCollection.GetAsync(toolConfig.Source.GalleryName);
            var sourceGalleryResource = sourceGalleryResponse.Value;

            // レスポンスから HTTP ステータスコードを取得
            var sourceGalleryHttpStatus = sourceGalleryResponse.GetRawResponse()?.Status.ToString() ?? "Unknown";

            var sourceGalleryMetadata = new Dictionary<string, string>
            {
                { "ResourceId", sourceGalleryResource.Id.ToString() },
                { "GalleryName", toolConfig.Source.GalleryName },
                { "ResourceGroup", toolConfig.Source.ResourceGroupName },
                { "HttpStatus", sourceGalleryHttpStatus },
                { "ErrorCode", "None" }
            };

            operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Retrieved source gallery: {toolConfig.Source.GalleryName}",
                LogLevel.Information,
                metadata: sourceGalleryMetadata);

            logger.LogInformation("Retrieving target gallery: {TargetGallery}", toolConfig.Target.GalleryName);
            var targetGalleryResponse = await targetGalleryCollection.GetAsync(toolConfig.Target.GalleryName);
            var targetGalleryResource = targetGalleryResponse.Value;

            // レスポンスから HTTP ステータスコードを取得
            var targetGalleryHttpStatus = targetGalleryResponse.GetRawResponse()?.Status.ToString() ?? "Unknown";

            var targetGalleryMetadata = new Dictionary<string, string>
            {
                { "ResourceId", targetGalleryResource.Id.ToString() },
                { "GalleryName", toolConfig.Target.GalleryName },
                { "ResourceGroup", toolConfig.Target.ResourceGroupName },
                { "HttpStatus", targetGalleryHttpStatus },
                { "ErrorCode", "None" }
            };

            operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                OperationLogger.OperationCode.QueryGallerySuccess,
                $"Retrieved target gallery: {toolConfig.Target.GalleryName}",
                LogLevel.Information,
                metadata: targetGalleryMetadata);

            logger.LogInformation("Source gallery: {SourceGallery}", toolConfig.Source.GalleryName);
            logger.LogInformation("Target gallery: {TargetGallery}", toolConfig.Target.GalleryName);

            // コピー（ドライラン設定反映）
            var summary = await copyService.CopyAllImagesAsync(
                sourceGalleryResource,
                targetGalleryResource,
                toolConfig.Source,
                toolConfig.Target,
                toolConfig.Filter,
                toolConfig.DryRun);

            if (toolConfig.DryRun)
            {
                dryRunPrinter.PrintDryRunPlan(summary);
                return dryRunPrinter.DetermineDryRunExitCode(summary);
            }
            else
            {
                summaryPrinter.PrintSummary(summary);
                return summaryPrinter.DetermineExitCode(summary);
            }
        }
        catch (Azure.RequestFailedException ex)
        {
            logger.LogError("Configuration-based copy failed: Status={Status}, ErrorCode={ErrorCode}, Message={Message}",
                ex.Status, ex.ErrorCode, ex.Message);

            var operationLogger = serviceProvider.GetRequiredService<IOperationLogger>();
            var errorMetadata = new Dictionary<string, string>
            {
                { "ErrorType", "RequestFailedException" },
                { "HttpStatus", ex.Status.ToString() },
                { "ErrorCode", ex.ErrorCode ?? "Unknown" },
                { "Message", ex.Message }
            };

            operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                "CONFIG_COPY_FAILED",
                $"Configuration-based copy failed: {ex.Message}",
                LogLevel.Error,
                ex,
                errorMetadata);

            return 4; // 実行時エラー
        }
        catch (Exception ex)
        {
            logger.LogError("Configuration-based copy failed: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);

            var operationLogger = serviceProvider.GetRequiredService<IOperationLogger>();
            var errorMetadata = new Dictionary<string, string>
            {
                { "ErrorType", ex.GetType().Name },
                { "Message", ex.Message }
            };

            operationLogger.LogOperationEvent(
                Guid.NewGuid().ToString(),
                "CONFIG_COPY_FAILED",
                $"Configuration-based copy failed: {ex.Message}",
                LogLevel.Error,
                ex,
                errorMetadata);

            return 4; // 実行時エラー
        }
    }
}
