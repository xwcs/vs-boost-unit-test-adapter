// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace BoostTestAdapter.Discoverers
{
    /// <summary>
    /// Verifies sources passed to BoostTestDiscoverer.
    /// </summary>
    public interface IDiscoveryVerifier
    {
        /// <summary>
        /// Determines whether the specified file exists.
        /// </summary>
        /// <param name="path">The file to check</param>
        bool FileExists(string path);

        /// <summary>
        /// Determines whether the specified file security zone is "my computer".
        /// </summary>
        /// <param name="path">The file to check</param>
        bool IsFileZoneMyComputer(string path);
    }
}
