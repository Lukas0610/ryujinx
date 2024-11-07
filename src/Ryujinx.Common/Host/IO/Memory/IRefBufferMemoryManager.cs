namespace Ryujinx.Common.Host.IO.Memory
{

    public interface IRefBufferMemoryManager : IBufferMemoryManager
    {

        /// <summary>
        /// Add a page to the reference-index or instruct the implementation of
        /// the memory-manager to update the state already added page
        /// </summary>
        /// <param name="page">The page to add or update the state of</param>
        void RefPage(BufferedFilePage page);

        /// <summary>
        /// Remove a page from the reference-index
        /// </summary>
        /// <param name="page">The page to remove</param>
        /// <returns><see langword="true"/> if a page was removed, <see langword="false"/> otherwise</returns>
        bool DerefPage(BufferedFilePage page);

        /// <summary>
        /// Evicts pages from the memory according to the constraints defined
        /// by the implementation of the memory-manager and/or its owner
        /// </summary>
        /// <returns>Number of pages evicted</returns>
        int EnsureConstraints();

        /// <summary>
        /// Determines whether according to the constraints defined
        /// by the implementation of the memory-manager and/or its owner,
        /// an eviction of existing pages should take place
        /// </summary>
        /// <returns><see langword="true"/> if pages should be evicted if new pages are to be added, <see langword="false" /> otherwise</returns>
        bool ShouldEvict();

    }

}
