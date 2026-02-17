using System.Reactive.Linq;
using System.Threading.Channels;

namespace Ouroboros.Application;

/// <summary>
/// Extension methods for Channel readers to convert to observables.
/// </summary>
internal static class ChannelExtensions
{
    public static IObservable<T> AsObservable<T>(this ChannelReader<T> reader)
    {
        return Observable.Create<T>(async (observer, cancellationToken) =>
        {
            try
            {
                await foreach (T? item in reader.ReadAllAsync(cancellationToken))
                {
                    observer.OnNext(item);
                }
                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }
}