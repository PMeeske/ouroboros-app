// <copyright file="IEventBus.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Interface for the event bus enabling cross-feature communication.
/// Uses reactive extensions (IObservable) for event streaming.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event.</typeparam>
    /// <param name="event">The event to publish.</param>
    void Publish<TEvent>(TEvent @event) where TEvent : class;

    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
    /// <returns>An observable stream of events.</returns>
    IObservable<TEvent> Subscribe<TEvent>() where TEvent : class;

    /// <summary>
    /// Clears all subscriptions and resets the event bus.
    /// </summary>
    void Clear();
}