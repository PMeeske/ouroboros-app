// <copyright file="OuroborosCliSteps.Cycle.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text;
using Ouroboros.Abstractions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Application;

public static partial class OuroborosCliSteps
{
    /// <summary>
    /// Exports the Ouroboros state to MeTTa symbolic representation.
    /// Usage: OuroborosToMeTTa
    /// </summary>
    [PipelineToken("OuroborosToMeTTa", "ExportMeTTa", "ToMeTTa")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosToMeTTa(string? args = null)
        => async s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized. Call OuroborosInit first.");
                return s;
            }

            string metta = _currentAtom.ToMeTTa();

            Console.WriteLine("[ouroboros] MeTTa representation:");
            Console.WriteLine(metta);

            s.Output = metta;
            s.Context = metta;

            // If MeTTa engine is available, add facts to it
            if (s.MeTTaEngine != null)
            {
                Result<Unit, string> result = await s.MeTTaEngine.AddFactAsync(metta);
                result.Match(
                    _ =>
                    {
                        if (s.Trace)
                        {
                            Console.WriteLine("[ouroboros] Facts added to MeTTa engine");
                        }
                    },
                    error =>
                    {
                        Console.WriteLine($"[ouroboros] Warning: Failed to add facts to MeTTa: {error}");
                    });
            }

