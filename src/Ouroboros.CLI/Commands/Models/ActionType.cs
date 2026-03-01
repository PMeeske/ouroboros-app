// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

/// <summary>
/// Identifies the category of action parsed from user input.
/// Used by CommandRoutingSubsystem to dispatch to the correct handler.
/// </summary>
public enum ActionType
{
    Chat,
    Help,
    ListSkills,
    ListTools,
    LearnTopic,
    CreateTool,
    UseTool,
    RunSkill,
    Suggest,
    Plan,
    Execute,
    Status,
    Mood,
    Remember,
    Recall,
    Query,
    // Unified CLI commands
    Ask,
    Pipeline,
    Metta,
    Orchestrate,
    Network,
    Dag,
    Affect,
    Environment,
    Maintenance,
    Policy,
    Explain,
    Test,
    Consciousness,
    Tokens,
    Fetch,
    Process,
    // Self-execution and sub-agent commands
    SelfExec,
    SubAgent,
    Epic,
    Goal,
    Delegate,
    SelfModel,
    Evaluate,
    // Emergent behavior commands
    Emergence,
    Dream,
    Introspect,
    // Push mode commands
    Approve,
    Reject,
    Pending,
    PushPause,
    PushResume,
    CoordinatorCommand,
    // Self-modification
    SaveCode,
    SaveThought,
    ReadMyCode,
    SearchMyCode,
    AnalyzeCode,
    // Index commands
    Reindex,
    ReindexIncremental,
    IndexSearch,
    IndexStats,
    // AGI subsystem commands
    AgiStatus,
    AgiCouncil,
    AgiIntrospect,
    AgiWorld,
    AgiCoordinate,
    AgiExperience,
    // Prompt optimization
    PromptOptimize,
    // Swarm orchestration
    Swarm
}
