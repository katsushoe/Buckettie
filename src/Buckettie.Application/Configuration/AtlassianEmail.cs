using System.Net.Mail;

namespace Buckettie.Application.Configuration;

/// <summary>
/// Atlassian emailの設定契約です。
/// </summary>
public static class AtlassianEmail
{
    /// <summary>値が単一の有効なemail addressかを返します。</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains('\r')
        && !value.Contains('\n')
        && MailAddress.TryCreate(value, out MailAddress? address)
        && string.Equals(address.Address, value, StringComparison.Ordinal);
}
