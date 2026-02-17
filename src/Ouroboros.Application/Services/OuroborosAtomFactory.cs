namespace Ouroboros.Application.Services;

/// <summary>
/// Factory for creating Ouroboros atom configurations.
/// </summary>
public static class OuroborosAtomFactory
{
    /// <summary>
    /// Creates an Ouroboros that embodies the concept of self-awareness.
    /// </summary>
    public static OuroborosAtom CreateSelfAware(string seedConcept = "consciousness")
    {
        var atom = new OuroborosAtom($"(self-aware \"{seedConcept}\")");

        // Bootstrap self-awareness through recursive consumption
        atom.Consume(core => $"(aware-of {core})");
        atom.Consume(core => $"(aware-of-awareness {core})");
        atom.Consume(core => $"(meta-aware {core})");

        return atom;
    }

    /// <summary>
    /// Creates an Ouroboros that explores recursive identity.
    /// </summary>
    public static OuroborosAtom CreateIdentityExplorer()
    {
        var atom = new OuroborosAtom("(identity (question \"who am I?\"))");

        atom.Consume(core => $"(reflect {core})");
        atom.Consume(core => $"(I-that-reflects {core})");

        return atom;
    }

    /// <summary>
    /// Creates an Ouroboros that embodies the Gödel self-reference.
    /// </summary>
    public static OuroborosAtom CreateGodelian()
    {
        // "This statement refers to itself"
        var atom = new OuroborosAtom("(statement (refers-to SELF))");

        // Apply Gödel numbering-style encoding
        atom.Consume(core => $"(encode (godel-number {core}))");
        atom.Consume(core => $"(decode (statement-about {core}))");

        return atom;
    }

    /// <summary>
    /// Creates an Ouroboros network - multiple atoms aware of each other.
    /// </summary>
    /// <param name="count">Number of atoms in the network.</param>
    /// <returns>List of interconnected Ouroboros atoms.</returns>
    public static List<OuroborosAtom> CreateNetwork(int count = 3)
    {
        var atoms = new List<OuroborosAtom>();

        for (int i = 0; i < count; i++)
        {
            atoms.Add(new OuroborosAtom($"(network-node {i})"));
        }

        // Each atom becomes aware of its neighbors
        for (int i = 0; i < count; i++)
        {
            var prev = atoms[(i - 1 + count) % count];
            var next = atoms[(i + 1) % count];

            atoms[i].Consume(core =>
                $"(aware-of-neighbors {core} (prev {prev.Id}) (next {next.Id}))");
        }

        return atoms;
    }

    /// <summary>
    /// Creates a strange loop Ouroboros based on Hofstadter's concept.
    /// </summary>
    public static OuroborosAtom CreateStrangeLoop()
    {
        var atom = new OuroborosAtom("(level-0 \"base\")");

        // Create tangled hierarchy
        atom.Consume(core => $"(level-1 (emerges-from {core}))");
        atom.Consume(core => $"(level-2 (emerges-from {core}))");
        atom.Consume(core => $"(level-0 (loops-back-to {core}))"); // Strange loop!

        return atom;
    }
}