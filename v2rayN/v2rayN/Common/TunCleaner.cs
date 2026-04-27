namespace v2rayN.Common;

/// <summary>
/// Removes orphaned vytswell_tun adapters left over from a previous hard crash of sing-box.
/// Best-effort — failures are non-fatal because sing-box will report a clearer error later if conflict persists.
/// </summary>
public static class TunCleaner
{
    private const string AdapterName = "vytswell_tun";

    public static void CleanupOrphaned()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", $"interface set interface name=\"{AdapterName}\" admin=disabled")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(2000);
        }
        catch
        {
            // best-effort
        }
    }
}
