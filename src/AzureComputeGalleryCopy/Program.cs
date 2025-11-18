// Entry point for Azure Compute Gallery Copy CLI tool
// エントリーポイント: Azure Compute Gallery クロスサブスクリプション コピー ツール

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AzureComputeGalleryCopy.Configuration;
using AzureComputeGalleryCopy.Logging;
using AzureComputeGalleryCopy.Services.Authentication;
using AzureComputeGalleryCopy.Validation;

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

            // DIコンテナを構築
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(loggerFactory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton(toolConfig);
            services.AddSingleton<IConfigurationLoader>(new ConfigurationLoader(configuration));
            services.AddSingleton<IConfigurationValidator, ConfigurationValidator>();
            services.AddSingleton<IAuthenticator>(_ => 
                new WebView2Authenticator(toolConfig.Authentication));
            services.AddSingleton<ILoggerFactoryBuilder, LoggerFactoryBuilder>();

            var serviceProvider = services.BuildServiceProvider();

            logger.LogInformation("Azure Compute Gallery Copy Tool started");
            logger.LogInformation("Source: {Subscription}/{ResourceGroup}/{Gallery}",
                toolConfig.Source.SubscriptionId, toolConfig.Source.ResourceGroupName,
                toolConfig.Source.GalleryName);
            logger.LogInformation("Target: {Subscription}/{ResourceGroup}/{Gallery}",
                toolConfig.Target.SubscriptionId, toolConfig.Target.ResourceGroupName,
                toolConfig.Target.GalleryName);

            // ヘルプメッセージを表示（CLI実装は Phase 3 以降）
            Console.WriteLine("Azure Compute Gallery Cross-Subscription Copy Tool");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("  acg-copy [command] [options]");
            Console.WriteLine("\nCommands:");
            Console.WriteLine("  copy      - Copy images from source to target gallery");
            Console.WriteLine("  list      - List resources in a gallery");
            Console.WriteLine("  validate  - Validate configuration");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  -c, --config    <path>   Path to configuration file");
            Console.WriteLine("  -l, --log-level <level>  Log level (default: Information)");
            Console.WriteLine("  -h, --help               Show help");
            Console.WriteLine("  -v, --version            Show version");

            logger.LogInformation("CLI root command initialized");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Unexpected error: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
