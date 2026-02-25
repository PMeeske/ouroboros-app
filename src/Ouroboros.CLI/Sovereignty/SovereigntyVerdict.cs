// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Sovereignty;

/// <summary>
/// The result of Iaret's constitutional review of a proposed action.
/// </summary>
public sealed record SovereigntyVerdict(
    bool Approved,
    string Reason,
    string RawResponse)
{
    public static SovereigntyVerdict Allow(string reason, string raw = "") =>
        new(true, reason, raw);

    public static SovereigntyVerdict Deny(string reason, string raw = "") =>
        new(false, reason, raw);

    public static SovereigntyVerdict DenyOnError(string error) =>
        new(false, $"[Sovereignty gate error — denied by default] {error}", "");
}
