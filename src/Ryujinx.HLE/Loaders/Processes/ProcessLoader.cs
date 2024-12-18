using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ns;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Path = System.IO.Path;

namespace Ryujinx.HLE.Loaders.Processes
{
    public class ProcessLoader
    {
        private readonly Switch _device;

        private readonly ConcurrentDictionary<ulong, ProcessResult> _processesByPid;

        private ulong _latestPid;

        public ProcessResult ActiveApplication => _processesByPid[_latestPid];

        public ProcessLoader(Switch device)
        {
            _device = device;
            _processesByPid = new ConcurrentDictionary<ulong, ProcessResult>();
        }

        public bool LoadXci(string path, ulong applicationId)
        {
            Stream stream = _device.HostFileSystem.OpenFileRead(path);
            if (stream == null)
            {
                if (!_device.HostFileSystem.RequestsCancelled)
                    Logger.Error?.Print(LogClass.Loader, "Unable to load XCI: Failed to open file");

                return false;
            }

            Xci xci = new(_device.Configuration.VirtualFileSystem.KeySet, stream.AsStorage());

            if (!xci.HasPartition(XciPartitionType.Secure))
            {
                Logger.Error?.Print(LogClass.Loader, "Unable to load XCI: Could not find XCI Secure partition");

                return false;
            }

            (bool success, ProcessResult processResult) = xci.OpenPartition(XciPartitionType.Secure).TryLoad(_device, path, applicationId, out string errorMessage);

            if (!success)
            {
                Logger.Error?.Print(LogClass.Loader, errorMessage, nameof(PartitionFileSystemExtensions.TryLoad));

                return false;
            }

            if (processResult.ProcessId != 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    _latestPid = processResult.ProcessId;

                    return true;
                }
            }

            return false;
        }

        public bool LoadNsp(string path, ulong applicationId)
        {
            Stream file = _device.HostFileSystem.OpenFileRead(path);
            if (file == null)
            {
                if (!_device.HostFileSystem.RequestsCancelled)
                    Logger.Error?.Print(LogClass.Loader, "Unable to load NSP: Failed to open file");

                return false;
            }

            PartitionFileSystem partitionFileSystem = new();
            partitionFileSystem.Initialize(file.AsStorage()).ThrowIfFailure();

            _device.ApplicationDocumentRegistry.InitializeFromNsp(partitionFileSystem, applicationId);

            (bool success, ProcessResult processResult) = partitionFileSystem.TryLoad(_device, path, applicationId, out string errorMessage);

            if (processResult.ProcessId == 0)
            {
                // This is not a normal NSP, it's actually a ExeFS as a NSP
                processResult = partitionFileSystem.Load(_device, new BlitStruct<ApplicationControlProperty>(1), partitionFileSystem.GetNpdm(), 0, true);
            }

            if (processResult.ProcessId != 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    _latestPid = processResult.ProcessId;

                    return true;
                }
            }

            if (!success)
            {
                Logger.Error?.Print(LogClass.Loader, errorMessage, nameof(PartitionFileSystemExtensions.TryLoad));
            }

