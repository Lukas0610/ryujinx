using Ryujinx.Common;
using Ryujinx.HLE.Loaders.Executables;
using System.Collections.Generic;

namespace Ryujinx.HLE.Utilities
{
    internal static class ExecutableUtils
    {

        public static Hash128 CreateCombinedBuildIdHash(IEnumerable<IExecutable> executables)
        {
            var buildIdBytes = new List<byte>();

            foreach (IExecutable executable in executables)
            {
                if (executable is NsoExecutable nso)
                {
                    buildIdBytes.AddRange(nso.BuildId);
                }
                else if (executable is NroExecutable nro)
                {
                    buildIdBytes.AddRange(nro.Header.BuildId);
                }
            }

            return XXHash128.ComputeHash(buildIdBytes.ToArray());
        }

    }
}
