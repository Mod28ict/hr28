using Microsoft.AspNetCore.HttpOverrides;

namespace HR28.Web.Middleware;

/// <summary>
/// Behind Cloudflare every request reaches the web app from a Cloudflare address, so the
/// visitor's own address (which the API's sign-in limits are counted per) has to come from
/// Cloudflare's CF-Connecting-IP header. The header is trusted ONLY when the request really
/// arrives from a Cloudflare network; from anywhere else it is ignored, so it can't be faked.
///
/// Off unless ReverseProxy:Cloudflare is true (Azure app setting ReverseProxy__Cloudflare).
/// Development and deployments without Cloudflare keep using the connecting address.
/// </summary>
public static class CloudflareClientIp
{
    public const string HeaderName = "CF-Connecting-IP";

    /// <summary>
    /// Cloudflare's published ranges (https://www.cloudflare.com/ips/). Check them when
    /// deploying; if Cloudflare changes them, set ReverseProxy:CloudflareNetworks instead of
    /// changing code (that list then replaces this one).
    /// </summary>
    public static readonly string[] DefaultNetworks =
    {
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22",
        "141.101.64.0/18", "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20",
        "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
        "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32",
        "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32"
    };

    /// <summary>
    /// Options for UseForwardedHeaders, or null when the Cloudflare switch is off.
    /// A malformed network in configuration stops the app at startup rather than
    /// silently trusting the wrong addresses.
    /// </summary>
    public static ForwardedHeadersOptions? CreateOptions(IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("ReverseProxy:Cloudflare"))
            return null;

        var networks = configuration.GetSection("ReverseProxy:CloudflareNetworks").Get<string[]>();

        if (networks is not { Length: > 0 })
            networks = DefaultNetworks;

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            ForwardedForHeaderName = HeaderName,
            ForwardLimit = 1
        };

        // Remove the loopback defaults: only Cloudflare may set the visitor's address.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        foreach (var cidr in networks)
        {
            var network = System.Net.IPNetwork.Parse(cidr.Trim());
            options.KnownIPNetworks.Add(network);

            // The same IPv4 network as seen on a dual-stack socket (::ffff:a.b.c.d).
            if (network.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                options.KnownIPNetworks.Add(new System.Net.IPNetwork(
                    network.BaseAddress.MapToIPv6(), network.PrefixLength + 96));
            }
        }

        return options;
    }
}
