using Ryujinx.Common.Collections;
using Ryujinx.Common.Host.IO.Stats;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.Common.Host.IO.Memory
{

    /// <summary>
    /// Manages all buffered file-pages by prioritizing them by access frequency
    /// and handling the eviction of active pages if requested
    /// </summary>
    public sealed class PrioritizingRefBufferMemoryManager
        : PageBufferMemoryManager, IRefBufferMemoryManager
    {

        private const int DefaultMaxPriority = 16;

        private readonly long _maxSize;
        private readonly int _maxPriority;

        private readonly PrioritizedLinkedList<BufferedFilePage>[] _tables;
        private readonly Dictionary<ulong, LinkedListNode<BufferedFilePage>> _index;

        private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _evictionLock = new SemaphoreSlim(1, 1);

        private long _currentSize;

        private long _counterEvictedPages = 0;

        public long NumberOfEvictedPages
        {
            get => _counterEvictedPages;
        }

        public PrioritizingRefBufferMemoryManager(long maxSize)
            : this(maxSize, DefaultMaxPriority)
        { }

        public PrioritizingRefBufferMemoryManager(long maxSize, int maxPriority)
        {
            _maxSize = maxSize;
            _maxPriority = maxPriority;

            _tables = new PrioritizedLinkedList<BufferedFilePage>[maxPriority];
            _index = new Dictionary<ulong, LinkedListNode<BufferedFilePage>>();

            for (int i = 0; i < maxPriority; i++)
            {
                _tables[i] = new PrioritizedLinkedList<BufferedFilePage>(i);
            }
        }

        /// <inheritdoc />
        public override IEnumerable<IHostIOStat> GetStats()
        {
            foreach (IHostIOStat baseStat in base.GetStats())
                yield return baseStat;

            yield return new CounterHostIOStat("NumberOfEvictedPages", _counterEvictedPages);
        }

        /// <inheritdoc />
        public void RefPage(BufferedFilePage page)
        {
            LinkedListNode<BufferedFilePage> node;

            _indexLock.Wait();
            try
            {
                if (!_index.TryGetValue(page.GlobalIdentifier, out node))
                {
                    _tables[0].Lock.Wait();
                    try
                    {
                        node = _tables[0].AddLast(page);
                    }
                    finally
                    {
                        _tables[0].Lock.Release();
                    }

                    _index[page.GlobalIdentifier] = node;

                    Interlocked.Add(ref _currentSize, page.Size);

                    return;
                }
            }
            finally
            {
                _indexLock.Release();
            }

            lock (node)
            {
                var currentTable = (PrioritizedLinkedList<BufferedFilePage>)node.List;
                int nextPriority = currentTable.Priority + 1;

                if (nextPriority < _maxPriority)
                {
                    PrioritizedLinkedList<BufferedFilePage> nextTable = _tables[nextPriority];

                    currentTable.Lock.Wait();
                    try
                    {
                        currentTable.Remove(node);
                    }
                    finally
                    {
                        currentTable.Lock.Release();
                    }

                    nextTable.Lock.Wait();
                    try
                    {
                        nextTable.AddFirst(node);
                    }
                    finally
                    {
                        nextTable.Lock.Release();
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool DerefPage(BufferedFilePage page)
        {
            LinkedListNode<BufferedFilePage> node;

            _indexLock.Wait();
            try
            {
                if (!_index.TryGetValue(page.GlobalIdentifier, out node))
                    return false;
            }
            finally
            {
                _indexLock.Release();
            }

            RemovePageNode(node);

            return true;
        }

        /// <inheritdoc />
        public int EnsureConstraints()
        {
            int evictions = 0;
            bool hasEvicted = true;

            if (!ShouldEvict())
                return 0;

            _evictionLock.Wait();
            try
            {
                while (ShouldEvict() && hasEvicted)
                {
                    hasEvicted = EvictOldestPage();

                    if (hasEvicted)
                    {
                        evictions++;
                    }
                }
            }
            finally
            {
                _evictionLock.Release();
            }

            return evictions;
        }

        /// <inheritdoc />
        public bool ShouldEvict()
        {
            return _maxSize > 0 && _maxSize <= _currentSize;
        }

        private bool EvictOldestPage()
        {
            for (int i = 0; i < _maxPriority; i++)
            {
                LinkedListNode<BufferedFilePage> oldestNode = _tables[i].Last;

                if (oldestNode != null)
                {
                    oldestNode.Value.Evict();
                    Interlocked.Increment(ref _counterEvictedPages);

                    return true;
                }
            }

            return false;
        }

        private void RemovePageNode(LinkedListNode<BufferedFilePage> node)
        {
            var table = (PrioritizedLinkedList<BufferedFilePage>)node.List;

            _indexLock.Wait();
            try
            {
                _index.Remove(node.Value.GlobalIdentifier);
            }
            finally
            {
                _indexLock.Release();
            }

            table.Lock.Wait();
            try
            {
                table.Remove(node);
            }
            finally
            {
                table.Lock.Release();
            }

            Interlocked.Add(ref _currentSize, -node.Value.Size);
        }

        class PrioritizedLinkedList<T> : LinkedList<T>
        {

            public int Priority { get; }

            public SemaphoreSlim Lock { get; }

            public PrioritizedLinkedList(int priority)
                : base()
            {
                Priority = priority;
                Lock = new SemaphoreSlim(1, 1);
            }

        }

    }

}
