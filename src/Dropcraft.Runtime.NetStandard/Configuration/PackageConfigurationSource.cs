﻿using Dropcraft.Common;
using Dropcraft.Common.Configuration;

namespace Dropcraft.Runtime.Configuration
{
    /// <summary>
    /// Configuration source for default JSON-based package configuration files
    /// </summary>
    public class PackageConfigurationSource : IPackageConfigurationSource
    {
        /// <summary>
        /// ManifestNameTemplate defines package manifest file name.
        /// Manifest can be changed from the default 'manifest.json' by providing a template.
        /// Example template: '{0}-manifest.json', where {0} will be replaced with the package ID
        /// </summary>
        public string ManifestNameTemplate { get; set; } = "manifest.json";

        public IPackageConfigurationProvider GetPackageConfigurationProvider()
        {
            return new PackageConfigurationProvider(ManifestNameTemplate);
        }
    }
}
