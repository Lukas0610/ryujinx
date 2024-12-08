using LibHac;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Ryujinx.IO.Host;
using Ryujinx.HLE.FileSystem;
using System.IO;

namespace Ryujinx.HLE.Utilities
{
    public static class PartitionFileSystemUtils
    {
        public static IFileSystem OpenApplicationFileSystem(string path, VirtualFileSystem fileSystem, HostFileSystem hostFileSystem, bool throwOnFailure = true)
        {
            Stream file = hostFileSystem.OpenFileRead(path);
            if (file == null)
            {
                return null;
            }

            IFileSystem partitionFileSystem;

            if (Path.GetExtension(path).ToLower() == ".xci")
            {
                partitionFileSystem = new Xci(fileSystem.KeySet, file.AsStorage()).OpenPartition(XciPartitionType.Secure);
            }
            else
            {
                var pfsTemp = new PartitionFileSystem();
                Result initResult = pfsTemp.Initialize(file.AsStorage());

                if (throwOnFailure)
                {
                    initResult.ThrowIfFailure();
                }
                else if (initResult.IsFailure())
                {
                    return null;
                }

                partitionFileSystem = pfsTemp;
            }

            fileSystem.ImportTickets(partitionFileSystem);

            return partitionFileSystem;
        }
    }
}
