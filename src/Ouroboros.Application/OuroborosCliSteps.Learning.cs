// <copyright file="OuroborosCliSteps.Learning.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Application;

public static partial class OuroborosCliSteps
{
    /// <summary>
    /// Records an experience for learning.
    /// Usage: OuroborosLearn('goal=Search task|success=true|quality=0.85|insight=Search was effective')
    /// </summary>
    [PipelineToken("OuroborosLearn", "RecordExperience", "Learn")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosLearn(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            // Parse experience data from args
            string parsed = ParseString(args);
            string goal = _currentAtom.CurrentGoal ?? "Unknown goal";
            bool success = true;
            double quality = 0.7;
            List<string> insights = new List<string>();

            if (!string.IsNullOrWhiteSpace(parsed))
            {
                foreach (string part in parsed.Split('|'))
                {
                    string trimmed = part.Trim();
                    if (trimmed.StartsWith("goal=", StringComparison.OrdinalIgnoreCase))
                    {
                        goal = trimmed.Substring(5);
                    }
                    else if (trimmed.StartsWith("success=", StringComparison.OrdinalIgnoreCase))
                    {
                        success = bool.TryParse(trimmed.Substring(8), out bool s2) && s2;
                    }
                    else if (trimmed.StartsWith("quality=", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(trimmed.Substring(8), out double q))
                        {
                            quality = Math.Clamp(q, 0.0, 1.0);
                        }
                    }
                    else if (trimmed.StartsWith("insight=", StringComparison.OrdinalIgnoreCase))
                    {
                        insights.Add(trimmed.Substring(8));
                    }
                }
            }

            // If no insights provided, generate a default one
            if (insights.Count == 0)
            {
                insights.Add(success ? "Task completed successfully" : "Task encountered issues");
            }

            OuroborosExperience experience = new OuroborosExperience(
                Guid.NewGuid(),
                goal,
                success,
                quality,
                insights,
                DateTime.UtcNow);

            _currentAtom.RecordExperience(experience);

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Experience recorded:");
                Console.WriteLine($"  Goal: {goal}");
                Console.WriteLine($"  Success: {success}");
                Console.WriteLine($"  Quality: {quality:P0}");
                Console.WriteLine($"  Insights: {string.Join(", ", insights)}");
                Console.WriteLine($"  Total experiences: {_currentAtom.Experiences.Count}");
            }

            s.Output = $"Experience recorded: {goal} ({(success ? "success" : "failure")}, quality: {quality:P0})";
            s.Branch = s.Branch.WithIngestEvent($"ouroboros:experience:{goal}", Array.Empty<string>());

            return Task.FromResult(s);
        };

    /// <summary>
    /// Adds a capability to the Ouroboros.
    /// Usage: OuroborosCapability('name=reasoning|desc=Logical reasoning|conf=0.85')
    /// </summary>
    [PipelineToken("OuroborosCapability", "AddCapability")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosCapability(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return Task.FromResult(s);
            }

            string parsed = ParseString(args);
            if (string.IsNullOrWhiteSpace(parsed))
            {
                // List current capabilities
                Console.WriteLine("[ouroboros] Current capabilities:");
                foreach (OuroborosCapability cap in _currentAtom.Capabilities)
                {
                    Console.WriteLine($"  - {cap.Name}: {cap.Description} ({cap.ConfidenceLevel:P0})");
                }

                return Task.FromResult(s);
            }

            string name = "unknown";
            string desc = "No description";
            double conf = 0.5;

            foreach (string part in parsed.Split('|'))
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
                {
                    name = trimmed.Substring(5);
                }
                else if (trimmed.StartsWith("desc=", StringComparison.OrdinalIgnoreCase))
                {
                    desc = trimmed.Substring(5);
                }
                else if (trimmed.StartsWith("conf=", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(trimmed.Substring(5), out double c))
                    {
                        conf = Math.Clamp(c, 0.0, 1.0);
                    }
                }
            }

            _currentAtom.AddCapability(new OuroborosCapability(name, desc, conf));

            if (s.Trace)
            {
                Console.WriteLine($"[ouroboros] Capability added: {name} ({conf:P0})");
            }

            s.Output = $"Capability '{name}' added with {conf:P0} confidence";

            return Task.FromResult(s);
        };
}
