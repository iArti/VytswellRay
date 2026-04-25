namespace ServiceLib.Services;

public class UpdateService(Config config, Func<bool, string, Task> updateFunc)
{
    private readonly Config? _config = config;
    private readonly Func<bool, string, Task>? _updateFunc = updateFunc;
    private readonly int _timeout = 30;
    private static readonly string _tag = "UpdateService";

    public async Task CheckUpdateCore(bool preRelease)
    {
        var type = ECoreType.sing_box;
        var url = string.Empty;
        var fileName = string.Empty;

        DownloadService downloadHandle = new();
        downloadHandle.UpdateCompleted += (sender2, args) =>
        {
            if (args.Success)
            {
                _ = UpdateFunc(false, ResUI.MsgDownloadV2rayCoreSuccessfully);
                _ = UpdateFunc(false, ResUI.MsgUnpacking);
                try { _ = UpdateFunc(true, fileName); }
                catch (Exception ex) { _ = UpdateFunc(false, ex.Message); }
            }
            else
            {
                _ = UpdateFunc(false, args.Msg);
            }
        };
        downloadHandle.Error += (sender2, args) =>
        {
            _ = UpdateFunc(false, args.GetException().Message);
        };

        await UpdateFunc(false, string.Format(ResUI.MsgStartUpdating, type));
        var result = await CheckUpdateAsync(downloadHandle, preRelease);
        if (result.Success)
        {
            await UpdateFunc(false, string.Format(ResUI.MsgParsingSuccessfully, type));
            await UpdateFunc(false, result.Msg);

            url = result.Url.ToString();
            var ext = url.Contains(".tar.gz") ? ".tar.gz" : Path.GetExtension(url);
            fileName = Utils.GetTempPath(Utils.GetGuid() + ext);
            await downloadHandle.DownloadFileAsync(url, fileName, true, _timeout);
        }
        else
        {
            if (!result.Msg.IsNullOrEmpty())
            {
                await UpdateFunc(false, result.Msg);
            }
        }
    }

