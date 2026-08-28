using System.Text.Json;

namespace Buckettie.Application.Interactive;

/// <summary>承認Dialogからの応答です（Secretは含みません）。</summary>
public sealed record ApprovalPromptResponse(bool Approved);

/// <summary>Token入力Dialogからの応答です。</summary>
public sealed record TokenPromptResponse(string? Token);

/// <summary>
/// Buckettie.ServerとBuckettie.ApprovalPromptが共有する、長さPrefix付きJSON Pipe Protocolです。
/// 両者が同じ契約を参照することで、Wire Formatのずれを防ぎます。
/// </summary>
public static class ApprovalPipeProtocol
{
    /// <summary>1メッセージあたりの最大許容バイト数です。</summary>
    public const int MaxPayloadBytes = 8192;

    /// <summary>長さPrefix付きJSONフレームを書き込みます。</summary>
    public static async Task WriteFrameAsync<T>(Stream target, T payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(payload);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (json.Length > MaxPayloadBytes)
        {
            throw new InvalidOperationException("Approval prompt payload exceeds the maximum allowed size.");
        }

        byte[] header = BitConverter.GetBytes(json.Length);
        await target.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await target.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 長さPrefix付きJSONフレームを読み込みます。切断・不正なフレームではnullを返します。
    /// </summary>
    public static async Task<T?> ReadFrameAsync<T>(Stream source, CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] header = new byte[4];
        if (!await ReadExactAsync(source, header, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        int length = BitConverter.ToInt32(header);
        if (length is < 0 or > MaxPayloadBytes)
        {
            return null;
        }

        byte[] payload = new byte[length];
        if (!await ReadExactAsync(source, payload, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> ReadExactAsync(Stream source, byte[] buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await source.ReadAsync(buffer.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }
}
