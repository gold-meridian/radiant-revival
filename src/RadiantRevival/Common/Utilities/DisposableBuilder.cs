using System;
using System.Collections.Generic;

namespace RadiantRevival.Common;

/// <summary>
///     Fluent <see cref="IDisposable"/> action builder.
/// </summary>
internal sealed class DisposableBuilder
{
    private sealed class CompositeDisposable(List<Action> actions) : IDisposable
    {
        public void Dispose()
        {
            foreach (var action in actions)
            {
                action();
            }
        }
    }

    private readonly List<Action> actions = [];

    public static DisposableBuilder Create()
    {
        return new DisposableBuilder();
    }

    public DisposableBuilder AddAction(Action action)
    {
        actions.Add(action);
        return this;
    }

    public DisposableBuilder AddDisposable(IDisposable disposable)
    {
        actions.Add(disposable.Dispose);
        return this;
    }

    public IDisposable Build()
    {
        return new CompositeDisposable(actions);
    }
}
