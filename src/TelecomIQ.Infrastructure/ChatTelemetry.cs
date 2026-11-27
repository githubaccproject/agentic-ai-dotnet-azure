using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TelecomIQ.Infrastructure;

public static class ChatTelemetry
{
    public static readonly ActivitySource Source = new("TelecomIQ.Chat");
    private static readonly Meter Meter = new("TelecomIQ.Chat");
    public static readonly Counter<long> Tokens = Meter.CreateCounter<long>("telecomiq.chat.reported_tokens");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("telecomiq.chat.duration", "ms");
}
