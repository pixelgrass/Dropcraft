﻿using System.Collections.Generic;
using Dropcraft.Common;
using Dropcraft.Common.Configuration;
using Dropcraft.Deployment.Configuration;
using Dropcraft.Runtime.Configuration;

namespace Dropcraft.Deployment
{
    /// <summary>
    /// Configuration object for creating <see cref="IDeploymentEngine"/> instances.
    /// </summary>
    public class DeploymentConfiguration
    {
        /// <summary>
        /// Path to install/uninstall packages 
        /// </summary>
        internal string PackagesFolderPath { get; set; }

        /// <summary>
        /// Instructs to always try to update packages from the remote sources
        /// </summary>
        internal bool UpdatePackages { get; set; }

        /// <summary>
        /// Instructs to allow packages downgrades
        /// </summary>
        internal bool AllowDowngrades { get; set; }

        /// <summary>
        /// Package configuration source
        /// </summary>
        internal IPackageConfigurationSource PackageConfigurationSource { get; set; }

        /// <summary>
        /// Product configuration source
        /// </summary>
        internal IProductConfigurationSource ProductConfigurationSource { get; set; }

        /// <summary>
        /// Deployment filters
        /// </summary>
        internal IDeploymentStrategySource DeploymentStrategySource { get; set; }

        /// <summary>
        /// List of remote package sources
        /// </summary>
        internal List<string> RemotePackagesSources { get; } = new List<string>();

        /// <summary>
        /// Constructor
        /// </summary>
        public DeploymentConfiguration()
        {
            ProductConfigurationSource = new ProductConfigurationSource();
            PackageConfigurationSource = new PackageConfigurationSource();
            DeploymentStrategySource = new DeploymentStrategySource();
        }

        /// <summary>
        /// Allows to setup additional options
        /// </summary>
        public DeploymentConfigurationOptions ConfigureTo => new DeploymentConfigurationOptions(this);

        /// <summary>
        /// Allows to setup the configuration sources for packages
        /// </summary>
        public PackagesConfigurationOptions ForPackagesConfiguration => new PackagesConfigurationOptions(this);

        /// <summary>
        /// Allows to setup the configuration sources for product
        /// </summary>
        public ProductConfigurationOptions ForProductConfiguration => new ProductConfigurationOptions(this);

        /// <summary>
        /// Allows to setup the deployment strategy for product
        /// </summary>
        public DeploymentStrategyOptions ForDeployment => new DeploymentStrategyOptions(this);

        /// <summary>
        /// Allows to setup local and remote package sources
        /// </summary>
        public PackageSourceOptions ForPackages => new PackageSourceOptions(this);

        /// <summary>
        /// Creates <see cref="IDeploymentEngine"/> instances.
        /// </summary>
        /// <param name="deploymentContext">Custom deployment context</param>
        /// <returns><see cref="IDeploymentEngine"/></returns>
        public IDeploymentEngine CreatEngine(DeploymentContext deploymentContext)
        {
            var deploymentStartegyProvider = DeploymentStrategySource.GetStartegyProvider(deploymentContext);
            return new DeploymentEngine(deploymentContext, deploymentStartegyProvider, PackagesFolderPath,
                RemotePackagesSources, UpdatePackages, AllowDowngrades);
        }

        public IDeploymentEngine CreatEngine(string productPath, string framework)
        {
            var packageConfigurationProvider = PackageConfigurationSource.GetPackageConfigurationProvider();
            var productConfigurationProvider = ProductConfigurationSource.GetProductConfigurationProvider(productPath);
            var deploymentContext = new DefaultDeploymentContext(productPath, framework, packageConfigurationProvider,
                productConfigurationProvider);

            return CreatEngine(deploymentContext);
        }
    }
}
