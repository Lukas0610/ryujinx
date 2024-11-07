using System;
using System.Diagnostics;
using System.IO;

namespace Ryujinx.Common.Host.IO
{

    internal class BufferedFileStream : Stream
    {

        private readonly int _streamIndex;

        private BufferedFile _file;

        private bool _disposed = false;
        private long _position = 0;

        public override bool CanRead { get; } = true;

        public override bool CanSeek { get; } = true;

        public override bool CanWrite { get; } = false;

        public override long Length
        {
            get => _file.Length;
        }

        public override long Position
        {
            get => _position;
            set
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                if (value < 0 || value >= _file.Length)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _position = value;
            }
        }

        internal BufferedFileStream(BufferedFile file, int streamIndex)
        {
            _file = file;
            _streamIndex = streamIndex;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(new Span<byte>(buffer, offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            ObjectDisposedException.ThrowIf(_file.IsDisposed(), this);

            int totalBufferLength = buffer.Length;
            int totalRead = 0;

            while (buffer.Length > 0 && _position < _file.Length)
            {
                (int pageIndex, int pageBufferOffset) = GetPageIndexAndOffset(_position);

                // Clamp read-count to available data and page alignment
                int currentRead = (int)Math.Min(_file.Length - _position, _file.PageSize - pageBufferOffset);

                // Do not read more data than the page can contain
                if (buffer.Length < currentRead)
                    currentRead = buffer.Length;

#if DEBUG
                Debug.Assert(currentRead > 0);
#endif

                BufferedFilePage page = _file.GetPage(pageIndex);

                int pageSpanMaxLength = _file.PageSize - pageBufferOffset;
                int pageSpanLength = Math.Min(pageSpanMaxLength, currentRead);

                bool pageAcquired = false;

                page.AcquireLock();
                try
                {
                    unsafe
                    {
                        byte* pageMemory = page.Memory;

                        if (pageMemory == null)
                        {
                            _file.RefMemoryManager?.EnsureConstraints();

                            pageMemory = _file.ReadPageFromFile(pageIndex, true).Memory;
                            pageAcquired = true;
                        }
                        else
                        {
                            _file.CountBufferedRead(pageSpanLength);
                        }

                        _file.RefMemoryManager?.RefPage(page);

                        var pageReadSpan = new ReadOnlySpan<byte>(pageMemory + pageBufferOffset, pageSpanLength);

                        pageReadSpan.CopyTo(buffer);
                    }
                }
                finally
                {
                    // If we just acquired the page from the underlying file I/O stream, "upgrade" the lock to allow
                    // for infinite concurrent access. If the page was already acquired, only release a single lock.
                    if (pageAcquired)
                    {
                        page.UpgradeLock();
                    }
                    else
                    {
                        page.ReleaseLock();
                    }
                }

                // Advance buffer position
                buffer = buffer[currentRead..];

                // Increment counters
                _position += currentRead;
                totalRead += currentRead;

#if DEBUG
                Debug.Assert(_position <= _file.Length);
                Debug.Assert(totalRead <= totalBufferLength);
#endif
            }

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = _position;

            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPosition = offset;
                    break;
                case SeekOrigin.Current:
                    newPosition = _position + offset;
                    break;
                case SeekOrigin.End:
                    newPosition = _file.Length + offset;
                    break;
            }

            if (newPosition < 0 || newPosition >= _file.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return _position;
        }

        public override void Flush() { }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _file?.DisposeStream(_streamIndex);
                _file = null;
            }

            base.Dispose(disposing);
        }

        private (int, int) GetPageIndexAndOffset(long offset)
        {
            int pageIndex = (int)(offset / _file.PageSize);
            if (pageIndex > _file.PageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            int pageBufferOffset = (int)(offset % _file.PageSize);

            return (pageIndex, pageBufferOffset);
        }

    }

}
