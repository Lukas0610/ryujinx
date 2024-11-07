using Ryujinx.Common.Utilities;
using System;

namespace ARMeilleure.Translation
{
    public sealed class TranslatorConfiguration
    {

        /// <summary>
        /// List of logical CPU cores the PTC background translation threads are allowed to run on
        /// </summary>
        public CPUSet BackgroundTranslationThreadsCPUSet { get; set; }

        /// <summary>
        /// Number of PTC background translation threads to start
        /// </summary>
        public int BackgroundTranslationThreadCount { get; set; }

        public TranslatorConfiguration(CPUSet backgroundTranslationThreadsCPUSet,
                                       int backgroundTranslationThreadCount)
        {
            BackgroundTranslationThreadsCPUSet = backgroundTranslationThreadsCPUSet;
            BackgroundTranslationThreadCount = backgroundTranslationThreadCount;
        }

        public static TranslatorConfiguration Default()
        {
            int backgroundTranslationThreadCount = Math.Min(4, Math.Max(1, (Environment.ProcessorCount - 6) / 3));

            return new(CPUSet.Default(),
                       backgroundTranslationThreadCount);
        }

    }
}
