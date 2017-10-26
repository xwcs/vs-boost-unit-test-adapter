// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Globalization;
using System.ServiceModel;

namespace BoostTestShared
{
    /// <summary>
    /// Helper class for configuring BoostTestPackageService
    /// </summary>
    public sealed class BoostTestPackageServiceConfiguration
    {
        /// <summary>
        /// Relative address of the endpoint interface.
        /// </summary>
        public static readonly Uri InterfaceAddress = new Uri(nameof(IBoostTestPackageService), UriKind.Relative);

        /// <summary>
        /// Constructs the Uri at which the service is made available.
        /// </summary>
        /// <param name="processId">Identifier of the process offering the service</param>
        /// <returns></returns>
        public static Uri ConstructPipeUri(int processId)
        {
            return new Uri(string.Format(CultureInfo.InvariantCulture,
                "net.pipe://localhost/BoostTestPackageService.6e0d1c7a-ef6a-4442-b8ce-9a58b21c3239.{0}/",
                processId));
        }

        /// <summary>
        /// Creates proxy object for the service.
        /// </summary>
        /// <remarks>
        /// The object must be handled as a <ref>System.ServiceModel.IClientChannel</ref>.
        /// </remarks>
        /// <param name="processId">Identifier of the process offering the service</param>
        /// <returns></returns>
        public static IBoostTestPackageService CreateProxy(int processId)
        {
            var endpointUri = new Uri(ConstructPipeUri(processId), InterfaceAddress);
            var endpointAddress = new EndpointAddress(endpointUri);
            return ChannelFactory<IBoostTestPackageService>.CreateChannel(new NetNamedPipeBinding(), endpointAddress);
        }

        /// <summary>
        /// Private default constructor to prevent from creating instances of this class.
        /// </summary>
        private BoostTestPackageServiceConfiguration()
        {
        }
    }
}
