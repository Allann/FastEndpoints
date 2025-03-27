﻿using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0022

namespace FastEndpoints.Messaging.Remote.Testing;

/// <summary>
/// WAF extension methods of integration testing gRPC event/command queues
/// </summary>
public static class Extensions
{
    /// <summary>
    /// enables communicating with a remote gRPC server in the WAF testing environment
    /// </summary>
    /// <param name="s"></param>
    /// <param name="remote">the <see cref="TestServer" /> of the target WAF</param>
    public static IServiceCollection RegisterTestRemote(this IServiceCollection s, TestServer remote)
        => s.AddSingleton(remote.CreateHandler());

    /// <summary>
    /// register test/fake/mock event handlers for integration testing gRPC event queues
    /// </summary>
    /// <typeparam name="TEvent">the type of the event model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test event handler</typeparam>
    public static IServiceCollection RegisterTestEventHandler<TEvent, THandler>(this IServiceCollection s)
        where TEvent : IEvent
        where THandler : class, IEventHandler<TEvent>
        => s.AddSingleton<IEventHandler<TEvent>, THandler>(); //event handlers are always singletons

    /// <summary>
    /// register test/fake/mock command handlers for integration testing gRPC commands
    /// </summary>
    /// <typeparam name="TCommand">the type of the command model to register a test handler for</typeparam>
    /// <typeparam name="THandler">the type of the test command handler</typeparam>
    public static IServiceCollection RegisterTestCommandHandler<TCommand, THandler>(this IServiceCollection s)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
        => s.AddSingleton<ICommandHandler<TCommand>, THandler>(); //command handlers are transient at consumption but, singleton here because we only need to resolve the type when consuming.

    /// <summary>
    /// registers test event receivers for the purpose of testing receipt of events.
    /// </summary>
    public static IServiceCollection RegisterTestEventReceivers(this IServiceCollection s)
        => s.AddSingleton(typeof(IEventReceiver<>), typeof(EventReceiver<>));

    /// <summary>
    /// gets a test event receiver for a given event type.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event</typeparam>
    /// <exception cref="InvalidOperationException">thrown when test event receivers are not registered</exception>
    public static IEventReceiver<TEvent> GetTestEventReceiver<TEvent>(this IServiceProvider provider) where TEvent : IEvent
        => provider.GetService(typeof(IEventReceiver<TEvent>)) as IEventReceiver<TEvent> ??
           throw new InvalidOperationException("Test event receivers are not registered!");
}