global using System;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Linq;
global using System.Threading.Tasks;

// Foundation
// Note: Ouroboros.Abstractions.Monads NOT globally imported — Option<T> conflicts with System.CommandLine.Option<T>
// Use Maybe<T> bridge (Ouroboros.CLI.Maybe<T>) or fully qualify where needed
global using Ouroboros.Core.EmbodiedInteraction;
global using Ouroboros.Core.Kleisli;
global using Ouroboros.Core.Monads;
global using Ouroboros.Core.Steps;
global using Ouroboros.Domain;
global using Ouroboros.Domain.Events;
global using Ouroboros.Domain.States;
global using Ouroboros.Domain.Vectors;
global using Ouroboros.Tools;
global using Ouroboros.Tools.MeTTa;

// Engine
global using Ouroboros.Agent;
global using Ouroboros.Agent.MetaAI;
global using Ouroboros.Agent.MetaAI.Affect;
global using Ouroboros.Agent.MetaAI.SelfModel;
global using Ouroboros.Network;
global using Ouroboros.Pipeline.Branches;
global using Ouroboros.Pipeline.Council;
global using Ouroboros.Pipeline.Learning;
global using Ouroboros.Pipeline.Metacognition;
global using Ouroboros.Pipeline.MultiAgent;
global using Ouroboros.Pipeline.WorldModel;
global using Ouroboros.Providers;

// Application
global using Ouroboros.Application;
global using Ouroboros.Application.Mcp;
global using Ouroboros.Application.Personality;
global using Ouroboros.Application.SelfAssembly;
global using Ouroboros.Application.Services;
global using Ouroboros.Application.Tools;

// CLI
global using Ouroboros.CLI.Abstractions;
global using Ouroboros.CLI.Infrastructure;
global using Ouroboros.CLI.Options;
global using Ouroboros.CLI.Resources;
global using Ouroboros.CLI.Subsystems;
global using Ouroboros.Options;
