namespace TelecomIQ.Api.Contracts;

internal static class PdfUpload
{
    public static bool HasSupportedNameAndSize(IFormFile file) =>
        file.Length is >= 5 and <= 20 * 1024 * 1024 &&
        file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    public static async Task<bool> HasValidHeader(Stream stream, CancellationToken ct)
    {
        var header = new byte[5];
        var bytesRead = await stream.ReadAtLeastAsync(header, header.Length, false, ct);
        stream.Position = 0;
        return bytesRead == header.Length && header.AsSpan().SequenceEqual("%PDF-"u8);
    }
}
