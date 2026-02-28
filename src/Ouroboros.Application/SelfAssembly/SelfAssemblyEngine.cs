using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ouroboros.Abstractions;
using Ouroboros.Application.Extensions;

namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Self-assembly engine that dynamically creates new neurons at runtime.
/// Uses a staged pipeline: Propose → Validate → Approve → Compile → Test → Deploy.
/// </summary>
public sealed partial class SelfAssemblyEngine : IAsyncDisposable
{
    private readonly SelfAssemblyConfig _config;
    private readonly ConcurrentDictionary<Guid, AssemblyProposal> _proposals = new();
    private readonly ConcurrentDictionary<Guid, List<AssemblyState>> _stateHistory = new();
    private readonly ConcurrentDictionary<string, Type> _assembledNeurons = new();
    private readonly List<MetadataReference> _defaultReferences;
    private readonly SemaphoreSlim _compilationLock = new(1, 1);

    // Callbacks for external integration
    private Func<NeuronBlueprint, Task<MeTTaValidation>>? _mettaValidator;
    private Func<NeuronBlueprint, Task<string>>? _codeGenerator;
    private Func<AssemblyProposal, Task<bool>>? _approvalCallback;

    // Events
    public event EventHandler<NeuronAssembledEvent>? NeuronAssembled;
    public event EventHandler<AssemblyFailedEvent>? AssemblyFailed;

    public SelfAssemblyEngine(SelfAssemblyConfig? config = null)
    {
        _config = config ?? new SelfAssemblyConfig();
        _defaultReferences = GetDefaultReferences();
    }

