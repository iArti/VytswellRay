namespace ServiceLib.Services.CoreConfig.Singbox;

/// <summary>
/// Generates the hardcoded sing-box config for VytswellRay.
/// All routing, DNS, inbound, and log settings are fixed here and not user-configurable.
/// Only the proxy outbound is derived from the active ProfileItem.
/// </summary>
public static class FixedPolicy
{
    private const string TunTag = "tun-in";
    private const string ProxyDnsTag = "proxy-dns";
    private const string DirectDnsTag = "direct-dns";

    // Outbound tags must match what CoreConfigSingboxService generates
    private const string DirectTag = "direct";
    private const string ProxyTag = "proxy";

    public static async Task<RetResult> BuildConfigAsync(ProfileItem profile, Config appConfig)
    {
        var ret = new RetResult();
        try
        {
            if (profile == null || !profile.IsValid())
            {
                ret.Msg = ResUI.CheckServerSettings;
                return ret;
            }

            // Use the existing service (minimal context, TUN disabled so it doesn't try to
            // build TUN-specific inbounds we will replace anyway).
            var context = new CoreConfigContext
            {
                Node = profile,
                RunCoreType = ECoreType.sing_box,
                AppConfig = appConfig,
                IsTunEnabled = false,
            };

            var baseResult = new CoreConfigSingboxService(context).GenerateClientConfigContent();
            if (baseResult.Success != true)
                return baseResult;

            var cfg = JsonUtils.Deserialize<SingboxConfig>(baseResult.Data?.ToString() ?? "{}");
            if (cfg == null)
            {
                ret.Msg = ResUI.FailedGenDefaultConfiguration;
                return ret;
            }

            // Replace every section with our hardcoded policy.
            cfg.log = BuildLog();
            cfg.inbounds = BuildInbounds();
            cfg.route = BuildRoute(appConfig.GuiItem?.RouteBittorrentDirect ?? true);
            cfg.dns = BuildDns();
            cfg.experimental = null;

            ret.Data = JsonUtils.Serialize(cfg);
            ret.Msg = string.Format(ResUI.SuccessfulConfiguration, "");
            ret.Success = true;
            return await Task.FromResult(ret);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("FixedPolicy", ex);
            ret.Msg = ResUI.FailedGenDefaultConfiguration;
            return ret;
        }
    }

    // ── Log ──────────────────────────────────────────────────────────────────

    private static Log4Sbox BuildLog() => new()
    {
        level = "warn",
        output = Utils.GetLogPath("sing-box.log"),
        timestamp = true,
    };

    // ── Inbound ──────────────────────────────────────────────────────────────

    private static List<Inbound4Sbox> BuildInbounds() =>
    [
        new()
        {
            type = "tun",
            tag = TunTag,
            interface_name = "vytswell_tun",
            address = ["172.19.0.1/30", "fdfe:dcba:9876::1/126"],
            mtu = 9000,
            auto_route = true,
            strict_route = false,   // no kill-switch — traffic falls through if sing-box dies
            stack = "system",
            sniff = true,           // required for protocol-based routing (bittorrent, etc.)
        }
    ];

    // ── Routing ──────────────────────────────────────────────────────────────

    private static Route4Sbox BuildRoute(bool bittorrentDirect)
    {
        var rules = new List<Rule4Sbox>
        {
            // Enable protocol sniffing (modern sing-box ≥1.9 approach)
            new() { action = "sniff" },
            // Hijack DNS so all DNS traffic goes through sing-box resolver
            new() { protocol = ["dns"], action = "hijack-dns" },
        };

        // BitTorrent: direct (hidden setting) or proxy (user opted in via secret toggle)
        if (bittorrentDirect)
            rules.Add(new() { protocol = ["bittorrent"], outbound = DirectTag });

        rules.AddRange([
            // Block ads / malware geosite
            new() { rule_set = ["geosite-ads"], action = "reject" },
            // Private IPs always direct (LAN bypass)
            new() { ip_is_private = true, outbound = DirectTag },
            // Private domains direct (.local, RFC-defined private domains, etc.)
            new() { rule_set = ["geosite-private"], outbound = DirectTag },
            // Russian IP space direct — no need to proxy RU-only services
            new() { rule_set = ["geoip-ru"], outbound = DirectTag },
            // .ru TLD and VK (and all subdomains) direct
            new() { domain_suffix = [".ru", "vk.com"], outbound = DirectTag },
        ]);

        return new Route4Sbox
        {
            rules = rules,
            rule_set = BuildRuleSets(),
            final = ProxyTag,
            auto_detect_interface = true,
        };
    }

    private static List<Ruleset4Sbox> BuildRuleSets() =>
    [
        RemoteRuleset("geosite-ads",     "geosite", "category-ads-all"),
        RemoteRuleset("geosite-private", "geosite", "private"),
        RemoteRuleset("geoip-ru",        "geoip",   "ru"),
    ];

    private static Ruleset4Sbox RemoteRuleset(string tag, string type, string name) => new()
    {
        tag = tag,
        type = "remote",
        format = "binary",
        url = string.Format(Global.SingboxRulesetUrl, type, $"{type}-{name}"),
        download_detour = DirectTag,    // fetch updates without proxy (avoids circular dependency)
        update_interval = "168h",       // weekly auto-update
    };

    // ── DNS ──────────────────────────────────────────────────────────────────

    private static Dns4Sbox BuildDns() => new()
    {
        servers =
        [
            // Direct DNS: system resolver for bypassed traffic (uses workplace/ISP DNS naturally)
            new Server4Sbox { type = "system", tag = DirectDnsTag, detour = DirectTag },
            // Proxy DNS: Cloudflare DoT through proxy for all proxied traffic
            new Server4Sbox { type = "tls", tag = ProxyDnsTag, server = "1.1.1.1", detour = ProxyTag },
        ],
        rules =
        [
            // DNS queries for Russian/private domains resolve via direct DNS
            new() { rule_set = ["geosite-private"], server = DirectDnsTag },
            new() { domain_suffix = [".ru", "vk.com"], server = DirectDnsTag },
        ],
        final = ProxyDnsTag,
        independent_cache = true,
    };
}
