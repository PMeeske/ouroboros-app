// <copyright file="OuroborosAgent.Commands.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using LangChain.DocumentLoaders;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.CLI.Mediator;
using Ouroboros.CLI.Services;
using Ouroboros.Abstractions.Monads;
using PipelineReasoningStep = Ouroboros.Domain.Events.ReasoningStep;

namespace Ouroboros.CLI.Commands;

public sealed partial class OuroborosAgent
{
    private async Task<string> PlanAsync(string goal)
    {
        if (_orchestrator == null)
        {
            // Fallback to LLM-based planning
            if (_llm != null)
            {
                var (plan, _) = await _llm.GenerateWithToolsAsync(
                    $"Create a step-by-step plan for: {goal}. Format as numbered steps.");
                return plan;
            }
            return "I need an orchestrator or LLM to create plans.";
        }

        var planResult = await _orchestrator.PlanAsync(goal);
        return planResult.Match(
            plan =>
            {
                var steps = string.Join("\n", plan.Steps.Select((s, i) => $"  {i + 1}. {s.Action}"));
                return $"Here's my plan for '{goal}':\n{steps}";
            },
            error => $"I couldn't plan that: {error}");
    }

    private async Task<string> ExecuteAsync(string goal)
    {
        if (_orchestrator == null)
            return await ChatAsync($"Help me accomplish: {goal}");

        var planResult = await _orchestrator.PlanAsync(goal);
        return await planResult.Match(
            async plan =>
            {
                var execResult = await _orchestrator.ExecuteAsync(plan);
                return execResult.Match(
                    result => result.Success
                        ? $"Done! {result.FinalOutput ?? "Goal accomplished."}"
                        : $"Partially completed: {result.FinalOutput}",
                    error => $"Execution failed: {error}");
            },
            error => Task.FromResult($"Couldn't plan: {error}"));
    }



    private async Task<string> FetchResearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "Usage: fetch <research query>";
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            string url = $"http://export.arxiv.org/api/query?search_query=all:{Uri.EscapeDataString(query)}&start=0&max_results=5";
            string xml = await httpClient.GetStringAsync(url);
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            System.Xml.Linq.XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants(atom + "entry").Take(5).ToList();

            if (entries.Count == 0)
            {
                return $"No research found for '{query}'. Try a different search term.";
            }

