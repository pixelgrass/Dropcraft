namespace Dropcraft.Common.Configuration
{
    /// <summary>
    /// Provides configurations for the packages
    /// </summary>
    public interface IPackageConfigurationProvider
    {
        IPackageConfiguration GetPackageConfiguration(PackageId packageInfo, string packagePath);
    }
}