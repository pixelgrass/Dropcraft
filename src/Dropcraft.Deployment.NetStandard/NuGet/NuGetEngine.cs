﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dropcraft.Common;
using Dropcraft.Common.Logging;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Versioning;

namespace Dropcraft.Deployment.NuGet
{
    public class NuGetEngine
    {
        private readonly NuGetLogger _nuGetLogger;
        private readonly bool _updatePackages;
        private readonly bool _allowDowngrades;
        private readonly SourceRepositoryProvider _repositoryProvider;
        private readonly SourceRepository _localRepository;
        private readonly NuGetFramework _framework;
        private static readonly ILog Logger = LogProvider.For<NuGetEngine>();
        private readonly SourceCacheContext _cache;

        public NuGetEngine(DeploymentContext deploymentContext, string packagesFolderPath,
            List<string> remotePackagesSources, bool updatePackages, bool allowDowngrades)
        {
            _updatePackages = updatePackages;
            _allowDowngrades = allowDowngrades;

            var settings = Settings.LoadDefaultSettings(packagesFolderPath);
            _nuGetLogger = new NuGetLogger();
            _cache = new SourceCacheContext();

            _repositoryProvider = new SourceRepositoryProvider(settings);
            _localRepository = _repositoryProvider.CreateRepository(packagesFolderPath);

            foreach (var packageSource in remotePackagesSources)
            {
                _repositoryProvider.AddPackageRepository(packageSource);
            }

            if (!string.IsNullOrWhiteSpace(deploymentContext.TargetFramework))
            {
                _framework = NuGetFramework.Parse(deploymentContext.TargetFramework);
                if (_framework == null)
                {
                    var ex = new ArgumentException($"Framework {deploymentContext.TargetFramework} is unknown");
                    Logger.Trace(ex.Message);
                    throw ex;
                }
            }
            else
            {
                _framework = GetCurrentFramework();
            }
        }

        public async Task<string> ResolveNuGetVersion(PackageId packageId)
        {
            NuGetVersion resolvedVersion = null;

            if (!_updatePackages)
            {
                resolvedVersion = await GetLatestMatchingVersion(packageId, _localRepository, _nuGetLogger);
            }

            if (resolvedVersion == null && _repositoryProvider.Repositories.Count > 0)
            {
                resolvedVersion = await GetLatestMatchingVersion(packageId, _repositoryProvider.Repositories, _nuGetLogger);
            }

            return resolvedVersion == null ? null : resolvedVersion.ToFullString();
        }

        public async Task<GraphNode<RemoteResolveResult>> ResolvePackages(List<PackageId> packages)
        {
            var walkerContext = new RemoteWalkContext(_cache, _nuGetLogger);

            foreach (var sourceRepository in _repositoryProvider.Repositories)
            {
                var provider = new SourceRepositoryDependencyProvider(sourceRepository, _nuGetLogger, _cache, true, true);
                walkerContext.RemoteLibraryProviders.Add(provider);
            }

            walkerContext.ProjectLibraryProviders.Add(new ProjectLibraryProvider(packages));
            var localProvider = new SourceRepositoryDependencyProvider(_localRepository, _nuGetLogger, _cache, true, true);
            walkerContext.LocalLibraryProviders.Add(localProvider);

            var fakeLib = new LibraryRange("Dropcraft", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Project);
            var walker = new RemoteDependencyWalker(walkerContext);

            return await walker.WalkAsync(fakeLib, _framework, _framework.GetShortFolderName(), RuntimeGraph.Empty, true);
        }

