using System.IO.Compression;

namespace v2rayN.Common;

public static class LogExporter
{
    /// <summary>
    /// Zips all NLog files plus the latest sing-box log into the given path.
    /// </summary>
    public static bool ExportTo(string zipPath)
    {
        try
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            var logDir = Utils.GetLogPath();
            if (Directory.Exists(logDir))
            {
                foreach (var file in Directory.GetFiles(logDir, "*.*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
                    }
                    catch { /* file in use — skip */ }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("LogExporter", ex);
            return false;
        }
    }
}
