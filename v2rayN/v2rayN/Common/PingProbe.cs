namespace v2rayN.Common;

/// <summary>
/// Measures TCP-connect round-trip time to a host:port.
/// Used as the "is it working" indicator next to the connection status.
/// </summary>
public static class PingProbe
{
    public static async Task<int?> MeasureAsync(string host, int port, CancellationToken ct, int timeoutMs = 5000)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0) return null;
        try
        {
            using var tcp = new TcpClient();
            var sw = Stopwatch.StartNew();
            await tcp.ConnectAsync(host, port, ct).AsTask().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);
            sw.Stop();
            return (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return null;
        }
    }
}
