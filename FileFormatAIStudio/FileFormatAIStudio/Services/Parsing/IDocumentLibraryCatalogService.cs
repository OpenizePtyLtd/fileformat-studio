using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileFormatAIStudio.Models;

namespace FileFormatAIStudio.Services.Parsing
{
    /// <summary>
    /// Service for querying and maintaining the catalog of all integrated document extraction libraries,
    /// their metadata, supported file types, licensing models, and package popularity statistics.
    /// </summary>
    public interface IDocumentLibraryCatalogService
    {
        /// <summary>
        /// Raised when library catalog metadata or statistics are updated.
        /// </summary>
        event EventHandler? CatalogUpdated;

        /// <summary>
        /// Gets all integrated document extraction libraries.
        /// </summary>
        IReadOnlyList<DocumentLibraryInfo> GetAllLibraries();

        /// <summary>
        /// Fetches live download counts, latest version, and publish dates from NuGet and npm APIs
        /// and updates the library catalog.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Updated list of libraries.</returns>
        Task<IReadOnlyList<DocumentLibraryInfo>> RefreshLiveStatsAsync(CancellationToken cancellationToken = default);
    }
}

