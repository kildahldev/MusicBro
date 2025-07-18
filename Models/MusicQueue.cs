using System.Collections.Concurrent;

namespace MusicBro.Models;

public class MusicQueue
{
    private readonly Queue<Track> _queue = new();
    private readonly object _lock = new();
    
    public Track? CurrentTrack { get; private set; }
    public bool IsPlaying { get; set; }
    public int Count => _queue.Count;
    
    public void Enqueue(Track track)
    {
        lock (_lock)
        {
            _queue.Enqueue(track);
        }
    }
    
    public void EnqueueNext(Track track)
    {
        lock (_lock)
        {
            var temp = new Queue<Track>();
            temp.Enqueue(track);
            
            while (_queue.Count > 0)
            {
                temp.Enqueue(_queue.Dequeue());
            }
            
            while (temp.Count > 0)
            {
                _queue.Enqueue(temp.Dequeue());
            }
        }
    }
    
    public Track? GetNext()
    {
        lock (_lock)
        {
            if (_queue.Count > 0)
            {
                CurrentTrack = _queue.Dequeue();
                return CurrentTrack;
            }
            
            CurrentTrack = null;
            return null;
        }
    }
    
    public void Skip()
    {
        CurrentTrack = null;
        IsPlaying = false;
    }
    
    public void SetCurrentTrack(Track track)
    {
        CurrentTrack = track;
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _queue.Clear();
            CurrentTrack = null;
            IsPlaying = false;
        }
    }
    
    public void ClearQueue()
    {
        lock (_lock)
        {
            _queue.Clear();
        }
    }
    
    public List<Track> GetTracks()
    {
        lock (_lock)
        {
            return _queue.ToList();
        }
    }
    
    public void Shuffle()
    {
        lock (_lock)
        {
            if (_queue.Count <= 1) return;
            
            var tracks = _queue.ToList();
            _queue.Clear();
            
            var random = new Random();
            for (int i = tracks.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (tracks[i], tracks[j]) = (tracks[j], tracks[i]);
            }
            
            foreach (var track in tracks)
            {
                _queue.Enqueue(track);
            }
        }
    }
}