using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Services.Authentication;

/// <summary>
/// Azureに対する認証を行うサービスのインターフェース
/// </summary>
public interface IAuthenticator
{
    /// <summary>
    /// 認証トークンを取得します。
    /// </summary>
    /// <param name="scopes">要求するスコープ</param>
    /// <returns>認証トークン</returns>
    Task<AccessToken> GetAccessTokenAsync(params string[] scopes);
}

/// <summary>
/// WebView2埋め込みInteractiveBrowserCredentialを使用した認証サービス
/// Windows専用、ブラウザキャッシュに依存しない実装
/// </summary>
public class WebView2Authenticator : IAuthenticator
{
    private readonly AuthenticationConfiguration _authConfig;
    private InteractiveBrowserCredential? _credential;
    private readonly object _credentialLock = new();

    public WebView2Authenticator(AuthenticationConfiguration authConfig)
    {
        _authConfig = authConfig ?? throw new ArgumentNullException(nameof(authConfig));
    }

    /// <inheritdoc />
    public async Task<AccessToken> GetAccessTokenAsync(params string[] scopes)
    {
        if (scopes == null || scopes.Length == 0)
        {
            throw new ArgumentException("At least one scope is required.", nameof(scopes));
        }

        var credential = GetOrCreateCredential();
        
        try
        {
            // TenantId はコンストラクタで設定済み、TokenRequestContext には含めない
            return await credential.GetTokenAsync(
                new TokenRequestContext(scopes));
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException("Authentication was cancelled by the user.");
        }
        catch (AuthenticationFailedException ex)
        {
            throw new InvalidOperationException(
                $"Authentication failed. Ensure you have access to the requested resources. Error: {ex.Message}",
                ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"An unexpected error occurred during authentication. Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// InteractiveBrowserCredentialインスタンスを取得または作成します。
    /// スレッドセーフな実装。
    /// </summary>
    private InteractiveBrowserCredential GetOrCreateCredential()
    {
        if (_credential != null)
        {
            return _credential;
        }

        lock (_credentialLock)
        {
            if (_credential != null)
            {
                return _credential;
            }

            // InteractiveBrowserCredentialをWebView2埋め込みで初期化
            // MSAL is-browser オプションはWindows 10以降で自動的にWebView2を使用
            var options = new InteractiveBrowserCredentialOptions
            {
                TenantId = _authConfig.TenantId,
                ClientId = _authConfig.ClientId,
                // UriRedirectを指定しない場合、プラットフォームのデフォルト動作（WebView2）を使用
            };

            _credential = new InteractiveBrowserCredential(options);
            return _credential;
        }
    }
}