    /// <summary>
    /// Configure MeTTa-based blueprint validation.
    /// </summary>
    public void SetMeTTaValidator(Func<NeuronBlueprint, Task<MeTTaValidation>> validator)
    {
        _mettaValidator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    /// <summary>
    /// Configure code generator (typically LLM-based).
    /// </summary>
    public void SetCodeGenerator(Func<NeuronBlueprint, Task<string>> generator)
    {
        _codeGenerator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <summary>
    /// Configure approval callback for human-in-the-loop.
    /// </summary>
    public void SetApprovalCallback(Func<AssemblyProposal, Task<bool>> callback)
    {
        _approvalCallback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <summary>
    /// Submit a blueprint for assembly. Returns proposal ID.
    /// </summary>
    public async Task<Result<Guid>> SubmitBlueprintAsync(NeuronBlueprint blueprint)
    {
        // Check limits
        if (_assembledNeurons.Count >= _config.MaxAssembledNeurons)
        {
            return Result<Guid>.Failure($"Maximum assembled neurons limit ({_config.MaxAssembledNeurons}) reached");
        }

        // Check for null/empty name
        if (string.IsNullOrWhiteSpace(blueprint.Name))
        {
            return Result<Guid>.Failure("Blueprint must have a name");
        }

        // Check for duplicate names
        if (_assembledNeurons.ContainsKey(blueprint.Name))
        {
            return Result<Guid>.Failure($"Neuron with name '{blueprint.Name}' already exists");
        }

        // Check forbidden capabilities
        foreach (var forbidden in _config.ForbiddenCapabilities)
        {
            if (blueprint.Capabilities.Contains(forbidden))
            {
                return Result<Guid>.Failure($"Blueprint requests forbidden capability: {forbidden}");
            }
        }

        var proposalId = Guid.NewGuid();

        // Stage 1: MeTTa Validation
        var validation = await ValidateBlueprintAsync(blueprint);
        if (!validation.IsValid)
        {
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Validation failed: {string.Join(", ", validation.Violations)}");
            return Result<Guid>.Failure($"Blueprint validation failed: {string.Join(", ", validation.Violations)}");
        }

        if (validation.SafetyScore < _config.MinSafetyScore)
        {
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Safety score {validation.SafetyScore:F2} below minimum {_config.MinSafetyScore:F2}");
            return Result<Guid>.Failure($"Safety score {validation.SafetyScore:F2} below minimum {_config.MinSafetyScore:F2}");
        }

        // Stage 2: Generate Code
        var codeResult = await GenerateCodeAsync(blueprint);
        if (!codeResult.IsSuccess)
        {
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Code generation failed: {codeResult.Error}");
            return Result<Guid>.Failure($"Code generation failed: {codeResult.Error}");
        }

        // Create proposal
        var proposal = new AssemblyProposal(
            proposalId,
            blueprint,
            validation,
            codeResult.Value!,
            DateTime.UtcNow);

        _proposals[proposalId] = proposal;
        RecordState(proposalId, AssemblyProposalStatus.PendingApproval, "Awaiting approval");

        // Stage 3: Check auto-approval
        if (ShouldAutoApprove(proposal))
        {
            Task.Run(() => ExecuteAssemblyPipelineAsync(proposalId))
                .ObserveExceptions("ExecuteAssemblyPipeline");
        }

        return Result<Guid>.Success(proposalId);
    }

    /// <summary>
    /// Approve a pending proposal and trigger assembly.
    /// </summary>
    public async Task<Result<Unit>> ApproveProposalAsync(Guid proposalId)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
        {
            return Result<Unit>.Failure($"Proposal {proposalId} not found");
        }

        if (proposal.Status != AssemblyProposalStatus.PendingApproval)
        {
            return Result<Unit>.Failure($"Proposal is not pending approval (status: {proposal.Status})");
        }

        await ExecuteAssemblyPipelineAsync(proposalId);
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Reject a pending proposal.
    /// </summary>
    public Result<Unit> RejectProposal(Guid proposalId, string reason)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
        {
            return Result<Unit>.Failure($"Proposal {proposalId} not found");
        }

        _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Rejected };
        RecordState(proposalId, AssemblyProposalStatus.Rejected, reason);
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Get all pending proposals.
    /// </summary>
    public IReadOnlyList<AssemblyProposal> GetPendingProposals()
    {
        return _proposals.Values
            .Where(p => p.Status == AssemblyProposalStatus.PendingApproval)
            .ToList();
    }

    /// <summary>
    /// Get state history for a proposal.
    /// </summary>
    public IReadOnlyList<AssemblyState> GetStateHistory(Guid proposalId)
    {
        return _stateHistory.TryGetValue(proposalId, out var history)
            ? history.AsReadOnly()
            : Array.Empty<AssemblyState>();
    }

    /// <summary>
    /// Get a specific proposal by ID.
    /// </summary>
    public AssemblyProposal? GetProposal(Guid proposalId)
    {
        return _proposals.TryGetValue(proposalId, out var proposal) ? proposal : null;
    }

    /// <summary>
    /// Get all assembled neuron types.
    /// </summary>
    public IReadOnlyDictionary<string, Type> GetAssembledNeurons()
    {
        return _assembledNeurons;
    }

    /// <summary>
    /// Create an instance of an assembled neuron.
    /// </summary>
    public Result<Neuron> CreateNeuronInstance(string name)
    {
        if (!_assembledNeurons.TryGetValue(name, out var type))
        {
            return Result<Neuron>.Failure($"Assembled neuron '{name}' not found");
        }

        try
        {
            var instance = Activator.CreateInstance(type) as Neuron;
            return instance != null
                ? Result<Neuron>.Success(instance)
                : Result<Neuron>.Failure($"Failed to create instance of {name}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Neuron>.Failure($"Failed to instantiate {name}: {ex.Message}");
        }
    }

    private async Task<MeTTaValidation> ValidateBlueprintAsync(NeuronBlueprint blueprint)
    {
        if (_mettaValidator != null)
        {
            return await _mettaValidator(blueprint);
        }

        // Default validation without MeTTa
        var violations = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(blueprint.Name))
            violations.Add("Blueprint must have a name");

        if (!blueprint.SubscribedTopics.Any())
            violations.Add("Blueprint must subscribe to at least one topic");

        if (!blueprint.MessageHandlers.Any())
            violations.Add("Blueprint must define at least one message handler");

        if (blueprint.ConfidenceScore < 0.5)
            warnings.Add($"Low confidence score: {blueprint.ConfidenceScore:F2}");

        return new MeTTaValidation(
            !violations.Any(),
            violations.Any() ? 0.0 : 0.8,
            violations,
            warnings,
            "(validate-blueprint default)");
    }

    private async Task<Result<string>> GenerateCodeAsync(NeuronBlueprint blueprint)
    {
        if (_codeGenerator != null)
        {
            try
            {
                var code = await _codeGenerator(blueprint);
                return Result<string>.Success(code);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<string>.Failure($"Code generator error: {ex.Message}");
            }
        }

        // Default template-based generation
        var code_default = GenerateDefaultNeuronCode(blueprint);
        return Result<string>.Success(code_default);
    }

    private bool ShouldAutoApprove(AssemblyProposal proposal)
    {
        if (!_config.AutoApprovalEnabled)
            return false;

        if (proposal.Validation.SafetyScore < _config.AutoApprovalThreshold)
            return false;

        if (proposal.Blueprint.ConfidenceScore < _config.AutoApprovalThreshold)
            return false;

        return true;
    }

    public ValueTask DisposeAsync()
    {
        _compilationLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
