// <copyright file="UrlValidator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Validates URLs against SSRF attacks by blocking requests to private/internal IP ranges.
/// </summary>
public static class UrlValidator
{
    /// <summary>
    /// Private IPv4 CIDR ranges that must be blocked.
    /// </summary>
    private static readonly (byte[] Network, int PrefixLength)[] PrivateIPv4Ranges =
    [
        (new byte[] { 127, 0, 0, 0 }, 8),       // 127.0.0.0/8  loopback
        (new byte[] { 10, 0, 0, 0 }, 8),         // 10.0.0.0/8   private class A
        (new byte[] { 172, 16, 0, 0 }, 12),      // 172.16.0.0/12 private class B
        (new byte[] { 192, 168, 0, 0 }, 16),     // 192.168.0.0/16 private class C
        (new byte[] { 169, 254, 0, 0 }, 16),     // 169.254.0.0/16 link-local
        (new byte[] { 0, 0, 0, 0 }, 8),          // 0.0.0.0/8    "this" network
    ];

    /// <summary>
    /// Checks whether a URL is safe to fetch (i.e. does not resolve to a private/internal IP).
    /// Resolves the hostname via DNS and checks all returned addresses against blocked ranges.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>True if the URL is safe to request; false if it targets a private/internal address.</returns>
    public static async Task<bool> IsUrlSafeAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        string host = uri.DnsSafeHost;

        // Block localhost explicitly (covers edge cases like uppercase, trailing dots, etc.)
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return false;

        // Resolve hostname to IP addresses
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host);
        }
        catch (SocketException)
        {
            // Cannot resolve hostname -- block to be safe
            return false;
        }

        foreach (var address in addresses)
        {
            if (IsPrivateAddress(address))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Synchronous convenience wrapper for <see cref="IsUrlSafeAsync"/>.
    /// </summary>
    public static bool IsUrlSafe(string url)
        => IsUrlSafeAsync(url).GetAwaiter().GetResult(); // Intentional: sync wrapper for callers that cannot be made async

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // ::1 is already covered by IPAddress.IsLoopback.
            // Block fd00::/8 (unique local addresses).
            byte[] bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) // fc00::/7 covers fd00::/8
                return true;

            // Block link-local fe80::/10
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
                return true;

            // Check IPv4-mapped IPv6 addresses (::ffff:x.x.x.x)
            if (address.IsIPv4MappedToIPv6)
            {
                var mapped = address.MapToIPv4();
                return IsPrivateIPv4(mapped.GetAddressBytes());
            }

            return false;
        }

        return IsPrivateIPv4(address.GetAddressBytes());
    }

    private static bool IsPrivateIPv4(byte[] addressBytes)
    {
        foreach (var (network, prefixLength) in PrivateIPv4Ranges)
        {
            if (IsInSubnet(addressBytes, network, prefixLength))
                return true;
        }

        return false;
    }

    private static bool IsInSubnet(byte[] address, byte[] network, int prefixLength)
    {
        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (address[i] != network[i])
                return false;
        }

        if (remainingBits > 0)
        {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            if ((address[fullBytes] & mask) != (network[fullBytes] & mask))
                return false;
        }

        return true;
    }
}
