using Ryujinx.IO.Host.Buffer;
using Ryujinx.IO.Host.Buffer.Memory;
using Ryujinx.IO.Host.Stats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ryujinx.IO.Host
{

    public sealed class HostFileSystem : IDisposable
    {

        private readonly bool _bufferingEnabled;
        private readonly bool _prefetchingEnabled;

        private readonly ConcurrentDictionary<string, BufferedFile> _files;
        private readonly PrioritizingRefBufferMemoryManager _memoryManager;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool _disposed = false;

        public event IOProgressChangedDelegate PrefetchProgressChanged;

        public bool RequestsCancelled => _cancellationTokenSource.IsCancellationRequested;

        public HostFileSystem(bool bufferingEnabled, bool prefetchingEnabled, long maxBufferSize)
        {
            _bufferingEnabled = bufferingEnabled;
            _prefetchingEnabled = prefetchingEnabled;

            _files = new ConcurrentDictionary<string, BufferedFile>();
            _memoryManager = new PrioritizingRefBufferMemoryManager(maxBufferSize);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public static HostFileSystem CreateDefault()
        {
            return new HostFileSystem(false, false, 0);
        }

        /// <summary>
        /// Returns statistics about this instance of <see cref="HostFileSystem"/> containing
        /// the statistics of the internal <see cref="IBufferMemoryManager"/>-instances
        /// and all open <see cref="BufferedFile"/>-instances
        /// </summary>
        public IEnumerable<IHostIOStat> GetStats()
        {
            foreach (IHostIOStat stat in _memoryManager.GetStats())
                yield return stat;

            IEnumerable<IHostIOStat> openFileStats = _files.Values.ToArray()
                .SelectMany(x => x.GetStats())
                .GroupBy(x => x.Name)
                .Select(g => g.Aggregate((x, y) => x.Add(y.Value)));

            foreach (IHostIOStat openFileStat in openFileStats)
                yield return openFileStat;
        }

        /// <summary>
        /// Request to abort all running asynchrounous requests
        /// </summary>
        public void AbortRequests()
        {
            _cancellationTokenSource.Cancel();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (!_disposed)
            {
                _disposed = true;

                if (_files != null)
                {
                    foreach (var bufferedFile in _files.Values)
                    {
                        bufferedFile.Dispose();
                    }

                    _files.Clear();
                }
            }
        }

        /// <summary>
        /// Open a file for reading.
        /// 
        /// Depending on the configuration of the <see cref="HostFileSystem"/>, this could return a <see cref="FileStream"/> or
        /// a buffering <see cref="BufferedFileStream"/>, which buffers the file-contents and may have partially or fully prefetched
        /// the contents of the requested file.
        /// </summary>
        /// <param name="path">The path of the file</param>
        /// <returns>A stream capable of randomly reading the contents of the requested file</returns>
        public Stream OpenFileRead(string path)
        {
            if (_bufferingEnabled)
            {
                if (_prefetchingEnabled)
                {
                    return OpenFileReadBuffered(path, true);
                }
                else
                {
                    return OpenFileReadBuffered(path, false);
                }
            }
            else
            {
                return OpenFileReadDirect(path);
            }
        }

        public byte[] ReadAllBytes(string path)
        {
            using Stream stream = OpenFileRead(path);
            byte[] buffer = new byte[stream.Length];

            stream.ReadExactly(buffer, 0, buffer.Length);

            return buffer;
        }

        public IEnumerable<string> ReadAllLines(string path)
        {
            using Stream stream = OpenFileRead(path);
            using StreamReader reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                yield return reader.ReadLine();
            }
        }

        public IEnumerable<string> ReadAllLines(string path, Encoding encoding)
        {
            using Stream stream = OpenFileRead(path);
            using StreamReader reader = new StreamReader(stream, encoding);

            while (!reader.EndOfStream)
            {
                yield return reader.ReadLine();
            }
        }

        private Stream OpenFileReadBuffered(string path, bool prefetch)
        {
            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            BufferedFileOptions options = new BufferedFileOptions()
            {
                MemoryManager = _memoryManager,
                PageSize = Environment.SystemPageSize,
                SequentialScan = prefetch,
                DisposeWithLastStream = false,
                CloseFileStreamAfterPrefetching = true,
            };

            BufferedFile bufferedFile = _files.GetOrAdd(path, _ => new BufferedFile(path, options));

            bufferedFile.Open();

            if (prefetch)
            {
                // In case the prefetching failed for reasons other than manual cancellation, fall back to default I/O.
                // If the failure was due to cancellation, return NULL
                if (!bufferedFile.Prefetch(cancellationToken, PrefetchProgressChanged))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        return OpenFileReadDirect(path);
                    }

                    return null;
                }
            }

            return bufferedFile.CreateStream();
        }

        private Stream OpenFileReadDirect(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

    }

}
