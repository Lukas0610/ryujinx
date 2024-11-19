using ARMeilleure.CodeGen;
using ARMeilleure.CodeGen.Linking;
using ARMeilleure.CodeGen.Unwinding;
using ARMeilleure.Common;
using ARMeilleure.Memory;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ARMeilleure.Translation.PTC
{

    /// <summary>
    /// Streaming Profiled Translation Cache (SPTC)
    /// </summary>
    class Sptc : IPtc
    {

        private const uint InternalVersion = 2; //! To be incremented manually for each change to the ARMeilleure project.
        private const long DataOffset = 0x1000;

        private const int ReportRefreshRate = 50; // ms.

        private const string TitleIdTextDefault = "0000000000000000";
        private const string BuildIdHashTextDefault = "0000000000000000";
        private const string DisplayVersionDefault = "0";

        public static readonly Encoding StreamEncoding = Encoding.UTF8;

        private static readonly byte[] FileMagic = "SptcData\0\0\0\xff"u8.ToArray();
        private static readonly byte[] EntryBeginMagic = "EntryBegin\xff"u8.ToArray();
        private static readonly byte[] EntryEndMagic = "EntryEnd\0\0\xff"u8.ToArray();

        private readonly PtcCacheFlags _cacheFlags;

        private bool _disposed;

        private SptcProfiler _profiler;

        private MemoryManagerType _memoryMode;

        private readonly Lock _writeCompiledFunctionLock;
        private readonly List<ulong> _writtenCompiledFunctions;

        private readonly BlockingCollection<EnqueuedCompiledFunction> _bgSaveQueue;
        private readonly Thread _bgSaveThread;

        private FileStream _fileStream;
        private BinaryReader _fileReader;
        private BinaryWriter _fileWriter;

        // Progress reporting helpers.
        private volatile int _translateCount;
        private volatile int _translateTotalCount;

        public event Action<PtcLoadingState, int, int> PtcStateChanged;

        public IPtcProfiler Profiler => _profiler;

        public PtcState State { get; private set; }

        public string CacheFileName { get; private set; }

        public string ProfileFileName { get; private set; }

        public string TitleIdText { get; private set; }

        public string BuildIdHashText { get; private set; }

        public string DisplayVersion { get; private set; }

        static Sptc()
        {
            Debug.Assert(FileMagic.Length == 13);
        }

        public Sptc(PtcCacheFlags cacheFlags)
        {
            _profiler = new SptcProfiler(this);

            _cacheFlags = cacheFlags;

            _writeCompiledFunctionLock = new Lock();
            _writtenCompiledFunctions = new List<ulong>();

            _bgSaveQueue = new BlockingCollection<EnqueuedCompiledFunction>(new ConcurrentQueue<EnqueuedCompiledFunction>());
            _bgSaveThread = new Thread(BackgroundSaveThreadStart)
            {
                Name = "CPU.SPTC.BackgroundSaveThread",
                Priority = ThreadPriority.BelowNormal,
                IsBackground = true,
            };

            _disposed = false;

            TitleIdText = TitleIdTextDefault;
            BuildIdHashText = BuildIdHashTextDefault;
            DisplayVersion = DisplayVersionDefault;

            CacheFileName = string.Empty;
            ProfileFileName = string.Empty;
        }

        public void Continue()
        {
            Enable();
        }

        public void BeginExecution() { }

        public void Enable()
        {
            if (State != PtcState.Stopped)
            {
                State = PtcState.Enabled;
            }
        }

        public void Disable()
        {
            if (State != PtcState.Stopped)
            {
                State = PtcState.Disabled;
            }
        }

        public void Stop()
        {
            State = PtcState.Stopped;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _bgSaveQueue.CompleteAdding();

                _bgSaveThread.Join();
                _fileStream.Flush();

                DisposeStreams();
            }
        }

        public void Initialize(string titleIdText, string buildIdHashText, string displayVersion, bool enabled, MemoryManagerType memoryMode)
        {
            _profiler.Wait();
            _profiler.ClearEntries();

            Logger.Info?.Print(LogClass.Ptc, $"Initializing Streaming Profiled Translation Cache (enabled: {enabled}).");

            if (!enabled || string.IsNullOrEmpty(titleIdText) || titleIdText == TitleIdTextDefault)
            {
                TitleIdText = TitleIdTextDefault;
                BuildIdHashText = BuildIdHashTextDefault;
                DisplayVersion = DisplayVersionDefault;

                CacheFileName = string.Empty;
                ProfileFileName = string.Empty;

                State = PtcState.Disabled;

                return;
            }

            TitleIdText = titleIdText;
            BuildIdHashText = !string.IsNullOrEmpty(buildIdHashText) ? buildIdHashText : BuildIdHashTextDefault;
            DisplayVersion = !string.IsNullOrEmpty(displayVersion) ? displayVersion : DisplayVersionDefault;
            _memoryMode = memoryMode;

            string cacheDirectory = Path.Combine(AppDataManager.GamesDirPath, TitleIdText, "cache", "cpu");

            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }

            string cacheFileBasePath = Path.Combine(cacheDirectory, BuildIdHashText);

            CacheFileName = $"{cacheFileBasePath}.sptcdata";
            ProfileFileName = $"{cacheFileBasePath}.sptcprofile";

            _bgSaveThread.Start();

            _profiler.Load();

            State = PtcState.Enabled;
        }

        public void LoadTranslations(Translator translator)
        {
            FileInfo cacheFileInfo = new(CacheFileName);

            if (cacheFileInfo.Exists && cacheFileInfo.Length > 0)
            {
                LoadImpl(translator);
            }
            else
            {
                OpenStream();
                InvalidateStream();
            }
        }

        public void MakeTranslations(Translator translator)
        {
            var profiledFuncsToTranslate = _profiler.GetProfiledFuncsToTranslate(translator.Functions);

            _translateCount = 0;
            _translateTotalCount = profiledFuncsToTranslate.Count;

            if (_translateTotalCount == 0)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                return;
            }

            int degreeOfParallelism = Environment.ProcessorCount;

            // If there are enough cores lying around, we leave one alone for other tasks.
            if (degreeOfParallelism > 4)
            {
                degreeOfParallelism--;
            }

            Logger.Info?.Print(LogClass.Ptc, $"{_translateCount} of {_translateTotalCount} functions translated | Thread count: {degreeOfParallelism}");

            PtcStateChanged?.Invoke(PtcLoadingState.Start, _translateCount, _translateTotalCount);

            using AutoResetEvent progressReportEvent = new(false);

            Thread progressReportThread = new(ReportProgress)
            {
                Name = "CPU.SPTC.TranslationProgressReporterThread",
                Priority = ThreadPriority.Lowest,
                IsBackground = true,
            };

            progressReportThread.Start(progressReportEvent);

            void TranslateThreadStart()
            {
                while (profiledFuncsToTranslate.TryDequeue(out var item))
                {
                    ulong address = item.address;

                    Debug.Assert(_profiler.IsAddressInStaticCodeRange(address));

                    TranslatedFunction func = translator.Translate(address, item.funcProfile.Mode, item.funcProfile.HighCq);

                    bool isAddressUnique = translator.Functions.TryAdd(address, func.GuestSize, func);

                    Debug.Assert(isAddressUnique, $"The address 0x{address:X16} is not unique.");

                    Interlocked.Increment(ref _translateCount);

                    translator.RegisterFunction(address, func);
                    _writtenCompiledFunctions.Add(address);

                    if (State != PtcState.Enabled)
                    {
                        break;
                    }
                }
            }

            List<Thread> threads = new();

            for (int i = 0; i < degreeOfParallelism; i++)
            {
                Thread thread = new(TranslateThreadStart)
                {
                    Name = $"CPU.SPTC.TranslationThread.{i}",
                    IsBackground = true,
                };

                threads.Add(thread);
            }

            Stopwatch sw = Stopwatch.StartNew();

            foreach (var thread in threads)
            {
                thread.Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            threads.Clear();

            progressReportEvent.Set();
            progressReportThread.Join();

            sw.Stop();

            PtcStateChanged?.Invoke(PtcLoadingState.Loaded, _translateCount, _translateTotalCount);

            Logger.Info?.Print(LogClass.Ptc, $"{_translateCount} of {_translateTotalCount} functions translated in {sw.Elapsed.TotalSeconds} seconds using {degreeOfParallelism} thread(s)");
        }

        private void ReportProgress(object state)
        {
            AutoResetEvent progressReportEvent = (AutoResetEvent)state;

            int count = 0;

            do
            {
                int newCount = _translateCount;

                if (count != newCount)
                {
                    PtcStateChanged?.Invoke(PtcLoadingState.Loading, newCount, _translateTotalCount);
                    count = newCount;
                }
            }
            while (!progressReportEvent.WaitOne(ReportRefreshRate));
        }

        public void WriteCompiledFunction(ulong address, ulong guestSize, Hash128 hash, bool highCq, CompiledFunction compiledFunc)
        {
            lock (_writeCompiledFunctionLock)
            {
                if (!_writtenCompiledFunctions.Contains(address))
                {
                    _writtenCompiledFunctions.Add(address);
                    _bgSaveQueue.Add(new EnqueuedCompiledFunction(address, guestSize, hash, highCq, compiledFunc));
                }
            }
        }

        private void OpenStream()
        {
            _fileStream = new FileStream(CacheFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            _fileReader = new BinaryReader(_fileStream, StreamEncoding, true);
            _fileWriter = new BinaryWriter(_fileStream, StreamEncoding, true);
        }

        private bool LoadImpl(Translator translator)
        {
            OpenStream();

            byte[] writtenMagic = _fileReader.ReadBytes(FileMagic.Length);
            if (!FileMagic.SequenceEqual(writtenMagic))
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

            if (fileHeader.Endianness != PtcUtils.GetEndianness())
            {
                InvalidateStream();
                return false;
            }

            if (fileHeader.FeatureInfo != PtcUtils.GetFeatureInfo())
            {
                InvalidateStream();
                return false;
            }

            if (fileHeader.MemoryManagerMode != GetMemoryManagerMode())
            {
                InvalidateStream();
                return false;
            }

            if (fileHeader.OSPlatform != PtcUtils.GetOSPlatform())
            {
                InvalidateStream();
                return false;
            }

            if (fileHeader.Architecture != (uint)RuntimeInformation.ProcessArchitecture)
            {
                InvalidateStream();
                return false;
            }

            if (fileHeader.Flags != _cacheFlags)
            {
                InvalidateStream();
                return false;
            }

            _fileStream.Seek(DataOffset, SeekOrigin.Begin);

            ulong numOfStaleFunctions = 0;
            ulong numOfPerformedStubs = 0;
            ulong numOfStubbedFunctions = 0;
            ulong numOfCorruptedEntries = 0;

            Stopwatch chrono = new();

            chrono.Start();

            while (_fileStream.Position < _fileStream.Length)
            {
                long currentEntryStreamStart = _fileStream.Position;

                byte[] writtenEntryBeginMagic = _fileReader.ReadBytes(EntryBeginMagic.Length);
                if (!EntryBeginMagic.SequenceEqual(writtenEntryBeginMagic))
                {
                    // An invalid entry begin magic block indicates a full corruption
                    // (e.g. misaligned writes) of the cache-file.
                    //
                    // Consider this and all following entries to be corrupted and drop them by
                    // truncating the file at the begin of the current entry.
                    _fileStream.SetLength(currentEntryStreamStart);
                    Interlocked.Increment(ref numOfCorruptedEntries);

                    break;
                }

                if (!PtcUtils.TryDeserializeHashedStructure(_fileStream, out StreamedInfoEntry infoEntry))
                {
                    // An invalid entry header indicates a partial corruption of the current entry.
                    //
                    // Consider this and all following entries to be corrupted and drop them by
                    // truncating the file at the begin of the current entry.
                    _fileStream.SetLength(currentEntryStreamStart);
                    Interlocked.Increment(ref numOfCorruptedEntries);

                    break;
                }

                bool hasCodeChanged = (infoEntry.Hash != PtcUtils.ComputeHash(translator.Memory, infoEntry.Address, infoEntry.GuestSize));
                bool hasHighCqChanged = (!infoEntry.HighCq && _profiler.ProfiledFuncs.TryGetValue(infoEntry.Address, out var value) && value.HighCq);

                if (hasCodeChanged || hasHighCqChanged)
                {
                    infoEntry.Stubbed = true;
                    Interlocked.Increment(ref numOfPerformedStubs);
                }

                byte[] code = new byte[infoEntry.CodeLength];

                _fileStream.ReadExactly(code, 0, code.Length);

                Hash128 computedCodeHash = XXHash128.ComputeHash(code);
                if (computedCodeHash == infoEntry.CodeHash)
                {
                    if (infoEntry.CodeIsCompressed)
                    {
                        code = PtcUtils.Decompress(code);
                    }
                }
                else
                {
                    infoEntry.Stubbed = true;
                    Interlocked.Increment(ref numOfPerformedStubs);
                }

                RelocEntry[] relocEntries = new RelocEntry[infoEntry.RelocEntriesCount];
                UnwindPushEntry[] unwindPushEntries = new UnwindPushEntry[infoEntry.UnwindPushEntriesCount];

                for (int i = 0; i < relocEntries.Length; i++)
                {
                    if (PtcUtils.TryDeserializeHashedStructure(_fileStream, out StreamedRelocEntry streamedRelocEntry))
                    {
                        relocEntries[i] = new RelocEntry(streamedRelocEntry.Position,
                                                         new Symbol(streamedRelocEntry.SymbolType, streamedRelocEntry.SymbolValue));
                    }
                    else
                    {
                        infoEntry.Stubbed = true;
                        Interlocked.Increment(ref numOfPerformedStubs);
                    }
                }

                for (int i = 0; i < unwindPushEntries.Length; i++)
                {
                    if (PtcUtils.TryDeserializeHashedStructure(_fileStream, out StreamedUnwindPushEntry streamedUnwindPushEntry))
                    {
                        unwindPushEntries[i] = new UnwindPushEntry(streamedUnwindPushEntry.PseudoOp,
                                                                   streamedUnwindPushEntry.PrologOffset,
                                                                   streamedUnwindPushEntry.RegIndex,
                                                                   streamedUnwindPushEntry.StackOffsetOrAllocSize);
                    }
                    else
                    {
                        infoEntry.Stubbed = true;
                        Interlocked.Increment(ref numOfPerformedStubs);
                    }
                }

                byte[] writtenEntryEndMagic = _fileReader.ReadBytes(EntryEndMagic.Length);
                if (!EntryEndMagic.SequenceEqual(writtenEntryEndMagic))
                {
                    // An invalid entry end magic block indicates an interruption while writing the entry to file.
                    //
                    // Consider this and all following entries to be corrupted and drop them by
                    // truncating the file at the begin of the current entry.
                    _fileStream.SetLength(currentEntryStreamStart);
                    Interlocked.Increment(ref numOfCorruptedEntries);

                    break;
                }

                if (!infoEntry.Stubbed)
                {
                    Counter<uint> callCounter = null;

                    if (relocEntries.Length != 0)
                    {
                        PtcUtils.PatchCode(translator, code, relocEntries, out callCounter);
                    }

                    UnwindInfo unwindInfo = new(unwindPushEntries, infoEntry.UnwindPrologSize);
                    TranslatedFunction func = PtcUtils.FastTranslate(code, callCounter, infoEntry.GuestSize, unwindInfo, infoEntry.HighCq);

                    translator.Functions.AddOrUpdate(infoEntry.Address, func.GuestSize, func, (key, oldFunc) =>
                    {
                        translator.EnqueueForDeletion(key, oldFunc);
                        Interlocked.Increment(ref numOfStaleFunctions);

                        return func;
                    });

                    translator.RegisterFunction(infoEntry.Address, func);
                    _writtenCompiledFunctions.Add(infoEntry.Address);
                }
                else
                {
                    long nextEntryStreamStart = _fileStream.Position;

                    // Return to the beginning of the current entry a overwrite the existing header
                    _fileStream.Seek(currentEntryStreamStart, SeekOrigin.Begin);

                    _fileStream.Write(EntryBeginMagic);
                    PtcUtils.SerializeStructure(_fileStream, infoEntry);

                    // Skip to the beginning of the next entry
                    _fileStream.Seek(nextEntryStreamStart, SeekOrigin.Begin);

                    Interlocked.Increment(ref numOfStubbedFunctions);
                }

                TimeSpan chronoElapsed = chrono.Elapsed;

                if (chronoElapsed.TotalSeconds >= 1.0)
                {
                    Logger.Info?.Print(LogClass.Ptc, $"Loading functions from streaming translation cache, {translator.Functions.Count} function(s) done");
                    chrono.Restart();
                }
            }

            chrono.Stop();

            Debug.Assert(_fileStream.Position == _fileStream.Length);

            Logger.Info?.Print(LogClass.Ptc,
                $"{translator.Functions.Count} functions loaded from streaming translation cache" +
                $"(size={_fileStream.Length}, stale={numOfStaleFunctions}, stubs={numOfPerformedStubs}, stubbed={numOfStubbedFunctions}, corrupted={numOfCorruptedEntries})");

            return true;
        }

        private void InvalidateStream()
        {
            // Truncate file
            _fileStream.SetLength(0);
            _fileStream.Seek(0, SeekOrigin.Begin);

            // Write new magic + header
            _fileStream.SetLength(DataOffset);
            _fileWriter.Write(FileMagic);

            PtcUtils.SerializeStructure(_fileStream, new FileHeader()
            {
                InternalVersion = InternalVersion,

                Endianness = PtcUtils.GetEndianness(),
                FeatureInfo = PtcUtils.GetFeatureInfo(),
                MemoryManagerMode = GetMemoryManagerMode(),
                OSPlatform = PtcUtils.GetOSPlatform(),
                Architecture = (uint)RuntimeInformation.ProcessArchitecture,
                Flags = _cacheFlags,
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
            foreach (EnqueuedCompiledFunction compiledFuncData in _bgSaveQueue.GetConsumingEnumerable())
            {
                ulong address = compiledFuncData.Address;
                ulong guestSize = compiledFuncData.GuestSize;
                bool highCq = compiledFuncData.HighCq;
                CompiledFunction compiledFunc = compiledFuncData.CompiledFunc;

                RelocInfo relocInfo = compiledFunc.RelocInfo;
                UnwindInfo unwindInfo = compiledFunc.UnwindInfo;

                byte[] code = compiledFunc.Code;
                byte[] compressedCode = PtcUtils.Compress(code);
                bool codeIsCompressed = false;

                // Only store compressed data if its smaller than the uncompressed data
                if (compressedCode.Length < code.Length)
                {
                    code = compressedCode;
                    codeIsCompressed = true;
                }

                Hash128 codeHash = XXHash128.ComputeHash(code);

                _fileStream.Write(EntryBeginMagic);

                PtcUtils.SerializeHashedStructure(_fileStream, new StreamedInfoEntry()
                {
                    Address = address,
                    GuestSize = guestSize,
                    Hash = compiledFuncData.Hash,
                    HighCq = highCq,
                    Stubbed = false,
                    CodeIsCompressed = codeIsCompressed,
                    CodeLength = code.Length,
                    CodeHash = codeHash,
                    RelocEntriesCount = relocInfo.Entries.Length,
                    UnwindPushEntriesCount = unwindInfo.PushEntries.Length,
                    UnwindPrologSize = unwindInfo.PrologSize,
                });

                _fileStream.Write(code);

                foreach (RelocEntry entry in relocInfo.Entries)
                {
                    PtcUtils.SerializeHashedStructure(_fileStream, new StreamedRelocEntry()
                    {
                        Position = entry.Position,
                        SymbolType = entry.Symbol.Type,
                        SymbolValue = entry.Symbol.Type != SymbolType.None ? entry.Symbol.Value : 0,
                    });
                }

                foreach (UnwindPushEntry entry in unwindInfo.PushEntries)
                {
                    PtcUtils.SerializeHashedStructure(_fileStream, new StreamedUnwindPushEntry()
                    {
                        PseudoOp = entry.PseudoOp,
                        PrologOffset = entry.PrologOffset,
                        RegIndex = entry.RegIndex,
                        StackOffsetOrAllocSize = entry.StackOffsetOrAllocSize,
                    });
                }

                _fileStream.Write(EntryEndMagic);
                _fileStream.Flush();
            }
        }

        private byte GetMemoryManagerMode()
        {
            return (byte)_memoryMode;
        }

        private class EnqueuedCompiledFunction
        {

            public readonly ulong Address;
            public readonly ulong GuestSize;
            public readonly Hash128 Hash;
            public readonly bool HighCq;
            public readonly CompiledFunction CompiledFunc;

            public EnqueuedCompiledFunction(ulong address, ulong guestSize, Hash128 hash, bool highCq, CompiledFunction compiledFunc)
            {
                Address = address;
                GuestSize = guestSize;
                Hash = hash;
                HighCq = highCq;
                CompiledFunc = compiledFunc;
            }

        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileHeader
        {
            public uint InternalVersion;

            public bool Endianness;
            public PtcFeatureInfo FeatureInfo;
            public byte MemoryManagerMode;
            public uint OSPlatform;
            public uint Architecture;
            public PtcCacheFlags Flags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StreamedInfoEntry
        {
            public ulong Address;
            public ulong GuestSize;
            public Hash128 Hash;
            public bool HighCq;
            public bool Stubbed;
            public bool CodeIsCompressed;
            public int CodeLength;
            public Hash128 CodeHash;
            public int RelocEntriesCount;
            public int UnwindPushEntriesCount;
            public int UnwindPrologSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StreamedRelocEntry
        {
            public int Position;
            public ulong SymbolValue;
            public SymbolType SymbolType;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StreamedUnwindPushEntry
        {
            public UnwindPseudoOp PseudoOp;
            public int PrologOffset;
            public int RegIndex;
            public int StackOffsetOrAllocSize;
        }

    }

}
