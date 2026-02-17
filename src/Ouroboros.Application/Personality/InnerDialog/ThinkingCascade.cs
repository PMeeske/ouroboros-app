using System.Text;

namespace Ouroboros.Application.Personality;

/// <summary>
/// A cascade of linked thoughts forming a neuro-symbolic reasoning chain.
/// </summary>
public sealed class ThinkingCascade
{
    private readonly List<NeuronActivation> _activations = [];
    private readonly Dictionary<string, List<string>> _links = new(); // parent -> children

    /// <summary>Gets all activations in the cascade.</summary>
    public IReadOnlyList<NeuronActivation> Activations => _activations.AsReadOnly();

    /// <summary>Gets the depth of the cascade.</summary>
    public int Depth => CalculateMaxDepth();

    /// <summary>Gets the root activation.</summary>
    public NeuronActivation? Root => _activations.FirstOrDefault(a => a.ParentThoughtId == null);

    /// <summary>Adds an activation to the cascade.</summary>
    public void AddActivation(NeuronActivation activation)
    {
        _activations.Add(activation);
        if (activation.ParentThoughtId != null)
        {
            if (!_links.ContainsKey(activation.ParentThoughtId))
                _links[activation.ParentThoughtId] = [];
            _links[activation.ParentThoughtId].Add(activation.ThoughtId);
        }
    }

    /// <summary>Gets children of a given activation.</summary>
    public IEnumerable<NeuronActivation> GetChildren(string thoughtId) =>
        _links.TryGetValue(thoughtId, out var childIds)
            ? _activations.Where(a => childIds.Contains(a.ThoughtId))
            : Enumerable.Empty<NeuronActivation>();

    /// <summary>Gets the path from root to a specific activation.</summary>
    public List<NeuronActivation> GetPath(string thoughtId)
    {
        var path = new List<NeuronActivation>();
        var current = _activations.FirstOrDefault(a => a.ThoughtId == thoughtId);

        while (current != null)
        {
            path.Insert(0, current);
            current = current.ParentThoughtId != null
                ? _activations.FirstOrDefault(a => a.ThoughtId == current.ParentThoughtId)
                : null;
        }

        return path;
    }

    /// <summary>Composes the cascade into a narrative thought stream.</summary>
    public string ComposeNarrative()
    {
        if (_activations.Count == 0) return string.Empty;

        var root = Root;
        if (root == null) return _activations[0].Content;

        var sb = new StringBuilder();
        ComposeNarrativeRecursive(root, sb, 0);
        return sb.ToString().Trim();
    }

    private void ComposeNarrativeRecursive(NeuronActivation activation, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);
        var connector = depth == 0 ? "" : "→ ";

        sb.AppendLine($"{indent}{connector}{activation.Content}");

        foreach (var child in GetChildren(activation.ThoughtId).OrderBy(c => c.Timestamp))
        {
            ComposeNarrativeRecursive(child, sb, depth + 1);
        }
    }

    private int CalculateMaxDepth()
    {
        if (Root == null) return 0;
        return CalculateDepthRecursive(Root.ThoughtId, 1);
    }

    private int CalculateDepthRecursive(string thoughtId, int currentDepth)
    {
        var children = GetChildren(thoughtId).ToList();
        if (children.Count == 0) return currentDepth;
        return children.Max(c => CalculateDepthRecursive(c.ThoughtId, currentDepth + 1));
    }
}