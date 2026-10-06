namespace Buckettie.Server;

/// <summary>Buckettie Serverの起動引数です。</summary>
/// <param name="ConfigurationPath">明示された設定ファイル。未指定なら既定の配置を使います。</param>
/// <param name="MoyaiIntegration"><c>--moyai</c>指定時だけtrueです。既定は単体動作です。</param>
/// <param name="DirectUnrestricted">
/// <c>--direct-unrestricted</c>指定時だけtrueです。連携モードのまま、Authorizationなしのloopback直接接続に
/// 単体モードと同じくすべてのRepository Toolを許可します。
/// </param>
internal sealed record ServerArguments(string? ConfigurationPath, bool MoyaiIntegration, bool DirectUnrestricted = false)
{
    /// <summary>Moyai連携モードを有効にする起動オプションです。</summary>
    internal const string MoyaiOption = "--moyai";

    /// <summary>連携モードで直接接続を単体モード相当にする起動オプションです。</summary>
    internal const string DirectUnrestrictedOption = "--direct-unrestricted";

    /// <summary>起動引数を解析します。未知のオプションは設定ファイルと誤認しないよう拒否します。</summary>
    internal static ServerArguments Parse(IReadOnlyList<string> args)
    {
        string? configurationPath = null;
        bool moyai = false;
        bool directUnrestricted = false;
        foreach (string argument in args)
        {
            if (string.Equals(argument, MoyaiOption, StringComparison.OrdinalIgnoreCase)) moyai = true;
            else if (string.Equals(argument, DirectUnrestrictedOption, StringComparison.OrdinalIgnoreCase)) directUnrestricted = true;
            else if (argument.StartsWith('-') || configurationPath is not null)
                throw new ArgumentException("Unsupported server argument.", nameof(args));
            else configurationPath = argument;
        }
        // Standalone mode already allows every direct call; accepting the option there would hide a missing --moyai.
        if (directUnrestricted && !moyai)
            throw new ArgumentException("--direct-unrestricted requires --moyai.", nameof(args));
        return new(configurationPath, moyai, directUnrestricted);
    }
}

/// <summary>稼働中ServerのMoyai連携モードです。</summary>
/// <param name="MoyaiIntegration">連携モードならtrue、単体動作ならfalseです。</param>
/// <param name="DirectUnrestricted">連携モードで直接接続にすべてのRepository Toolを許可するならtrueです。</param>
public sealed record ProviderIntegrationMode(bool MoyaiIntegration, bool DirectUnrestricted = false)
{
    /// <summary>Capabilityへ公開するモード名です。</summary>
    public string Name => MoyaiIntegration ? "moyai" : "standalone";

    /// <summary>
    /// Capabilityへ公開する直接接続の扱いです（Moyai Consumer Contract）。単体動作は常に<c>unrestricted</c>です。
    /// </summary>
    public string DirectConnection => !MoyaiIntegration || DirectUnrestricted ? "unrestricted" : "read_only";
}
