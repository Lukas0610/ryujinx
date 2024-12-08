using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Ryujinx.IO.Host;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.Loaders.Processes.Extensions;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.HLE.FileSystem
{

    public sealed class ApplicationDocumentRegistry
    {

        private readonly Switch _device;

        private readonly VirtualFileSystem _virtualFileSystem;
        private readonly HostFileSystem _hostFileSystem;
        private readonly IntegrityCheckLevel _checkLevel;

        private readonly Dictionary<string, byte[]> _htmlDocumentFiles;
        private readonly Dictionary<string, byte[]> _legalInformationFiles;

        internal ApplicationDocumentRegistry(Switch device)
        {
            _device = device;

            _virtualFileSystem = device.FileSystem;
            _hostFileSystem = device.HostFileSystem;
            _checkLevel = device.Configuration.FsIntegrityCheckLevel;

            _htmlDocumentFiles = new Dictionary<string, byte[]>();
            _legalInformationFiles = new Dictionary<string, byte[]>();
        }

        public bool TryGetHtmlDocumentFileData(string path, out byte[] data)
            => _htmlDocumentFiles.TryGetValue(path, out data);

        public bool TryGetLegalInformationFileData(string path, out byte[] data)
            => _legalInformationFiles.TryGetValue(path, out data);

        internal void Reset()
        {
            _htmlDocumentFiles.Clear();
            _legalInformationFiles.Clear();
        }

        internal void InitializeFromNsp(PartitionFileSystem pfs, ulong applicationId)
        {
            ContentMetaData content = pfs
                .GetContentData(ContentMetaType.Application, _virtualFileSystem, _checkLevel)
                .FirstOrDefault(x => x.Key == applicationId)
                .Value;

            if (content != null)
            {
                InitializeFromNca(content, ContentType.HtmlDocument, _htmlDocumentFiles);
                InitializeFromNca(content, ContentType.LegalInformation, _legalInformationFiles);
            }
        }

        private void InitializeFromNca(ContentMetaData content, ContentType type, Dictionary<string, byte[]> files)
        {
            Nca nca = content.GetNcaByType(_virtualFileSystem.KeySet, type);

            if (nca == null)
            {
                return;
            }

            IFileSystem ncaFs = nca.OpenFileSystem(NcaSectionType.Data, _checkLevel);
            string updatePath = null;

            try
            {
                ContentMetaData updateContent = nca.GetUpdateContent(_virtualFileSystem, _hostFileSystem, _checkLevel, 0, out updatePath);

                if (updateContent != null)
                {
                    Nca patchNca = updateContent.GetNcaByType(_virtualFileSystem.KeySet, type, 0);

                    if (patchNca.CanOpenSection(NcaSectionType.Data))
                    {
                        ncaFs = nca.OpenFileSystemWithPatch(patchNca, NcaSectionType.Data, _checkLevel);
                    }
                }
            }
            catch (InvalidDataException)
            {
                Logger.Warning?.Print(LogClass.Application, $"The header key is incorrect or missing and therefore the NCA header content type check has failed. Errored File: {updatePath}");
            }
            catch (MissingKeyException exception)
            {
                Logger.Warning?.Print(LogClass.Application, $"Your key set is missing a key with the name: {exception.Name}. Errored File: {updatePath}");
            }

            foreach (DirectoryEntryEx entry in ncaFs.EnumerateEntries("*.*", SearchOptions.RecurseSubdirectories))
            {
                if (entry.Type == LibHac.Fs.DirectoryEntryType.File)
                {
                    using UniqueRef<IFile> fileRef = new();
                    using MemoryStream stream = new();

                    ncaFs.OpenFile(ref fileRef.Ref, entry.FullPath.ToU8Span(), LibHac.Fs.OpenMode.Read).ThrowIfFailure();
                    fileRef.Get.AsStream().CopyTo(stream);

                    files.Add(entry.FullPath, stream.ToArray());
                }
            }
        }

    }

}