        public void AnalysePackages(GraphNode<RemoteResolveResult> resolvedPackages)
        {
            var packagesAnalysis = resolvedPackages.Analyze();
            if (packagesAnalysis.Downgrades.Any())
            {
                foreach (var downgrade in packagesAnalysis.Downgrades)
                {
                    Logger.Warn($"Downgrade from {downgrade.DowngradedFrom.Key} to "
                        + $"{downgrade.DowngradedTo.Key}");
                }

                if (!_allowDowngrades)
                {
                    var name = packagesAnalysis.Downgrades.First().DowngradedFrom.Item.Key.Name;
                    throw new InvalidOperationException($"At least one package requires downgrade and downgrade is not allowed: {name}");
                }
            }

            if (packagesAnalysis.Cycles.Any())
            {
                foreach (var cycle in packagesAnalysis.Cycles)
                {
                    Logger.Trace($"Cycle dependency for {cycle.Item.Key.Name},{cycle.Item.Key.Version.ToNormalizedString()}");
                }

                var name = packagesAnalysis.Cycles.First().Key.Name;
                throw new InvalidOperationException($"At least one package has cycle dependency: {name}");
            }

            if (packagesAnalysis.VersionConflicts.Any())
            {
                foreach (var conflict in packagesAnalysis.VersionConflicts)
                {
                    Logger.Trace($"Conflict for {conflict.Conflicting.Key.Name},{conflict.Conflicting.Key.VersionRange.ToNormalizedString()} resolved as "
                        + $"{conflict.Selected.Item.Key.Name},{conflict.Selected.Item.Key.Version.ToNormalizedString()}");
                }
            }
        }

        private async Task<NuGetVersion> GetLatestMatchingVersion(PackageId packageInfo, IEnumerable<SourceRepository> sourceRepositories, ILogger logger)
        {
            NuGetVersion[] versionMatches = await Task.WhenAll(sourceRepositories.Select(x => GetLatestMatchingVersion(packageInfo, x, logger)));
            return versionMatches
                .Where(x => x != null)
                .DefaultIfEmpty()
                .Max();
        }

        private async Task<NuGetVersion> GetLatestMatchingVersion(PackageId packageInfo, SourceRepository sourceRepository, ILogger logger)
        {
            try
            {
                var versionRange = string.IsNullOrWhiteSpace(packageInfo.Version) ? null : VersionRange.Parse(packageInfo.Version);
                var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                var dependencyInfo = await dependencyInfoResource.ResolvePackages(
                    packageInfo.Id, _framework, logger, CancellationToken.None);
                return dependencyInfo
                    .Select(x => x.Version)
                    .Where(x => x != null && (versionRange == null || versionRange.Satisfies(x)) && (packageInfo.AllowPrereleaseVersions || !x.IsPrerelease))
                    .DefaultIfEmpty()
                    .Max();
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not get latest version for package {packageInfo.Id}{packageInfo.Version} from source {sourceRepository}: {ex.Message}");
                return null;
            }
        }

        private NuGetFramework GetCurrentFramework()
        {
            string frameworkName = GetType().GetTypeInfo().Assembly.GetCustomAttributes()
                .OfType<System.Runtime.Versioning.TargetFrameworkAttribute>()
                .Select(x => x.FrameworkName)
                .FirstOrDefault();
            return frameworkName == null
                ? NuGetFramework.AnyFramework
                : NuGetFramework.ParseFrameworkName(frameworkName, new DefaultFrameworkNameProvider());
        }

        public Task InstallPackage(RemoteMatch match, string path)
        {
            var packageIdentity = new PackageIdentity(match.Library.Name, match.Library.Version);

            var versionFolderPathContext = new VersionFolderPathContext(
                packageIdentity,
                path,
                _nuGetLogger,
                PackageSaveMode.Defaultv3,
                XmlDocFileSaveMode.None);

            return PackageExtractor.InstallFromSourceAsync(
                stream => match.Provider.CopyToAsync(
                    match.Library,
                    stream,
                    _cache, 
                    _nuGetLogger,
                    CancellationToken.None),
                versionFolderPathContext,
                CancellationToken.None);
        }

        public string GetPackageTargetPath(string packageId, NuGetVersion version, string path)
        {
            var pathResolver = new VersionFolderPathResolver(path);
            return pathResolver.GetInstallPath(packageId, version);
        }

        public static string GetMostCompatibleFramework(string targetFramework, IEnumerable<string> candidateFrameworks)
        {
            var frameworkReducer = new FrameworkReducer();
            var nearestFramework = frameworkReducer.GetNearest(NuGetFramework.Parse(targetFramework),
                candidateFrameworks.Select(NuGetFramework.Parse));

            return nearestFramework?.GetShortFolderName();
        }

    }
}