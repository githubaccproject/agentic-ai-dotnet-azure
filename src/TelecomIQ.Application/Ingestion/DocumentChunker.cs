using TelecomIQ.Domain;

namespace TelecomIQ.Application.Ingestion;

internal static class DocumentChunker
{
    public static IEnumerable<(int Page, int Offset, string Text)> Split(IEnumerable<Page> pages)
    {
        const int chunkSize = 1600;
        const int stride = 1400;

        foreach (var page in pages)
        {
            for (var offset = 0; offset < page.Text.Length; offset += stride)
            {
                var text = page.Text.Substring(offset, Math.Min(chunkSize, page.Text.Length - offset));
                if (!string.IsNullOrWhiteSpace(text))
                    yield return (page.Number, offset, text);
            }
        }
    }
}
