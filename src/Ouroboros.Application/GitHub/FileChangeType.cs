namespace Ouroboros.Application.GitHub;

/// <summary>
/// Type of file change operation.
/// </summary>
public enum FileChangeType
{
    /// <summary>Create a new file</summary>
    Create,

    /// <summary>Update an existing file</summary>
    Update,

    /// <summary>Delete a file</summary>
    Delete
}