            return s;
        };

    /// <summary>
    /// Runs a complete improvement cycle: Plan -> Execute -> Verify -> Learn.
    /// Usage: OuroborosCycle('Analyze the codebase')
    /// </summary>
    [PipelineToken("OuroborosCycle", "ImprovementCycle", "SelfImprove")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosCycle(string? args = null)
        => async s =>
        {
            if (_currentAtom == null)
            {
                _currentAtom = OuroborosAtom.CreateDefault();
                Console.WriteLine("[ouroboros] Auto-initialized Ouroboros atom");
            }

            string goal = ParseString(args);
            if (string.IsNullOrWhiteSpace(goal))
            {
                goal = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Prompt;
            }

            if (string.IsNullOrWhiteSpace(goal))
            {
                Console.WriteLine("[ouroboros] No goal provided for improvement cycle");
                return s;
            }

            // Check safety
            if (!_currentAtom.IsSafeAction(goal))
            {
                Console.WriteLine($"[ouroboros] ⚠ Goal violates safety constraints");
                s.Output = "Improvement cycle aborted: safety violation";
                return s;
            }

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"[ouroboros] Starting Improvement Cycle");
            Console.WriteLine($"[ouroboros] Goal: {goal}");
            Console.WriteLine($"[ouroboros] Confidence: {_currentAtom.AssessConfidence(goal)}");
            Console.WriteLine($"{'=',-60}\n");

            _currentAtom.SetGoal(goal);

            StringBuilder cycleOutput = new StringBuilder();
            List<string> insights = new List<string>();
            bool overallSuccess = true;

            // Phase 1: PLAN
            Console.WriteLine("[ouroboros] Phase 1: PLAN");
            _currentAtom.AdvancePhase(); // Move to Execute after planning

            string planPrompt = $@"Create a step-by-step plan to achieve: {goal}

Self-Assessment:
{_currentAtom.SelfReflect()}

Provide a concise plan with 3-5 actionable steps.";

            string planOutput;
            try
            {
                planOutput = await s.Llm.InnerModel.GenerateTextAsync(planPrompt);
                Console.WriteLine($"[ouroboros] Plan generated:\n{planOutput}");
                cycleOutput.AppendLine($"PLAN:\n{planOutput}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Plan generation failed: {ex.Message}");
                overallSuccess = false;
                planOutput = "Planning failed";
                insights.Add($"Planning error: {ex.Message}");
            }

            // Phase 2: EXECUTE
            Console.WriteLine("\n[ouroboros] Phase 2: EXECUTE");
            _currentAtom.AdvancePhase(); // Move to Verify after execution

            string executePrompt = $@"Execute this plan for goal: {goal}

Plan:
{planOutput}

Describe the execution results for each step. Be concise.";

            string executeOutput;
            try
            {
                executeOutput = await s.Llm.InnerModel.GenerateTextAsync(executePrompt);
                Console.WriteLine($"[ouroboros] Execution results:\n{executeOutput}");
                cycleOutput.AppendLine($"EXECUTION:\n{executeOutput}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Execution failed: {ex.Message}");
                overallSuccess = false;
                executeOutput = "Execution failed";
                insights.Add($"Execution error: {ex.Message}");
            }

            // Phase 3: VERIFY
            Console.WriteLine("\n[ouroboros] Phase 3: VERIFY");
            _currentAtom.AdvancePhase(); // Move to Learn after verification

            string verifyPrompt = $@"Verify the execution results for goal: {goal}

Execution Output:
{executeOutput}

Provide:
1. Was the goal achieved? (yes/no)
2. Quality score (0-100%)
3. Any issues identified";

            string verifyOutput;
            double quality = 0.7;
            try
            {
                verifyOutput = await s.Llm.InnerModel.GenerateTextAsync(verifyPrompt);
                Console.WriteLine($"[ouroboros] Verification:\n{verifyOutput}");
                cycleOutput.AppendLine($"VERIFICATION:\n{verifyOutput}\n");

                // Try to extract quality from response
                if (verifyOutput.Contains("100%"))
                {
                    quality = 1.0;
                }
                else if (verifyOutput.Contains("90%"))
                {
                    quality = 0.9;
                }
                else if (verifyOutput.Contains("80%"))
                {
                    quality = 0.8;
                }
                else if (verifyOutput.ToLower().Contains("no") && verifyOutput.ToLower().Contains("achieved"))
                {
                    quality = 0.4;
                    overallSuccess = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Verification failed: {ex.Message}");
                verifyOutput = "Verification failed";
                insights.Add($"Verification error: {ex.Message}");
            }

            // Phase 4: LEARN
            Console.WriteLine("\n[ouroboros] Phase 4: LEARN");
            _currentAtom.AdvancePhase(); // Back to Plan, completing the cycle

            string learnPrompt = $@"Extract 2-3 key insights from this improvement cycle:

Goal: {goal}
Plan: {planOutput}
Execution: {executeOutput}
Verification: {verifyOutput}

Provide bullet points starting with '-':";

            try
            {
                string learnOutput = await s.Llm.InnerModel.GenerateTextAsync(learnPrompt);
                Console.WriteLine($"[ouroboros] Insights:\n{learnOutput}");
                cycleOutput.AppendLine($"LEARNING:\n{learnOutput}\n");

                // Parse insights
                foreach (string line in learnOutput.Split('\n'))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("-") || trimmed.StartsWith("*"))
                    {
                        insights.Add(trimmed.TrimStart('-', '*', ' '));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ouroboros] Learning failed: {ex.Message}");
                insights.Add($"Learning error: {ex.Message}");
            }

            // Record the experience
            if (insights.Count == 0)
            {
                insights.Add(overallSuccess ? "Cycle completed successfully" : "Cycle encountered issues");
            }

            OuroborosExperience experience = new OuroborosExperience(
                Guid.NewGuid(),
                goal,
                overallSuccess,
                quality,
                insights,
                DateTime.UtcNow);

            _currentAtom.RecordExperience(experience);

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"[ouroboros] ✓ Cycle {_currentAtom.CycleCount} completed");
            Console.WriteLine($"[ouroboros] Success: {overallSuccess}");
            Console.WriteLine($"[ouroboros] Quality: {quality:P0}");
            Console.WriteLine($"[ouroboros] Insights: {insights.Count}");
            Console.WriteLine($"{'=',-60}\n");

            s.Output = cycleOutput.ToString();
            s.Context = string.Join("\n", insights);
            s.Branch = s.Branch.WithReasoning(new FinalSpec(cycleOutput.ToString()), goal, new List<ToolExecution>());

            return s;
        };

    /// <summary>
    /// Gets the current status of the Ouroboros.
    /// Usage: OuroborosStatus
    /// </summary>
    [PipelineToken("OuroborosStatus", "SelfStatus", "Status")]
    public static Step<CliPipelineState, CliPipelineState> OuroborosStatus(string? args = null)
        => s =>
        {
            if (_currentAtom == null)
            {
                Console.WriteLine("[ouroboros] Not initialized");
                s.Output = "Ouroboros not initialized";
                return Task.FromResult(s);
            }

            StringBuilder status = new StringBuilder();
            status.AppendLine($"╔{'═',58}╗");
            status.AppendLine($"║ Ouroboros Status: {_currentAtom.Name,-38}║");
            status.AppendLine($"╠{'═',58}╣");
            status.AppendLine($"║ Instance ID: {_currentAtom.InstanceId,-43}║");
            status.AppendLine($"║ Current Phase: {_currentAtom.CurrentPhase,-41}║");
            status.AppendLine($"║ Cycles Completed: {_currentAtom.CycleCount,-38}║");
            status.AppendLine($"║ Current Goal: {TruncateString(_currentAtom.CurrentGoal ?? "None", 42),-42}║");
            status.AppendLine($"║ Capabilities: {_currentAtom.Capabilities.Count,-42}║");
            status.AppendLine($"║ Experiences: {_currentAtom.Experiences.Count,-43}║");

            if (_currentAtom.Experiences.Count > 0)
            {
                double successRate = _currentAtom.Experiences.Count(e => e.Success) / (double)_currentAtom.Experiences.Count;
                status.AppendLine($"║ Success Rate: {successRate:P0,-42}║");
            }

            status.AppendLine($"╚{'═',58}╝");

            string statusString = status.ToString();
            Console.WriteLine(statusString);
            s.Output = statusString;

            return Task.FromResult(s);
        };
}
