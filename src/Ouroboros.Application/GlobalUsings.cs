// <copyright file="GlobalUsings.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

// System namespaces
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

// Core monadic and functional programming
global using Ouroboros.Core.Kleisli;
global using Ouroboros.Core.Monads;
global using Ouroboros.Core.Steps;

// Agent
global using Ouroboros.Agent;

// CLI
global using Ouroboros.Application;
global using Ouroboros.Application.Cli;

// Domain models and state management
global using Ouroboros.Domain;
global using Ouroboros.Domain.Events;
global using Ouroboros.Domain.States;
global using Ouroboros.Domain.Vectors;

// Pipeline components
global using Ouroboros.Pipeline.Branches;
global using Ouroboros.Pipeline.Ingestion;
global using Ouroboros.Pipeline.Reasoning;
global using Ouroboros.Providers;

// Tools and providers
global using Ouroboros.Tools;
