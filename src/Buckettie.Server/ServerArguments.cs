namespace Buckettie.Server;

/// <summary>Buckettie Serverの起動引数です。</summary>
/// <param name="ConfigurationPath">明示された設定ファイル。未指定なら既定の配置を使います。</param>
/// <param name="MoyaiIntegration"><c>--moyai</c>指定時だけtrueです。既定は単体動作です。</param>
internal sealed record ServerArguments(string? ConfigurationPath, bool MoyaiIntegration)
{
    /// <summary>Moyai連携モードを有効にする起動オプションです。</summary>
    internal const string MoyaiOption = "--moyai";

    /// <summary>起動引数を解析します。未知のオプションは設定ファイルと誤認しないよう拒否します。</summary>
    internal static ServerArguments Parse(IReadOnlyList<string> args)
    {
        string? configurationPath = null;
        bool moyai = false;
        foreach (string argument in args)
        {
            if (string.Equals(argument, MoyaiOption, StringComparison.OrdinalIgnoreCase)) moyai = true;
            else if (argument.StartsWith('-') || configurationPath is not null)
                throw new ArgumentException("Unsupported server argument.", nameof(args));
            else configurationPath = argument;
        }
        return new(configurationPath, moyai);
    }
}

/// <summary>稼働中ServerのMoyai連携モードです。</summary>
/// <param name="MoyaiIntegration">連携モードならtrue、単体動作ならfalseです。</param>
public sealed record ProviderIntegrationMode(bool MoyaiIntegration)
{
    /// <summary>Capabilityへ公開するモード名です。</summary>
    public string Name => MoyaiIntegration ? "moyai" : "standalone";
}
