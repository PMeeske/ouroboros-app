// Copyright (c) Ouroboros. All rights reserved.

using System.Net.Http;
using Ouroboros.Abstractions.Monads;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Partial class containing the monadic Step constructs of <see cref="OuroborosAgent"/>.
/// These expose core agent actions as <c>Step&lt;string, Result&lt;string, string&gt;&gt;</c>
/// so they can be composed using Pipeline/Step combinators across the system.
/// </summary>
public sealed partial class OuroborosAgent
{
    // ═══════════════════════════════════════════════════════════════════════════
    // MONADIC STEP CONSTRUCTS (for functional composition)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Functional step to ask a question. Input: question string. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> AskStep()
        => async question => await AskResultAsync(question);

    /// <summary>
    /// Functional step to run a Pipeline DSL expression. Input: DSL string. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> PipelineStep()
        => async dsl => await RunPipelineResultAsync(dsl);

    /// <summary>
    /// Functional step to execute a MeTTa expression directly. Input: expression. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> MeTTaExpressionStep()
        => async expression => await RunMeTTaExpressionResultAsync(expression);

    /// <summary>
    /// Functional step to query the MeTTa engine. Input: query string. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> MeTTaQueryStep()
        => async query => await QueryMeTTaResultAsync(query);

    /// <summary>
    /// Functional step to orchestrate a multi-step goal. Input: goal. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> OrchestrateStep()
        => async goal =>
        {
            try
            {
                var text = await OrchestrateAsync(goal);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to produce a plan for a goal. Input: goal. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> PlanStep()
        => async goal =>
        {
            try
            {
                var text = await PlanAsync(goal);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to execute a goal with planning. Input: goal. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> ExecuteStep()
        => async goal =>
        {
            try
            {
                var text = await ExecuteAsync(goal);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to fetch research (arXiv). Input: query. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> FetchResearchStep()
        => async query =>
        {
            try
            {
                var text = await FetchResearchAsync(query);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to process large input via divide-and-conquer. Input: text-or-filepath. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> ProcessLargeInputStep()
        => async input =>
        {
            try
            {
                var text = await ProcessLargeInputAsync(input);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to remember information. Input: info. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> RememberStep()
        => async info =>
        {
            try
            {
                var text = await RememberAsync(info);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to recall information. Input: topic. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> RecallStep()
        => async topic =>
        {
            try
            {
                var text = await RecallAsync(topic);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step to run a named skill. Input: skill name. Output: Result text.
    /// </summary>
    public Step<string, Result<string, string>> RunSkillStep()
        => async skillName =>
        {
            try
            {
                var text = await RunSkillAsync(skillName);
                return Result<string, string>.Success(text);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };

    /// <summary>
    /// Functional step factory to invoke a specific tool. The returned step takes the tool input string.
    /// </summary>
    public Step<string, Result<string, string>> UseToolStep(string toolName)
        => async toolInput =>
        {
            try
            {
                var tool = _tools.Get(toolName) ?? _tools.All.FirstOrDefault(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
                if (tool is null)
                    return Result<string, string>.Failure($"Tool not found: {toolName}");

                Result<string, string> result = await tool.InvokeAsync(toolInput ?? string.Empty);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
            catch (HttpRequestException ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        };
}
