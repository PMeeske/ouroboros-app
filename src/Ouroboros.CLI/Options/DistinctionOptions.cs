// <copyright file="DistinctionOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Command-line options for distinction learning management.
/// </summary>
[Verb("distinction", HelpText = "Manage distinction learning from consciousness dream cycles")]
public sealed class DistinctionOptions
{
    /// <summary>
    /// Gets or sets the subcommand to execute.
    /// </summary>
    [Value(0, Required = true, MetaName = "command", HelpText = "Command: status, list, dissolve, learn, export, clear")]
    public required string Command { get; set; }
}