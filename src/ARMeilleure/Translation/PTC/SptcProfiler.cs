using ARMeilleure.State;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ARMeilleure.Translation.PTC
{

    /// <summary>
    /// Streaming Profiled Translation Cache (SPTC) profiler and info cache
    /// </summary>
    class SptcProfiler : IPtcProfiler
    {

        private const uint InternalVersion = 0; //! Not to be incremented manually for each change to the ARMeilleure project.
        private const long DataOffset = 0x1000;

        private static readonly byte[] FileMagic = "SptcProfile\xff"u8.ToArray();

        private readonly Sptc _ptc;
        private readonly ManualResetEvent _waitEvent;
        private readonly Lock _lock;

        private readonly BlockingCollection<FuncProfile> _bgSaveQueue;
        private readonly Thread _bgSaveThread;

        private FileStream _fileStream;
        private BinaryReader _fileReader;
        private BinaryWriter _fileWriter;

        private bool _disposed;

        public Dictionary<ulong, FuncProfile> ProfiledFuncs { get; private set; }

        public bool Enabled { get; private set; }

        public ulong StaticCodeStart { get; set; }

        public ulong StaticCodeSize { get; set; }

        static SptcProfiler()
        {
            Debug.Assert(FileMagic.Length == 13);
        }

        public SptcProfiler(Sptc ptc)
        {
            _ptc = ptc;
            _waitEvent = new ManualResetEvent(true);
            _lock = new Lock();

            _bgSaveQueue = new BlockingCollection<FuncProfile>(new ConcurrentQueue<FuncProfile>());
            _bgSaveThread = new Thread(BackgroundSaveThreadStart)
            {
                Name = "CPU.SPTC.Profiler.BackgroundSaveThread",
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true,
            };

            _disposed = false;

            ProfiledFuncs = new Dictionary<ulong, FuncProfile>();
            Enabled = false;
        }

        public void Load()
        {
            FileInfo fileInfo = new(_ptc.ProfileFileName);

            if (!fileInfo.Exists || fileInfo.Length == 0 || !LoadImpl(false))
            {
                OpenStream();
                InvalidateStream();
            }
        }

        public void Start()
        {
            if (_ptc.State == PtcState.Enabled)
            {
                Enabled = true;
                _bgSaveThread.Start();
            }
        }

        public void Stop()
        {
            _bgSaveQueue.CompleteAdding();
            _bgSaveThread.Join();

            _fileStream.Flush();

            Enabled = false;
        }

        public void PerformSave() { }

        public void Wait()
        {
            _waitEvent.WaitOne();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                DisposeStreams();

                _waitEvent.WaitOne();
                _waitEvent.Dispose();
            }
        }

        public void AddEntry(ulong address, ExecutionMode mode, bool highCq)
        {
            if (IsAddressInStaticCodeRange(address))
            {
                Debug.Assert(!highCq);

                lock (_lock)
                {
                    FuncProfile profile = new(address, mode, highCq: false);

                    if (ProfiledFuncs.TryAdd(address, profile))
                        _bgSaveQueue.Add(profile);
                }
            }
        }

        public void UpdateEntry(ulong address, ExecutionMode mode, bool highCq) { }

        public bool IsAddressInStaticCodeRange(ulong address)
        {
            return address >= StaticCodeStart && address < StaticCodeStart + StaticCodeSize;
        }

        public ConcurrentQueue<(ulong address, FuncProfile funcProfile)> GetProfiledFuncsToTranslate(TranslatorCache<TranslatedFunction> funcs)
        {
            var profiledFuncsToTranslate = new ConcurrentQueue<(ulong address, FuncProfile funcProfile)>();

            foreach (var profiledFunc in ProfiledFuncs)
            {
                if (!funcs.ContainsKey(profiledFunc.Key))
                {
                    profiledFuncsToTranslate.Enqueue((profiledFunc.Key, profiledFunc.Value));
                }
            }

            return profiledFuncsToTranslate;
        }

        public void ClearEntries()
        {
            ProfiledFuncs.Clear();
            ProfiledFuncs.TrimExcess();
        }

        private void OpenStream()
        {
            _fileStream = new FileStream(_ptc.ProfileFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            _fileReader = new BinaryReader(_fileStream, Sptc.StreamEncoding, true);
            _fileWriter = new BinaryWriter(_fileStream, Sptc.StreamEncoding, true);
        }

        private bool LoadImpl(bool tryBackup)
        {
            OpenStream();

            byte[] presentMagic = _fileReader.ReadBytes(FileMagic.Length);

            if (!FileMagic.SequenceEqual(presentMagic))
            {
                InvalidateStream();
                return false;
            }

            FileHeader fileHeader = PtcUtils.DeserializeStructure<FileHeader>(_fileStream);

            if (fileHeader.InternalVersion != InternalVersion)
            {
                InvalidateStream();
                return false;
            }

            _fileStream.Seek(DataOffset, SeekOrigin.Begin);

            ProfiledFuncs = new Dictionary<ulong, FuncProfile>();

            while (_fileStream.Position < _fileStream.Length)
            {
                long funcProfileStreamStart = _fileStream.Position;

                if (PtcUtils.TryDeserializeHashedStructure(_fileStream, out FuncProfile profile))
                {
                    if (!ProfiledFuncs.ContainsKey(profile.Address))
                    {
                        ProfiledFuncs.Add(profile.Address, profile);
                    }
                }
                else
                {
                    profile.Valid = false;

                    // Overwrite current entry
                    _fileStream.Seek(funcProfileStreamStart, SeekOrigin.Begin);
                    PtcUtils.SerializeHashedStructure(_fileStream, profile);
                }
            }

            Debug.Assert(_fileStream.Position == _fileStream.Length);

            return true;
        }

        private void InvalidateStream()
        {
            // Truncate file
            _fileStream.SetLength(0L);
            _fileStream.Seek(0, SeekOrigin.Begin);

            // Write new magic + header
            _fileStream.SetLength(DataOffset);
            _fileWriter.Write(FileMagic);

            PtcUtils.SerializeStructure(_fileStream, new FileHeader()
            {
                InternalVersion = InternalVersion
            });

            _fileStream.Flush();
            _fileStream.Seek(DataOffset, SeekOrigin.Begin);
        }

        private void DisposeStreams()
        {
            _fileWriter?.Dispose();
            _fileWriter = null;

            _fileReader?.Dispose();
            _fileReader = null;

            _fileStream?.Close();
            _fileStream?.Dispose();
            _fileStream = null;
        }

        private void BackgroundSaveThreadStart(object state)
        {
            foreach (FuncProfile profile in _bgSaveQueue.GetConsumingEnumerable())
            {
                PtcUtils.SerializeHashedStructure(_fileStream, profile);
                _fileStream.Flush();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileHeader
        {
            public uint InternalVersion;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct FuncProfile
        {
            public bool Valid;
            public ulong Address;
            public ExecutionMode Mode;
            public bool HighCq;

            public FuncProfile(ulong address, ExecutionMode mode, bool highCq)
            {
                Valid = true;
                Address = address;
                Mode = mode;
                HighCq = highCq;
            }

        }

    }

}