            return false;
        }

        public bool LoadNca(string path)
        {
            Stream file = _device.HostFileSystem.OpenFileRead(path);
            if (file == null)
            {
                if (!_device.HostFileSystem.RequestsCancelled)
                    Logger.Error?.Print(LogClass.Loader, "Unable to load NCA: Failed to open file");

                return false;
            }

            Nca nca = new(_device.Configuration.VirtualFileSystem.KeySet, file.AsStorage(false));

            ProcessResult processResult = nca.Load(_device, null, null);

            if (processResult.ProcessId != 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    // NOTE: Check if process is SystemApplicationId or ApplicationId
                    if (processResult.ProgramId > 0x01000000000007FF)
                    {
                        _latestPid = processResult.ProcessId;
                    }

                    return true;
                }
            }

            return false;
        }

        public bool LoadUnpackedNca(string exeFsDirPath, string romFsPath = null)
        {
            ProcessResult processResult = new LocalFileSystem(exeFsDirPath).Load(_device, romFsPath);

            if (processResult.ProcessId != 0 && _processesByPid.TryAdd(processResult.ProcessId, processResult))
            {
                if (processResult.Start(_device))
                {
                    _latestPid = processResult.ProcessId;

                    return true;
                }
            }

            return false;
        }

        public bool LoadNxo(string path, CancellationToken? cancellationToken = null)
        {
            var nacpData = new BlitStruct<ApplicationControlProperty>(1);
            IFileSystem dummyExeFs = null;
            Stream romfsStream = null;

            string programName = "";
            ulong programId = 0000000000000000;

            // Load executable.
            IExecutable executable;

            if (Path.GetExtension(path).ToLower() == ".nro")
            {
                Stream input = _device.HostFileSystem.OpenFileRead(path);
                if (input == null)
                {
                    if (!_device.HostFileSystem.RequestsCancelled)
                        Logger.Error?.Print(LogClass.Loader, "Unable to load NXO: Failed to open file");

                    return false;
                }

                NroExecutable nro = new(input.AsStorage());

                executable = nro;

                // Open RomFS if exists.
                IStorage romFsStorage = nro.OpenNroAssetSection(LibHac.Tools.Ro.NroAssetType.RomFs, false);
                romFsStorage.GetSize(out long romFsSize).ThrowIfFailure();
                if (romFsSize != 0)
                {
                    romfsStream = romFsStorage.AsStream();
                }

                // Load Nacp if exists.
                IStorage nacpStorage = nro.OpenNroAssetSection(LibHac.Tools.Ro.NroAssetType.Nacp, false);
                nacpStorage.GetSize(out long nacpSize).ThrowIfFailure();
                if (nacpSize != 0)
                {
                    nacpStorage.Read(0, nacpData.ByteSpan);

                    programName = nacpData.Value.Title[(int)_device.System.State.DesiredTitleLanguage].NameString.ToString();

                    if (string.IsNullOrWhiteSpace(programName))
                    {
                        programName = Array.Find(nacpData.Value.Title.ItemsRo.ToArray(), x => x.Name[0] != 0).NameString.ToString();
                    }

                    if (nacpData.Value.PresenceGroupId != 0)
                    {
                        programId = nacpData.Value.PresenceGroupId;
                    }
                    else if (nacpData.Value.SaveDataOwnerId != 0)
                    {
                        programId = nacpData.Value.SaveDataOwnerId;
                    }
                    else if (nacpData.Value.AddOnContentBaseId != 0)
                    {
                        programId = nacpData.Value.AddOnContentBaseId - 0x1000;
                    }
                }

                // TODO: Add icon maybe ?
            }
            else
            {
                programName = Path.GetFileNameWithoutExtension(path);

                executable = new NsoExecutable(new LocalStorage(path, FileAccess.Read), programName);
            }

            // Explicitly null TitleId to disable the shader cache.
            Graphics.Gpu.GraphicsConfig.TitleId = null;
            _device.Gpu.HostInitalized.Set();

            ProcessResult processResult = ProcessLoaderHelper.LoadNsos(_device,
                                                                       _device.System.KernelContext,
                                                                       dummyExeFs.GetNpdm(),
                                                                       nacpData,
                                                                       diskCacheEnabled: false,
                                                                       allowCodeMemoryForJit: true,
                                                                       programName,
                                                                       programId,
                                                                       0,
                                                                       null,
                                                                       executable);

            // Make sure the process id is valid.
            if (processResult.ProcessId != 0)
            {
                // Load RomFS.
                if (romfsStream != null)
                {
                    _device.Configuration.VirtualFileSystem.SetRomFs(processResult.ProcessId, romfsStream);
                }

                // Start process.
                if (_processesByPid.TryAdd(processResult.ProcessId, processResult))
                {
                    if (processResult.Start(_device))
                    {
                        _latestPid = processResult.ProcessId;

                        return true;
                    }
                }
            }

            return false;
        }
    }
}
