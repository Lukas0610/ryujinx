using Ryujinx.IO.Host.Stats;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.IO.Host.Buffer.Memory
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

        private readonly Lock _indexLock = new Lock();
        private readonly Lock _evictionLock = new Lock();

        private long _currentSize;

        private long _counterEvictedPages = 0;

        public long NumberOfEvictedPages
        {
            get => Interlocked.Read(ref _counterEvictedPages);
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

            yield return new CounterHostIOStat("NumberOfEvictedPages", Interlocked.Read(ref _counterEvictedPages));
        }

        /// <inheritdoc />
        public void RefPage(BufferedFilePage page)
        {
            LinkedListNode<BufferedFilePage> node;

            _indexLock.Enter();
            try
            {
                if (!_index.TryGetValue(page.GlobalIdentifier, out node))
                {
                    _tables[0].Lock.Enter();
                    try
                    {
                        node = _tables[0].AddLast(page);
                    }
                    finally
                    {
                        _tables[0].Lock.Exit();
                    }

                    _index[page.GlobalIdentifier] = node;

                    Interlocked.Add(ref _currentSize, page.Size);

                    return;
                }
            }
            finally
            {
                _indexLock.Exit();
            }

            lock (node)
            {
                var currentTable = (PrioritizedLinkedList<BufferedFilePage>)node.List;
                int nextPriority = currentTable.Priority + 1;

                if (nextPriority < _maxPriority)
                {
                    PrioritizedLinkedList<BufferedFilePage> nextTable = _tables[nextPriority];

                    currentTable.Lock.Enter();
                    try
                    {
                        currentTable.Remove(node);
                    }
                    finally
                    {
                        currentTable.Lock.Exit();
                    }

                    nextTable.Lock.Enter();
                    try
                    {
                        nextTable.AddFirst(node);
                    }
                    finally
                    {
                        nextTable.Lock.Exit();
                    }
                }
            }
        }

        /// <inheritdoc />
        public bool DerefPage(BufferedFilePage page)
        {
            LinkedListNode<BufferedFilePage> node;

            _indexLock.Enter();
            try
            {
                if (!_index.TryGetValue(page.GlobalIdentifier, out node))
                    return false;
            }
            finally
            {
                _indexLock.Exit();
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

            _evictionLock.Enter();
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
                _evictionLock.Exit();
            }

            return evictions;
        }

        /// <inheritdoc />
        public bool ShouldEvict()
        {
            return _maxSize > 0 && _maxSize <= Interlocked.Read(ref _currentSize);
        }

        private bool EvictOldestPage()
        {
            for (int i = 0; i < _maxPriority; i++)
            {
                LinkedListNode<BufferedFilePage> oldestNode = _tables[i].Last;

                if (oldestNode != null)
                {
                    oldestNode.Value.Evict();
                    RemovePageNode(oldestNode);

                    Interlocked.Increment(ref _counterEvictedPages);

                    return true;
                }
            }

            return false;
        }

        private void RemovePageNode(LinkedListNode<BufferedFilePage> node)
        {
            var table = (PrioritizedLinkedList<BufferedFilePage>)node.List;

            _indexLock.Enter();
            try
            {
                _index.Remove(node.Value.GlobalIdentifier);
            }
            finally
            {
                _indexLock.Exit();
            }

            table.Lock.Enter();
            try
            {
                table.Remove(node);
            }
            finally
            {
                table.Lock.Exit();
            }

            Interlocked.Add(ref _currentSize, -node.Value.Size);
        }

        class PrioritizedLinkedList<T> : LinkedList<T>
        {

            public int Priority { get; }

            public Lock Lock { get; }

            public PrioritizedLinkedList(int priority)
                : base()
            {
                Priority = priority;
                Lock = new Lock();
            }

        }

    }

}