    public async Task UpdateGeoFileAll()
    {
        await UpdateGeoFiles();
        await UpdateOtherFiles();
        await UpdateSrsFileAll();
        await UpdateFunc(true, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, "geo"));
    }

    #region CheckUpdate private

    private async Task<UpdateResult> CheckUpdateAsync(DownloadService downloadHandle, bool preRelease)
    {
        try
        {
            var result = await GetRemoteVersion(downloadHandle, preRelease);
            if (!result.Success || result.Version is null)
            {
                return result;
            }
            return await ParseDownloadUrl(result);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(false, ex.Message);
            return new UpdateResult(false, ex.Message);
        }
    }

    private async Task<UpdateResult> GetRemoteVersion(DownloadService downloadHandle, bool preRelease)
    {
        var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.sing_box);
        var tagName = string.Empty;
        if (preRelease)
        {
            var url = coreInfo?.ReleaseApiUrl;
            var result = await downloadHandle.TryDownloadString(url, true, Global.AppName);
            if (result.IsNullOrEmpty()) return new UpdateResult(false, "");
            var gitHubReleases = JsonUtils.Deserialize<List<GitHubRelease>>(result);
            var gitHubRelease = preRelease ? gitHubReleases?.First() : gitHubReleases?.First(r => r.Prerelease == false);
            tagName = gitHubRelease?.TagName;
        }
        else
        {
            var url = Path.Combine(coreInfo.Url, "latest");
            var lastUrl = await downloadHandle.UrlRedirectAsync(url, true);
            if (lastUrl == null) return new UpdateResult(false, "");
            tagName = lastUrl?.Split("/tag/").LastOrDefault();
        }
        return new UpdateResult(true, new SemanticVersion(tagName));
    }

    private async Task<SemanticVersion> GetCoreVersion()
    {
        try
        {
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.sing_box);
            var filePath = string.Empty;
            foreach (var name in coreInfo.CoreExes)
            {
                var vName = Utils.GetBinPath(Utils.GetExeName(name), coreInfo.CoreType.ToString());
                if (File.Exists(vName)) { filePath = vName; break; }
            }

            if (!File.Exists(filePath))
                return new SemanticVersion("");

            var result = await Utils.GetCliWrapOutput(filePath, coreInfo.VersionArg);
            var version = Regex.Match(result ?? "", $"([0-9.]+)").Groups[1].Value;
            return new SemanticVersion(version);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(false, ex.Message);
            return new SemanticVersion("");
        }
    }

    private async Task<UpdateResult> ParseDownloadUrl(UpdateResult result)
    {
        try
        {
            var version = result.Version ?? new SemanticVersion(0, 0, 0);
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.sing_box);
            var coreUrl = await GetUrlFromCore(coreInfo) ?? string.Empty;
            var curVersion = await GetCoreVersion();
            var message = string.Format(ResUI.IsLatestCore, ECoreType.sing_box, curVersion.ToVersionString("v"));
            var url = string.Format(coreUrl, version.ToVersionString("v"), version);

            if (curVersion >= version && version != new SemanticVersion(0, 0, 0))
                return new UpdateResult(false, message);

            result.Url = url;
            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await UpdateFunc(false, ex.Message);
            return new UpdateResult(false, ex.Message);
        }
    }

    private async Task<string?> GetUrlFromCore(CoreInfo? coreInfo)
    {
        if (Utils.IsWindows())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlWinArm64,
                Architecture.X64 => coreInfo?.DownloadUrlWin64,
                _ => null,
            };
        }
        else if (Utils.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => coreInfo?.DownloadUrlLinuxArm64,
                Architecture.X64 => coreInfo?.DownloadUrlLinux64,
                _ => null,
            };
        }
        return await Task.FromResult("");
    }

    #endregion CheckUpdate private

    #region Geo private

    private async Task UpdateGeoFiles()
    {
        var geoUrl = string.IsNullOrEmpty(_config?.ConstItem.GeoSourceUrl)
            ? Global.GeoUrl
            : _config.ConstItem.GeoSourceUrl;

        List<string> files = ["geosite", "geoip"];
        foreach (var geoName in files)
        {
            var fileName = $"{geoName}.dat";
            var targetPath = Utils.GetBinPath($"{fileName}");
            var url = string.Format(geoUrl, geoName);
            await DownloadGeoFile(url, fileName, targetPath);
        }
    }

    private async Task UpdateOtherFiles()
    {
        if (_config.ConstItem.GeoSourceUrl.IsNotEmpty()) return;

        foreach (var url in Global.OtherGeoUrls)
        {
            var fileName = Path.GetFileName(url);
            var targetPath = Utils.GetBinPath($"{fileName}");
            await DownloadGeoFile(url, fileName, targetPath);
        }
    }

    private async Task UpdateSrsFileAll()
    {
        var geoipFiles = new List<string>();
        var geoSiteFiles = new List<string>();

        var routingItems = await AppManager.Instance.RoutingItems();
        foreach (var routing in routingItems)
        {
            var rules = JsonUtils.Deserialize<List<RulesItem>>(routing.RuleSet);
            foreach (var item in rules ?? [])
            {
                AddPrefixedItems(item.Ip, "geoip:", geoipFiles);
                AddPrefixedItems(item.Domain, "geosite:", geoSiteFiles);
            }
        }

        var dnsItem = await AppManager.Instance.GetDNSItem(ECoreType.sing_box);
        if (dnsItem != null)
        {
            ExtractDnsRuleSets(dnsItem.NormalDNS, geoipFiles, geoSiteFiles);
            ExtractDnsRuleSets(dnsItem.TunDNS, geoipFiles, geoSiteFiles);
        }

        geoSiteFiles.AddRange(["google", "cn", "geolocation-cn", "category-ads-all"]);

        var path = Utils.GetBinPath("srss");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        foreach (var item in geoipFiles.Distinct())
            await UpdateSrsFile("geoip", item);

        foreach (var item in geoSiteFiles.Distinct())
            await UpdateSrsFile("geosite", item);
    }

    private void AddPrefixedItems(List<string>? items, string prefix, List<string> output)
    {
        if (items == null) return;
        foreach (var item in items)
        {
            if (item.StartsWith(prefix))
                output.Add(item.Substring(prefix.Length));
        }
    }

    private void ExtractDnsRuleSets(string? dnsJson, List<string> geoipFiles, List<string> geoSiteFiles)
    {
        if (string.IsNullOrEmpty(dnsJson)) return;
        try
        {
            var dns = JsonUtils.Deserialize<Dns4Sbox>(dnsJson);
            if (dns?.rules != null)
                foreach (var rule in dns.rules)
                    ExtractSrsRuleSets(rule, geoipFiles, geoSiteFiles);
        }
        catch { }
    }

    private void ExtractSrsRuleSets(Rule4Sbox? rule, List<string> geoipFiles, List<string> geoSiteFiles)
    {
        if (rule == null) return;
        AddPrefixedItems(rule.rule_set, "geosite-", geoSiteFiles);
        AddPrefixedItems(rule.rule_set, "geoip-", geoipFiles);
        if (rule.rules != null)
            foreach (var nestedRule in rule.rules)
                ExtractSrsRuleSets(nestedRule, geoipFiles, geoSiteFiles);
    }

    private async Task UpdateSrsFile(string type, string srsName)
    {
        var srsUrl = string.IsNullOrEmpty(_config.ConstItem.SrsSourceUrl)
            ? Global.SingboxRulesetUrl
            : _config.ConstItem.SrsSourceUrl;

        var fileName = $"{type}-{srsName}.srs";
        var targetPath = Path.Combine(Utils.GetBinPath("srss"), fileName);
        var url = string.Format(srsUrl, type, $"{type}-{srsName}", srsName);
        await DownloadGeoFile(url, fileName, targetPath);
    }

    private async Task DownloadGeoFile(string url, string fileName, string targetPath)
    {
        var tmpFileName = Utils.GetTempPath(Utils.GetGuid());

        DownloadService downloadHandle = new();
        downloadHandle.UpdateCompleted += (sender2, args) =>
        {
            if (args.Success)
            {
                _ = UpdateFunc(false, string.Format(ResUI.MsgDownloadGeoFileSuccessfully, fileName));
                try
                {
                    if (File.Exists(tmpFileName))
                    {
                        File.Copy(tmpFileName, targetPath, true);
                        File.Delete(tmpFileName);
                    }
                }
                catch (Exception ex) { _ = UpdateFunc(false, ex.Message); }
            }
            else { _ = UpdateFunc(false, args.Msg); }
        };
        downloadHandle.Error += (sender2, args) =>
        {
            _ = UpdateFunc(false, args.GetException().Message);
        };

        await downloadHandle.DownloadFileAsync(url, tmpFileName, true, _timeout);
    }

    #endregion Geo private

    private async Task UpdateFunc(bool notify, string msg)
    {
        await _updateFunc?.Invoke(notify, msg);
    }
}
