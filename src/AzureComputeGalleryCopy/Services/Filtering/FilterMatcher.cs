using AzureComputeGalleryCopy.Models;

namespace AzureComputeGalleryCopy.Services.Filtering;

/// <summary>
/// フィルタ基準に基づいてイメージ定義・バージョン名をマッチングするサービス
/// </summary>
public interface IFilterMatcher
{
    /// <summary>
    /// イメージ定義名がフィルタ条件に一致するか判定
    /// </summary>
    /// <param name="imageDefinitionName">イメージ定義名</param>
    /// <param name="criteria">フィルタ基準</param>
    /// <returns>一致する場合true</returns>
    bool MatchesImageDefinition(string imageDefinitionName, FilterCriteria criteria);

    /// <summary>
    /// バージョン名がフィルタ条件に一致するか判定
    /// </summary>
    /// <param name="versionName">バージョン名</param>
    /// <param name="criteria">フィルタ基準</param>
    /// <returns>一致する場合true</returns>
    bool MatchesVersion(string versionName, FilterCriteria criteria);
}

/// <summary>
/// フィルタマッチング実装
/// </summary>
public class FilterMatcher : IFilterMatcher
{
    /// <inheritdoc/>
    public bool MatchesImageDefinition(string imageDefinitionName, FilterCriteria criteria)
    {
        ArgumentException.ThrowIfNullOrEmpty(imageDefinitionName);
        ArgumentNullException.ThrowIfNull(criteria);

        return MatchesFilters(
            imageDefinitionName,
            criteria.ImageDefinitionIncludes,
            criteria.ImageDefinitionExcludes,
            criteria.MatchMode
        );
    }

    /// <inheritdoc/>
    public bool MatchesVersion(string versionName, FilterCriteria criteria)
    {
        ArgumentException.ThrowIfNullOrEmpty(versionName);
        ArgumentNullException.ThrowIfNull(criteria);

        return MatchesFilters(
            versionName,
            criteria.VersionIncludes,
            criteria.VersionExcludes,
            criteria.MatchMode
        );
    }

    /// <summary>
    /// テキストがInclude/Excludeフィルタに一致するか判定
    /// </summary>
    /// <param name="text">対象テキスト</param>
    /// <param name="includes">Includeパターン（空の場合は制限なし）</param>
    /// <param name="excludes">Excludeパターン</param>
    /// <param name="matchMode">マッチモード</param>
    /// <returns>フィルタ条件に一致する場合true</returns>
    private bool MatchesFilters(
        string text,
        List<string> includes,
        List<string> excludes,
        MatchMode matchMode)
    {
        // Excludeパターンに一致する場合は除外
        if (excludes.Count > 0 && MatchesPattern(text, excludes, matchMode))
        {
            return false;
        }

        // Includeパターンが指定されている場合、それに一致する必要がある
        if (includes.Count > 0)
        {
            return MatchesPattern(text, includes, matchMode);
        }

        // Include未指定かつExclude未適用の場合は対象
        return true;
    }

    /// <summary>
    /// テキストがパターン一覧に一致するか判定（いずれかのパターンに一致すればtrue）
    /// </summary>
    private bool MatchesPattern(
        string text,
        List<string> patterns,
        MatchMode matchMode)
    {
        return patterns.Any(pattern =>
            matchMode switch
            {
                MatchMode.Prefix => text.StartsWith(pattern, StringComparison.OrdinalIgnoreCase),
                MatchMode.Contains => text.Contains(pattern, StringComparison.OrdinalIgnoreCase),
                _ => throw new ArgumentException($"不明なマッチモード: {matchMode}", nameof(matchMode))
            }
        );
    }
}