            // Create skill name from query
            string skillName = string.Join("", query.Split(' ')
                .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "") : "")) + "Analysis";

            // Register new skill if we have a skill registry
            if (_skills != null)
            {
                var newSkill = new Skill(
                    skillName,
                    $"Analysis methodology from '{query}' research",
                    new List<string> { "research-context" },
                    new List<PlanStep>
                    {
                        new("Gather sources", new Dictionary<string, object> { ["query"] = query }, "Relevant papers", 0.9),
                        new("Extract patterns", new Dictionary<string, object> { ["method"] = "identify" }, "Key techniques", 0.85),
                        new("Synthesize", new Dictionary<string, object> { ["action"] = "combine" }, "Actionable knowledge", 0.8)
                    },
                    0.75, 0, DateTime.UtcNow, DateTime.UtcNow);
                _skills.RegisterSkill(newSkill.ToAgentSkill());
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Found {entries.Count} papers on '{query}':");
            sb.AppendLine();

            foreach (var entry in entries)
            {
                var title = entry.Element(atom + "title")?.Value?.Trim().Replace("\n", " ");
                var summary = entry.Element(atom + "summary")?.Value?.Trim();
                var truncatedSummary = summary?.Length > 150 ? summary[..150] + "..." : summary;

                sb.AppendLine($"  • {title}");
                sb.AppendLine($"    {truncatedSummary}");
                sb.AppendLine();
            }

            if (_skills != null)
            {
                sb.AppendLine($"✓ New skill created: UseSkill_{skillName}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error fetching research: {ex.Message}";
        }
    }

    /// <summary>
    /// Processes large input using divide-and-conquer orchestration.

    private async Task<string> ProcessLargeInputAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "Usage: process <large text or file path>";
        }

        // Check if input is a file path
        string textToProcess = input;
        if (File.Exists(input))
        {
            try
            {
                textToProcess = await File.ReadAllTextAsync(input);
            }
            catch (Exception ex)
            {
                return $"Error reading file: {ex.Message}";
            }
        }

        if (_divideAndConquer == null)
        {
            // Fall back to regular processing
            if (_chatModel == null)
            {
                return "No LLM available for processing.";
            }
            return await _chatModel.GenerateTextAsync($"Summarize and extract key points:\n\n{textToProcess}");
        }

        try
        {
            var chunks = _divideAndConquer.DivideIntoChunks(textToProcess);
            var result = await _divideAndConquer.ExecuteAsync(
                "Summarize and extract key points:",
                chunks);

            return result.Match(
                success => $"Processed {chunks.Count} chunks:\n\n{success}",
                error => $"Processing error: {error}");
        }
        catch (Exception ex)
        {
            return $"Divide-and-conquer processing failed: {ex.Message}";
        }
    }

    private async Task<string> RememberAsync(string info)
    {
        if (_personalityEngine != null && _personalityEngine.HasMemory)
        {
            await _personalityEngine.StoreConversationMemoryAsync(
                _voice.ActivePersona.Name,
                $"Remember: {info}",
                "Memory stored.",
                "user_memory",
                "neutral",
                0.8);
            return "Got it, I'll remember that.";
        }
        return "I don't have memory storage set up, but I'll try to keep it in mind for this session.";
    }

    private async Task<string> RecallAsync(string topic)
    {
        if (_personalityEngine != null && _personalityEngine.HasMemory)
        {
            var memories = await _personalityEngine.RecallConversationsAsync(topic, _voice.ActivePersona.Name, 5);
            if (memories.Any())
            {
                var recollections = memories.Take(3).Select(m => m.UserMessage);
                return "I remember: " + string.Join("; ", recollections);
            }
        }
        return $"I don't have specific memories about '{topic}' yet.";
    }

    private async Task<string> QueryMeTTaAsync(string query)
    {
        var result = await QueryMeTTaResultAsync(query);
        return result.Match(
            success => $"MeTTa result: {success}",
            error => $"Query error: {error}");
    }

    private async Task<Result<string, string>> QueryMeTTaResultAsync(string query)
    {
        if (_mettaEngine == null)
            return Result<string, string>.Failure("MeTTa symbolic reasoning isn't available.");

        return await _mettaEngine.ExecuteQueryAsync(query, CancellationToken.None);
    }

    // ================================================================
    // UNIFIED CLI COMMANDS - All Ouroboros capabilities in one place
    // ================================================================

    /// <summary>
    /// Ask a single question (routes to AskCommands CLI handler).
    /// </summary>
    private async Task<string> AskAsync(string question)
    {
        var result = await AskResultAsync(question);
        return result.Match(success => success, error => $"Error asking question: {error}");
    }

    private async Task<Result<string, string>> AskResultAsync(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            return Result<string, string>.Failure("What would you like to ask?");

        var askService = ServiceContainerFactory.Provider.GetService<IAskService>();
        if (askService == null)
            return Result<string, string>.Failure("Ask service not available.");

        var answer = await askService.AskAsync(new AskRequest(
            Question: question,
            ModelName: _config.Model ?? "llama3",
            Temperature: 0.7,
            MaxTokens: 2048,
            TimeoutSeconds: 120,
            Culture: Thread.CurrentThread.CurrentCulture.Name,
            Debug: _config.Debug));
        return Result<string, string>.Success(answer);
    }

    // IAgentFacade explicit implementations for monadic operations
    Task<Result<string, string>> IAgentFacade.AskResultAsync(string question) => AskResultAsync(question);
    Task<Result<string, string>> IAgentFacade.RunPipelineResultAsync(string dsl) => RunPipelineResultAsync(dsl);
    Task<Result<string, string>> IAgentFacade.RunMeTTaExpressionResultAsync(string expression) => RunMeTTaExpressionResultAsync(expression);
    Task<Result<string, string>> IAgentFacade.QueryMeTTaResultAsync(string query) => QueryMeTTaResultAsync(query);

    private async Task<string> RunPipelineAsync(string dsl)
    {
        var result = await RunPipelineResultAsync(dsl);
        return result.Match(success => success, error => $"Pipeline error: {error}");
    }

    private async Task<Result<string, string>> RunPipelineResultAsync(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl))
            return Result<string, string>.Failure("Please provide a DSL expression. Example: 'pipeline draft → critique → final'");

        var pipelineOpts = new PipelineOptions
        {
            Dsl = dsl,
            Model = "llama3",
            Temperature = 0.7,
            MaxTokens = 4096,
            TimeoutSeconds = 120,
            Voice = false,
            Culture = Thread.CurrentThread.CurrentCulture.Name,
            Debug = false
        };

        return await CaptureConsoleOutAsync(() => PipelineCommands.RunPipelineAsync(pipelineOpts));
    }

    /// <summary>
    /// Execute a MeTTa expression directly (routes to MeTTaCommands CLI handler).
    /// </summary>
    private async Task<string> RunMeTTaExpressionAsync(string expression)
    {
        var result = await RunMeTTaExpressionResultAsync(expression);
        return result.Match(success => success, error => $"MeTTa execution failed: {error}");
    }

    private async Task<Result<string, string>> RunMeTTaExpressionResultAsync(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Result<string, string>.Failure("Please provide a MeTTa expression. Example: '!(+ 1 2)' or '(= (greet $x) (Hello $x))'");

        var mettaOpts = new MeTTaOptions
        {
            Goal = expression,
            Voice = false,
            Culture = Thread.CurrentThread.CurrentCulture.Name,
            Debug = false
        };

        return await CaptureConsoleOutAsync(() => MeTTaCommands.RunMeTTaAsync(mettaOpts));
    }

    // Helper to capture CLI command output and return as Result
    private static async Task<Result<string, string>> CaptureConsoleOutAsync(Func<Task> action)
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                await action();
                return Result<string, string>.Success(writer.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private async Task<string> OrchestrateAsync(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return "What would you like me to orchestrate?";

        try
        {
            var orchestratorOpts = new OrchestratorOptions
            {
                Goal = goal,
                Model = "llama3",
                Temperature = 0.7,
                MaxTokens = 4096,
                TimeoutSeconds = 300,
                Voice = false,
                Debug = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await OrchestratorCommands.RunOrchestratorAsync(orchestratorOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Orchestration error: {ex.Message}";
        }
    }

    /// <summary>
    /// Network status and management (routes to NetworkCommands CLI handler).

    private async Task<string> NetworkCommandAsync(string subCommand)
    {
        try
        {
            var networkOpts = new NetworkOptions();

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await NetworkCommands.RunAsync(networkOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Network command error: {ex.Message}";
        }
    }

    /// <summary>
    /// DAG visualization and management (routes to DagCommands CLI handler).

    private async Task<string> DagCommandAsync(string subCommand)
    {
        try
        {
            var dagOpts = new DagOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "show"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await DagCommands.RunDagAsync(dagOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"DAG command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Affect and emotional state (routes to AffectCommands CLI handler).

    private async Task<string> AffectCommandAsync(string subCommand)
    {
        try
        {
            var affectOpts = new AffectOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await AffectCommands.RunAffectAsync(affectOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Affect command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Environment detection and configuration (routes to EnvironmentCommands CLI handler).

    private async Task<string> EnvironmentCommandAsync(string subCommand)
    {
        try
        {
            var envOpts = new EnvironmentOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await EnvironmentCommands.RunEnvironmentCommandAsync(envOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Environment command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Maintenance operations (routes to MaintenanceCommands CLI handler).

    private async Task<string> MaintenanceCommandAsync(string subCommand)
    {
        try
        {
            var maintenanceOpts = new MaintenanceOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await MaintenanceCommands.RunMaintenanceAsync(maintenanceOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"Maintenance command error: {ex.Message}";
        }
    }

    /// <summary>
    /// Policy management - routes to the real CLI PolicyCommands.

    private async Task<string> PolicyCommandAsync(string subCommand)
    {
        var cmd = subCommand.ToLowerInvariant().Trim();

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
                Culture = _config.Culture,
                Format = "summary",
                Limit = 50,
                Verbose = _config.Debug
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
                    var match = System.Text.RegularExpressions.Regex.Match(argument, @"--risk-level\s+(\w+)");
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
                if (System.Guid.TryParse(argument, out _))
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
                if (parts.Length > 0 && System.Guid.TryParse(parts[0], out _))
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
        catch (Exception ex)
        {
            return $"Policy command failed: {ex.Message}";
        }
    }


    /// <summary>

    private async Task<string> RunTestAsync(string testSpec)
    {
        if (string.IsNullOrWhiteSpace(testSpec))
        {
            return @"Test Commands:
• 'test llm' - Test LLM connectivity
• 'test metta' - Test MeTTa engine
• 'test embedding' - Test embedding model
• 'test all' - Run all connectivity tests";
        }

        var cmd = testSpec.ToLowerInvariant().Trim();

        if (cmd == "llm")
        {
            if (_chatModel == null) return "✗ LLM: Not configured";
            try
            {
                var response = await _chatModel.GenerateTextAsync("Say OK");
                return $"✓ LLM: {_config.Model} responds correctly";
            }
            catch (Exception ex)
            {
                return $"✗ LLM: {ex.Message}";
            }
        }

        if (cmd == "metta")
        {
            if (_mettaEngine == null) return "✗ MeTTa: Not configured";
            var result = await _mettaEngine.ExecuteQueryAsync("!(+ 1 2)", CancellationToken.None);
            return result.Match(
                output => $"✓ MeTTa: Engine working (1+2={output})",
                error => $"✗ MeTTa: {error}");
        }

        if (cmd == "embedding")
        {
            if (_embedding == null) return "✗ Embedding: Not configured";
            try
            {
                var vec = await _embedding.CreateEmbeddingsAsync("test");
                return $"✓ Embedding: {_config.EmbedModel} (dim={vec.Length})";
            }
            catch (Exception ex)
            {
                return $"✗ Embedding: {ex.Message}";
            }
        }

        if (cmd == "all")
        {
            var results = new List<string>
            {
                await RunTestAsync("llm"),
                await RunTestAsync("metta"),
                await RunTestAsync("embedding")
            };
            return "Test Results:\n" + string.Join("\n", results);
        }

        return $"Unknown test: {testSpec}. Try 'test llm', 'test metta', 'test embedding', or 'test all'.";
    }

    /// <summary>Runs the full LLM chat pipeline (delegated to ChatSubsystem).</summary>
    private Task<string> ChatAsync(string input) => _chatSub.ChatAsync(input);

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!", "stop" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a Tapo device type is a camera (for RTSP streaming).
    /// </summary>
    private static bool IsCameraDeviceType(Ouroboros.Providers.Tapo.TapoDeviceType deviceType) =>
        deviceType is Ouroboros.Providers.Tapo.TapoDeviceType.C100
            or Ouroboros.Providers.Tapo.TapoDeviceType.C200
            or Ouroboros.Providers.Tapo.TapoDeviceType.C210
            or Ouroboros.Providers.Tapo.TapoDeviceType.C220
            or Ouroboros.Providers.Tapo.TapoDeviceType.C310
            or Ouroboros.Providers.Tapo.TapoDeviceType.C320
            or Ouroboros.Providers.Tapo.TapoDeviceType.C420
            or Ouroboros.Providers.Tapo.TapoDeviceType.C500
            or Ouroboros.Providers.Tapo.TapoDeviceType.C520;

    public async Task ProcessGoalAsync(string goal)
    {
        var response = await ExecuteAsync(goal);
        await SayWithVoiceAsync(response);
        Say(response);  // Side channel
        _conversationHistory.Add($"Goal: {goal}");
        _conversationHistory.Add($"Ouroboros: {response}");
    }

    /// <summary>
    /// Processes an initial question provided via command line.
    /// </summary>
    public async Task ProcessQuestionAsync(string question)
    {
        var response = await ChatAsync(question);
        await SayWithVoiceAsync(response);
        Say(response);  // Side channel
        _conversationHistory.Add($"User: {question}");
        _conversationHistory.Add($"Ouroboros: {response}");
    }

    /// <summary>
    /// Processes and executes a pipeline DSL string.
    /// </summary>
    public async Task ProcessDslAsync(string dsl)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  📜 Executing DSL: {dsl}\n");
            Console.ResetColor();

            // Explain the DSL first
            var explanation = PipelineDsl.Explain(dsl);
            Console.WriteLine(explanation);

            // Build and execute the pipeline
            if (_embedding != null && _llm != null)
            {
                var store = new TrackedVectorStore();
                var dataSource = DataSource.FromPath(".");
                var branch = new PipelineBranch("ouroboros-dsl", store, dataSource);

                var state = new CliPipelineState
                {
                    Branch = branch,
                    Llm = _llm,
                    Tools = _tools,
                    Embed = _embedding,
                    Trace = _config.Debug,
                    NetworkTracker = _networkTracker  // Enable automatic step reification
                };

                // Initial tracking of the branch
                _networkTracker?.TrackBranch(branch);

                // Track capability usage for self-improvement
                var startTime = DateTime.UtcNow;
                var success = true;

                try
                {
                    var step = PipelineDsl.Build(dsl);
                    state = await step(state);
                }
                catch (Exception stepEx)
                {
                    success = false;
                    throw new InvalidOperationException($"Pipeline step failed: {stepEx.Message}", stepEx);
                }

                // Final update to capture all step events
                if (_networkTracker != null)
                {
                    var trackResult = _networkTracker.UpdateBranch(state.Branch);
                    if (_config.Debug)
                    {
                        var stepEvents = state.Branch.Events.OfType<StepExecutionEvent>().ToList();
                        Console.WriteLine($"  📊 Network state: {trackResult.Value} events reified ({stepEvents.Count} steps tracked)");
                        foreach (var stepEvt in stepEvents.TakeLast(5))
                        {
                            var status = stepEvt.Success ? "✓" : "✗";
                            Console.WriteLine($"      {status} [{stepEvt.TokenName}] {stepEvt.Description} ({stepEvt.DurationMs}ms)");
                        }
                    }
                }

                // Track capability usage for self-improvement
                var duration = DateTime.UtcNow - startTime;
                if (_capabilityRegistry != null)
                {
                    var execResult = AutonomySubsystem.CreateCapabilityPlanExecutionResult(success, duration, dsl);
                    await _capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
                }

                // Update global workspace with execution result
                _globalWorkspace?.AddItem(
                    $"DSL Executed: {dsl[..Math.Min(100, dsl.Length)]}\nDuration: {duration.TotalSeconds:F2}s",
                    WorkspacePriority.Normal,
                    "dsl-execution",
                    new List<string> { "dsl", "pipeline", success ? "success" : "failure" });

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  ✓ Pipeline completed");
                Console.ResetColor();

                // Get last reasoning output
                var lastReasoning = state.Branch.Events.OfType<PipelineReasoningStep>().LastOrDefault();
                if (lastReasoning != null)
                {
                    Console.WriteLine($"\n{lastReasoning.State.Text}");
                    await SayWithVoiceAsync(lastReasoning.State.Text);
                }
                else if (!string.IsNullOrEmpty(state.Output))
                {
                    Console.WriteLine($"\n{state.Output}");
                    await SayWithVoiceAsync(state.Output);
                }
            }
            else
            {
                Console.WriteLine("  ⚠ Cannot execute DSL: LLM or embeddings not available");
            }
        }
        catch (Exception ex)
        {
            // Track failure for self-improvement
            if (_capabilityRegistry != null)
            {
                var execResult = AutonomySubsystem.CreateCapabilityPlanExecutionResult(false, TimeSpan.Zero, dsl);
                await _capabilityRegistry.UpdateCapabilityAsync("pipeline_execution", execResult);
            }

            Console.ForegroundColor = ConsoleColor.Red;
            _output.WriteError($"DSL execution failed: {ex.Message}");
            Console.ResetColor();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION & DIVIDE-AND-CONQUER HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates text using multi-model orchestration if available, falling back to single model.
    /// The orchestrator automatically routes to specialized models (coder, reasoner, summarizer)
    /// based on prompt content analysis.
}