// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.Options;

/// <summary>
/// Lightweight <see cref="IVoiceOptions"/> adapter used by the <c>immersive</c> CLI subcommand.
/// Carries only the options exposed by <c>CreateImmersiveCommand</c> in Program.cs.
/// </summary>
public sealed class ImmersiveCommandVoiceOptions : IVoiceOptions
{
    public string Persona       { get; set; } = "Iaret";
    public string Model         { get; set; } = "llama3:latest";
    public string Endpoint      { get; set; } = "http://localhost:11434";
    public string EmbedModel    { get; set; } = "nomic-embed-text";
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";

    public bool Voice     { get; set; }
    public bool VoiceOnly { get; set; }
    public bool LocalTts  { get; set; }
    public bool VoiceLoop { get; set; }

    // Extended options (beyond IVoiceOptions) consumed by ImmersiveMode / RoomMode
    public bool Avatar   { get; set; } = true;
    public int  AvatarPort { get; set; } = 9471;
    public bool RoomMode { get; set; }
}
