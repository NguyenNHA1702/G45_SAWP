namespace Services.Interfaces;

/// <summary>
/// Work run after the HTTP response has been sent, in its own dependency-injection scope. Used where the
/// response must not depend on (or wait for) the work, e.g. emailing a recovery link: the answer to
/// "forgot password" must take the same time whether or not the address is registered (BR-04).
/// </summary>
public interface IBackgroundWorkQueue
{
    void Enqueue(string description, Func<IServiceProvider, CancellationToken, Task> work);

    /// <summary>Waits for the next item (used by the worker).</summary>
    ValueTask<(string Description, Func<IServiceProvider, CancellationToken, Task> Work)> DequeueAsync(CancellationToken ct);
}
