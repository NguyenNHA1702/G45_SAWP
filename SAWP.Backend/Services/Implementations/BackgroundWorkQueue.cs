using System.Threading.Channels;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>In-memory queue (one per API process); the API's BackgroundWorkService runs the items.</summary>
public class BackgroundWorkQueue : IBackgroundWorkQueue
{
    private readonly Channel<(string, Func<IServiceProvider, CancellationToken, Task>)> _channel =
        Channel.CreateUnbounded<(string, Func<IServiceProvider, CancellationToken, Task>)>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(string description, Func<IServiceProvider, CancellationToken, Task> work)
    {
        if (!_channel.Writer.TryWrite((description, work)))
        {
            throw new InvalidOperationException("The background work queue is closed.");
        }
    }

    public ValueTask<(string Description, Func<IServiceProvider, CancellationToken, Task> Work)> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
