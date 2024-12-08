using Ryujinx.IO.Host.Buffer.Memory;
using Ryujinx.IO.Host.Stats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Ryujinx.IO.Host.Buffer
{

    public sealed class BufferedFile : IDisposable
    {

        private BufferedFilePage[] _pages;

        private readonly bool _sequentialScan;
        private readonly bool _disposeWithLastStream;
        private readonly bool _closeFileStreamAfterPrefetching;

        private readonly Lock _lock = new();

        private FileStream _fileStream;
        private long _length;
        private int _pageCount;

        private int _prefetched = 0;
        private int _opened = 0;
        private int _disposed = 0;

        private long _counterFileReadCount = 0;
        private long _counterFileReadLength = 0;
        private long _counterBufferedReadCount = 0;
        private long _counterBufferedReadLength = 0;

        private int _openStreams = 0;

        internal readonly int PageSize;
        internal readonly IBufferMemoryManager MemoryManager;
        internal readonly IRefBufferMemoryManager RefMemoryManager;

        public string Path { get; }

        public long NumberOfFileReads
        {
            get => _counterFileReadCount;
        }

        public long SizeOfFileReads
        {
            get => _counterFileReadLength;
        }

        public long NumberOfBufferedReads
        {
            get => _counterBufferedReadCount;
        }

        public long SizeOfBufferedReads
        {
            get => _counterBufferedReadLength;
        }

        internal long Length
        {
            get => _length;
        }

        internal int PageCount
        {
            get => _pageCount;
        }

        public BufferedFile(string path)
            : this(path, new BufferedFileOptions())
        { }

        public BufferedFile(string path, BufferedFileOptions options)
        {
            _sequentialScan = options.SequentialScan;
            _disposeWithLastStream = options.DisposeWithLastStream;
            _closeFileStreamAfterPrefetching = options.CloseFileStreamAfterPrefetching;

            PageSize = options.PageSize;
            MemoryManager = options.MemoryManager;

            if (MemoryManager is IRefBufferMemoryManager refMemoryManager)
                RefMemoryManager = refMemoryManager;

            Path = path;
        }

        /// <summary>
        /// Open the file and initialize all fields required for proper operation
        /// </summary>
        public void Open()
        {
            _lock.Enter();
            try
            {
                // Only allow to initialize/open an instance once.
                // Run this check within the locked region to ensure that all callers have a fully initialized/open instance
                if (Interlocked.CompareExchange(ref _opened, 1, 0) != 0)
                    return;

                var fileStreamOptions = _sequentialScan ? FileOptions.SequentialScan : FileOptions.None;

                _fileStream = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read, PageSize, fileStreamOptions);

                _length = _fileStream.Length;
                _pageCount = (int)(_length / PageSize) + (_length % PageSize > 0 ? 1 : 0);

                _pages = new BufferedFilePage[_pageCount];

                for (int i = 0; i < _pageCount; i++)
                {
                    _pages[i] = new BufferedFilePage(this);
                }
            }
            finally
            {
                _lock.Exit();
            }
        }

        /// <summary>
        /// Create a stream for reading from the buffer and to retrieve pages from file which
        /// have not been read to memory yet or have been evicted previously.
        /// </summary>
        /// <returns>A read-only stream to read from the buffer</returns>
        public Stream CreateStream()
        {
            ObjectDisposedException.ThrowIf(IsDisposed(), this);

            int streamIndex = Interlocked.Increment(ref _openStreams);

            return new BufferedFileStream(this, streamIndex);
        }

        /// <summary>
        /// Prefe
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="progressDelegate"></param>
        /// <returns></returns>
        public bool Prefetch(CancellationToken cancellationToken,
                             IOProgressChangedDelegate progressDelegate)
        {
            ObjectDisposedException.ThrowIf(IsDisposed(), this);

            if (Interlocked.CompareExchange(ref _prefetched, 1, 0) != 0)
            {
                // Only run prefetch on this file-object once
                return true;
            }

            bool success = true;

            DateTime lastTime = DateTime.Now;
            long lastPosition = 0;

            int pageIndexCounter = 0;

            _fileStream.Seek(0, SeekOrigin.Begin);

            while (_fileStream.Position < _length)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    success = false;
                    break;
                }

                // Check if, according the the memory-manager, pages would need to be evicted if
                // we were to prefetch more data from the underlying file. If that's the case,
                // gracefully stop prefetching more data
                if (RefMemoryManager != null && RefMemoryManager.ShouldEvict())
                {
                    success = true;
                    break;
                }

                int pageIndex = pageIndexCounter++;

                unsafe
                {
                    RefMemoryManager?.EnsureConstraints();

                    BufferedFilePage page = ReadPageFromFile(pageIndex, false);

                    RefMemoryManager?.RefPage(page);
                }

                if (progressDelegate != null)
                {
                    DispatchProgress(false);
                }
            }

            if (progressDelegate != null)
            {
                DispatchProgress(true);
            }

            if (!_closeFileStreamAfterPrefetching)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }

            return success;

            void DispatchProgress(bool final)
            {
                DateTime nowTime = DateTime.Now;
                TimeSpan timeDelta = nowTime - lastTime;

                if (timeDelta.TotalSeconds >= .5 || _fileStream.Position >= _length)
                {
                    long current = _fileStream.Position;
                    long dataDelta = current - lastPosition;
                    double speed = dataDelta / timeDelta.TotalSeconds;

                    progressDelegate(this, new(Path, current, _length, speed));

                    lastTime = nowTime;
                    lastPosition = _fileStream.Position;
                }
                else if (final)
                {
                    // In case we skip reading parts of the file, still indicate
                    // the file to have been fully read into memory for better UX
                    progressDelegate(this, new(Path, _length, _length, 0));
                }
            }
        }

        /// <summary>
        /// Creates a dictionary containing statistics about the current <see cref="BufferedFile"/>-instance
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IHostIOStat> GetStats()
        {
            yield return new CounterHostIOStat("NumberOfFileReads", _counterFileReadCount);
            yield return new SizeHostIOStat("SizeOfFileReads", _counterFileReadLength);
            yield return new CounterHostIOStat("NumberOfBufferedReads", _counterBufferedReadCount);
            yield return new SizeHostIOStat("SizeOfBufferedReads", _counterBufferedReadLength);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                _fileStream?.Dispose();
                _fileStream = null;

                if (_pages != null)
                {
                    for (int i = 0; i < _pages.Length; i++)
                    {
                        BufferedFilePage page = _pages[i];

                        MemoryManager.FreePage(page);

                        if (MemoryManager is IRefBufferMemoryManager refMemoryManager)
                            refMemoryManager.DerefPage(page);
                    }

                    _pages = null;
                }
            }
        }

        internal BufferedFilePage ReadPageFromFile(int pageIndex, bool seek)
        {
            BufferedFilePage page = _pages[pageIndex];

            MemoryManager.AllocPage(page);

            if (seek)
            {
                long pagePositionInFile = (long)pageIndex * PageSize;
                _fileStream.Seek(pagePositionInFile, SeekOrigin.Begin);
            }

            int readCount;

            unsafe
            {
                readCount = _fileStream.Read(new Span<byte>(page.Memory, PageSize));
            }

            Interlocked.Increment(ref _counterFileReadCount);
            Interlocked.Add(ref _counterFileReadLength, readCount);

            return page;
        }

        internal void CountBufferedRead(int length)
        {
            Interlocked.Increment(ref _counterBufferedReadCount);
            Interlocked.Add(ref _counterBufferedReadLength, length);
        }

        internal bool IsDisposed()
        {
            return Interlocked.CompareExchange(ref _disposed, 0, 0) == 1;
        }

        internal BufferedFilePage GetPage(int index)
        {
            return _pages[index];
        }

        internal void DisposeStream(int streamIndex)
        {
            if (_disposeWithLastStream)
            {
                // When the last stream is being disposed, also dispose the
                // buffered file if that was requested by the owner
                if (Interlocked.Decrement(ref _openStreams) == 0)
                {
                    Dispose();
                }
            }
        }

    }

}
