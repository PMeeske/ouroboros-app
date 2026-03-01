using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ouroboros.Abstractions;

namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Pipeline execution, compilation, sandbox testing, and code generation.
/// </summary>
public sealed partial class SelfAssemblyEngine
{
    private string GenerateDefaultNeuronCode(NeuronBlueprint blueprint)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Ouroboros.Domain.Autonomous;");
        sb.AppendLine();
        sb.AppendLine($"namespace Ouroboros.SelfAssembled;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// {blueprint.Description}");
        sb.AppendLine($"/// Auto-assembled: {DateTime.UtcNow:O}");
        sb.AppendLine($"/// Rationale: {blueprint.Rationale}");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public class {blueprint.Name} : Neuron");
        sb.AppendLine("{");
        sb.AppendLine($"    public override string Name => \"{blueprint.Name}\";");
        sb.AppendLine();
        sb.AppendLine("    protected override void ConfigureSubscriptions()");
        sb.AppendLine("    {");

        foreach (var topic in blueprint.SubscribedTopics)
        {
            sb.AppendLine($"        Subscribe(\"{topic}\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected override async Task OnMessageAsync(NeuralMessage message, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine("        // Auto-generated handler logic");
        sb.AppendLine("        var response = $\"[{Name}] Processed: {message.Topic}\";");
        sb.AppendLine("        await PublishAsync(new NeuralMessage($\"{Name}:response\", response), ct);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private async Task ExecuteAssemblyPipelineAsync(Guid proposalId)
    {
        if (!_proposals.TryGetValue(proposalId, out var proposal))
            return;

        try
        {
            // Request approval if needed
            if (proposal.Status == AssemblyProposalStatus.PendingApproval)
            {
                if (_approvalCallback != null)
                {
                    var approved = await _approvalCallback(proposal);
                    if (!approved)
                    {
                        _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Rejected };
                        RecordState(proposalId, AssemblyProposalStatus.Rejected, "Rejected by approval callback");
                        return;
                    }
                }

                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Approved };
                RecordState(proposalId, AssemblyProposalStatus.Approved, "Approved");
            }

            // Stage 4: Security Validation (before compilation)
            RecordState(proposalId, AssemblyProposalStatus.Compiling, "Validating code security");

            var validator = new CodeSecurityValidator(_config.AdditionalForbiddenNamespaces);
            var securityResult = validator.Validate(proposal.GeneratedCode);
            if (!securityResult.IsSuccess)
            {
                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
                RecordState(proposalId, AssemblyProposalStatus.Failed, $"Security validation failed: {securityResult.Error}");
                AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, securityResult.Error!, DateTime.UtcNow));
                return;
            }

            // Stage 5: Compile
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Compiling };
            RecordState(proposalId, AssemblyProposalStatus.Compiling, "Compiling neuron");

            var compileResult = await CompileNeuronAsync(proposal);
            if (!compileResult.IsSuccess)
            {
                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
                RecordState(proposalId, AssemblyProposalStatus.Failed, $"Compilation failed: {compileResult.Error}");
                AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, compileResult.Error!, DateTime.UtcNow));
                return;
            }

            // Stage 6: Test in sandbox
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Testing };
            RecordState(proposalId, AssemblyProposalStatus.Testing, "Testing in sandbox");

            var testResult = await TestInSandboxAsync(compileResult.Value!);
            if (!testResult.IsSuccess)
            {
                _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
                RecordState(proposalId, AssemblyProposalStatus.Failed, $"Testing failed: {testResult.Error}");
                AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, testResult.Error!, DateTime.UtcNow));
                return;
            }

            // Stage 7: Deploy
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Deployed };
            _assembledNeurons[proposal.Blueprint.Name] = compileResult.Value!;
            RecordState(proposalId, AssemblyProposalStatus.Deployed, "Successfully deployed");

            NeuronAssembled?.Invoke(this, new NeuronAssembledEvent(proposalId, proposal.Blueprint.Name, compileResult.Value!, DateTime.UtcNow));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _proposals[proposalId] = proposal with { Status = AssemblyProposalStatus.Failed };
            RecordState(proposalId, AssemblyProposalStatus.Failed, $"Unexpected error: {ex.Message}");
            AssemblyFailed?.Invoke(this, new AssemblyFailedEvent(proposalId, proposal.Blueprint.Name, ex.Message, DateTime.UtcNow));
        }
    }

    private async Task<Result<Type>> CompileNeuronAsync(AssemblyProposal proposal)
    {
        await _compilationLock.WaitAsync();
        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(proposal.GeneratedCode);

            var compilation = CSharpCompilation.Create(
                $"SelfAssembled_{proposal.Blueprint.Name}_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                _defaultReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                    .WithPlatform(Platform.AnyCpu));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();
                return Result<Type>.Failure($"Compilation errors: {string.Join("; ", errors)}");
            }

            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var neuronType = assembly.GetTypes()
                .FirstOrDefault(t => t.IsSubclassOf(typeof(Neuron)) && !t.IsAbstract);

            return neuronType != null
                ? Result<Type>.Success(neuronType)
                : Result<Type>.Failure("No Neuron subclass found in compiled assembly");
        }
        finally
        {
            _compilationLock.Release();
        }
    }

    private async Task<Result<Unit>> TestInSandboxAsync(Type neuronType)
    {
        try
        {
            using var cts = new CancellationTokenSource(_config.SandboxTimeout);

            // Create instance
            var instance = Activator.CreateInstance(neuronType) as Neuron;
            if (instance == null)
            {
                return Result<Unit>.Failure("Failed to create neuron instance");
            }

            // Basic smoke test - verify it doesn't throw on initialization
            // In a real implementation, this would run in an isolated AppDomain or container
            await Task.Delay(100, cts.Token);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            return Result<Unit>.Failure("Sandbox test timed out");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Unit>.Failure($"Sandbox test failed: {ex.Message}");
        }
    }

    private void RecordState(Guid proposalId, AssemblyProposalStatus status, string? details = null)
    {
        var state = new AssemblyState(proposalId, status, DateTime.UtcNow, details);
        _stateHistory.AddOrUpdate(
            proposalId,
            _ => new List<AssemblyState> { state },
            (_, list) => { list.Add(state); return list; });
    }

    private static List<MetadataReference> GetDefaultReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(Enumerable).Assembly,
            typeof(Neuron).Assembly,
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("netstandard")
        };

        return assemblies
            .Where(a => !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
    }
}
