using ARMeilleure.Translation;

namespace Ryujinx.Cpu
{
    public sealed class CpuContextConfiguration
    {

        /// <summary>
        /// Configuration to be passed to the ARMeilleure instruction translator
        /// </summary>
        public TranslatorConfiguration TranslatorConfiguration { get; set; }

        /// <summary>
        /// Use sparse function address tables if available
        /// </summary>
        public bool UseSparseAddressTable { get; set; }

        public CpuContextConfiguration(TranslatorConfiguration translatorConfiguration,
                                       bool useSparseAddressTable)
        {
            TranslatorConfiguration = translatorConfiguration;
            UseSparseAddressTable = useSparseAddressTable;
        }

        public static CpuContextConfiguration Default()
        {
            return new(TranslatorConfiguration.Default(), true);
        }

    }
}
