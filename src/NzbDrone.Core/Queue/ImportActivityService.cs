using System;
using System.Collections.Concurrent;
using System.Threading;
using NzbDrone.Common.Messaging;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Queue
{
    public class ImportActivityUpdatedEvent : IEvent
    {
        public string DownloadId { get; }
        public string Activity { get; }

        public ImportActivityUpdatedEvent(string downloadId, string activity)
        {
            DownloadId = downloadId;
            Activity = activity;
        }
    }

    public interface IImportActivityService
    {
        IDisposable BeginDownloadScope(string downloadId);
        void SetActivity(string downloadId, string activity);
        void SetCurrentActivity(string activity);
        void ClearActivity(string downloadId);
        string GetActivity(string downloadId);
    }

    public class ImportActivityService : IImportActivityService
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ConcurrentDictionary<string, string> _activities = new(StringComparer.OrdinalIgnoreCase);
        private readonly AsyncLocal<string> _currentDownloadId = new();

        public ImportActivityService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public IDisposable BeginDownloadScope(string downloadId)
        {
            var previous = _currentDownloadId.Value;
            _currentDownloadId.Value = downloadId;
            return new Scope(() => _currentDownloadId.Value = previous);
        }

        public void SetActivity(string downloadId, string activity)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(activity))
            {
                ClearActivity(downloadId);
                return;
            }

            if (_activities.TryGetValue(downloadId, out var existing) &&
                string.Equals(existing, activity, StringComparison.Ordinal))
            {
                return;
            }

            _activities[downloadId] = activity;
            _eventAggregator?.PublishEvent(new ImportActivityUpdatedEvent(downloadId, activity));
        }

        public void SetCurrentActivity(string activity)
        {
            var downloadId = _currentDownloadId.Value;
            if (!string.IsNullOrWhiteSpace(downloadId))
            {
                SetActivity(downloadId, activity);
            }
        }

        public void ClearActivity(string downloadId)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                return;
            }

            if (_activities.TryRemove(downloadId, out _))
            {
                _eventAggregator?.PublishEvent(new ImportActivityUpdatedEvent(downloadId, null));
            }
        }

        public string GetActivity(string downloadId)
        {
            if (string.IsNullOrWhiteSpace(downloadId))
            {
                return null;
            }

            return _activities.TryGetValue(downloadId, out var activity) ? activity : null;
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action _onDispose;

            public Scope(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose();
            }
        }
    }
}
