﻿using Bogus;
using Xunit;

namespace FastEndpoints.Testing;

public abstract class StateFixture : IAsyncLifetime, IFaker
{
    static readonly Faker _faker = new();

    /// <inheritdoc />
    public Faker Fake => _faker;

    /// <summary>
    /// override this method if you'd like to do some one-time setup for the fixture.
    /// it is run before any of the test-methods of the class is executed.
    /// </summary>
    protected virtual ValueTask SetupAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time teardown for the fixture.
    /// it is run after all test-methods have executed.
    /// </summary>
    protected virtual ValueTask TearDownAsync()
        => ValueTask.CompletedTask;

    ValueTask IAsyncLifetime.InitializeAsync()
        => SetupAsync();

    ValueTask IAsyncDisposable.DisposeAsync()
        => TearDownAsync();
}