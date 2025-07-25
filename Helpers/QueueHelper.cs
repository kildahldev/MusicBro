using MusicBro.Services;

namespace MusicBro.Helpers;

public static class QueueHelper
{
    public static void StartAutoPlaylistIfEmpty(QueueManager queueManager)
    {
        // Start autoplaylist if queue is empty
        if (!queueManager.Queue.IsPlaying && queueManager.Queue.Count == 0)
        {
            ThreadPool.QueueUserWorkItem(_ => {
                queueManager.PlayNextAsync().Wait();
            });
        }
    }
}