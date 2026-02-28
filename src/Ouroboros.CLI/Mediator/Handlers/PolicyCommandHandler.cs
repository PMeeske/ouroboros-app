using System.Text.RegularExpressions;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="PolicyCommandRequest"/>.
/// Extracted from <c>OuroborosAgent.PolicyCommandAsync</c>.
/// </summary>
public sealed partial class PolicyCommandHandler : IRequestHandler<PolicyCommandRequest, string>
{
    private readonly OuroborosAgent _agent;

    public PolicyCommandHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(PolicyCommandRequest request, CancellationToken cancellationToken)
    {
        var subCommand = request.SubCommand;
        _ = subCommand.ToLowerInvariant().Trim(); // parsed below via args split

        // Parse policy subcommand and create appropriate PolicyOptions
        var args = subCommand.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string command = args.Length > 0 ? args[0] : "list";
        string argument = args.Length > 1 ? args[1] : "";

        try
        {
            // Create PolicyOptions from parsed command
            var policyOpts = new PolicyOptions
            {
                Command = command,
                Culture = _agent.Config.Culture,
                Format = "summary",
                Limit = 50,
                Verbose = _agent.Config.Debug
            };

            // Parse arguments based on command type
            if (command == "list")
            {
                policyOpts.Format = argument switch
                {
                    "json" => "json",
                    "table" => "table",
                    _ => "summary"
                };
            }
            else if (command == "show")
            {
                policyOpts.Command = "list";
            }
            else if (command == "enforce")
            {
                policyOpts.Command = "enforce";
                // Parse arguments: --enable-self-mod --risk-level Low
                if (argument.Contains("--enable-self-mod"))
                {
                    policyOpts.EnableSelfModification = true;
                }
                if (argument.Contains("--risk-level"))
                {
                    var match = RiskLevelArgRegex().Match(argument);
                    if (match.Success)
                    {
                        policyOpts.RiskLevel = match.Groups[1].Value;
                    }
                }
            }
            else if (command == "audit")
            {
                policyOpts.Command = "audit";
                if (int.TryParse(argument, out var limit))
                {
                    policyOpts.Limit = limit;
                }
            }
            else if (command == "simulate")
            {
                policyOpts.Command = "simulate";
                if (Guid.TryParse(argument, out _))
                {
                    policyOpts.PolicyId = argument;
                }
            }
            else if (command == "create")
            {
                policyOpts.Command = "create";
                var parts = argument.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    policyOpts.Name = parts[0].Trim();
                }
                if (parts.Length > 1)
                {
                    policyOpts.Description = parts[1].Trim();
                }
            }
            else if (command == "approve")
            {
                policyOpts.Command = "approve";
                var parts = argument.Split(' ', 2);
                if (parts.Length > 0 && Guid.TryParse(parts[0], out _))
                {
                    policyOpts.ApprovalId = parts[0];
                }
                if (parts.Length > 1)
                {
                    policyOpts.Decision = "approve";
                    policyOpts.ApproverId = "agent";
                }
            }

            // Call the real PolicyCommands
            await PolicyCommands.RunPolicyAsync(policyOpts);
            return $"Policy command executed: {command}";
        }
        catch (InvalidOperationException ex)
        {
            return $"Policy command failed: {ex.Message}";
        }
    }

    [GeneratedRegex(@"--risk-level\s+(\w+)")]
    private static partial Regex RiskLevelArgRegex();
}
