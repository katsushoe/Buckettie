namespace Buckettie.Application.Configuration;

/// <summary>管理者が保護するProvider認証設定です。秘密値は含めません。</summary>
public sealed record ProviderAuthenticationOptions
{
    /// <summary>許可するMoyai Instanceです。</summary>
    public required string Issuer { get; init; }
    /// <summary>公開Trust Bundleの絶対パスです。</summary>
    public required string TrustBundlePath { get; init; }
    /// <summary>全Consumerで共有するReplay DBの絶対パスです。</summary>
    public required string ReplayDatabasePath { get; init; }
    /// <summary>管理者が登録した対応付けです。</summary>
    public required ProviderProjectBinding[] Bindings { get; init; }
}

/// <summary>JWTから独立したProjectとRepositoryの対応付けです。</summary>
public sealed record ProviderProjectBinding
{
    /// <summary>Buckettieの登録IDです。</summary>
    public required string Repository { get; init; }
    /// <summary>Moyaiの既存Project UUIDです。</summary>
    public required Guid ProjectId { get; init; }
    /// <summary>登録変更時の意図しない再対応付けを防ぐRepository識別子です。</summary>
    public required string NormalizedRepository { get; init; }
}

/// <summary>Project文脈のない管理Tool専用の相互TLS設定です。</summary>
public sealed record AdministratorEndpointOptions
{
    /// <summary>通常のMCPポートと異なるloopback HTTPSポートです。</summary>
    public required int Port { get; init; }
    /// <summary>LocalMachine/Myのサーバー証明書のThumbprintです。</summary>
    public required string ServerCertificateThumbprint { get; init; }
    /// <summary>許可する管理者クライアント証明書のThumbprintです。</summary>
    public required string[] ClientCertificateThumbprints { get; init; }
    /// <summary>CLIがCurrentUser/Myから選ぶ証明書です。秘密鍵はStore外へ出しません。</summary>
    public string? CliCertificateThumbprint { get; init; }
}
