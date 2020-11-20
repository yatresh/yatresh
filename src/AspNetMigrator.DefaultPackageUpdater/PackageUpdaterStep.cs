﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Extensions.Logging;

namespace AspNetMigrator.Engine
{
    public class PackageUpdaterStep : MigrationStep
    {
        private const string DefaultPackageConfigFileName = "PackageMap.json";
        private const string PackageReferenceType = "PackageReference";
        private const string AnalyzerPackageName = "AspNetMigrator.Analyzers";
        private const string AnalyzerPackageVersion = "1.0.0";
        private const string VersionElementName = "Version";

        public string PackageMapPath { get; }

        private IEnumerable<NuGetPackageMap> _packageMaps = Enumerable.Empty<NuGetPackageMap>();

        public PackageUpdaterStep(MigrateOptions options, PackageUpdaterOptions updaterOptions, ILogger<PackageUpdaterStep> logger)
            : base(options, logger)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var mapPath = updaterOptions?.PackageMapPath ?? DefaultPackageConfigFileName;

            PackageMapPath = Path.IsPathFullyQualified(mapPath) ?
                mapPath :
                Path.Combine(Path.GetDirectoryName(typeof(PackageUpdaterStep).Assembly.Location)!, mapPath);

            Title = $"Update NuGet packages";
            Description = $"Update package references in {options.ProjectPath} to work with .NET based on mappings in {PackageMapPath}";
        }

        // TODO : This does not currently update package dependencies, so it's easy to get into a state where package versions conflict.
        //        This should be updated to more robustly handle dependencies, either by including dependency information in the config (which
        //        would require a fair bit of work) or by determining dependencies at runtime.
        protected override async Task<(MigrationStepStatus Status, string StatusDetails)> ApplyImplAsync(IMigrationContext context, CancellationToken token)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!File.Exists(PackageMapPath))
            {
                throw new FileNotFoundException("Package map file not found", PackageMapPath);
            }

            var projectPath = await context.GetProjectPathAsync(token).ConfigureAwait(false);

            try
            {
                var project = ProjectRootElement.Open(projectPath);
                project.Reload(false); // Reload to make sure we're not seeing an old cached version of the project

                var referencesToAdd = new List<NuGetReference>();

                // Check for NuGet packages that need replaced and remove them
                foreach (var reference in project.Items.Where(i => i.ItemType.Equals(PackageReferenceType, StringComparison.OrdinalIgnoreCase)))
                {
                    var packageName = reference.Include;
                    var packageVersion = (reference.Children.FirstOrDefault(c => c.ElementName.Equals(VersionElementName, StringComparison.OrdinalIgnoreCase)) as ProjectMetadataElement)?.Value;
                    var map = _packageMaps.FirstOrDefault(m => m.ContainsReference(packageName, packageVersion));

                    if (map != null)
                    {
                        Logger.LogInformation("Removing outdated packaged reference (based on package map {PackageMapName}): {PackageReference}", map.PackageSetName, new NuGetReference(packageName, packageVersion));

                        // The reference should be replaced
                        var itemGroup = reference.Parent;
                        itemGroup.RemoveChild(reference);

                        if (!itemGroup.Children.Any())
                        {
                            // If no element remain in the item group, remove it
                            Logger.LogDebug("Removing empty ItemGroup");
                            itemGroup.Parent.RemoveChild(itemGroup);
                        }

                        // Include the updated versions of removed packages in the list of packages to add references to
                        referencesToAdd.AddRange(map.NetCorePackages);
                    }
                }

                // Find a place to add new package references
                var packageReferenceItemGroup = project.ItemGroups.FirstOrDefault(g => g.Items.Any(i => i.ItemType.Equals(PackageReferenceType, StringComparison.OrdinalIgnoreCase)));
                if (packageReferenceItemGroup is null)
                {
                    Logger.LogDebug("Creating a new ItemGroup for package references");
                    packageReferenceItemGroup = project.CreateItemGroupElement();
                    project.AppendChild(packageReferenceItemGroup);
                }
                else
                {
                    Logger.LogDebug("Found ItemGroup for package references");
                }

                // Add replacement packages
                foreach (var newReference in referencesToAdd.Distinct())
                {
                    Logger.LogInformation("Adding package reference to: {PackageReference}", newReference);
                    var newItemElement = project.CreateItemElement(PackageReferenceType, newReference.Name);
                    packageReferenceItemGroup.AppendChild(newItemElement);
                    newItemElement.AddMetadata(VersionElementName, newReference.Version, true);
                }

                // Add reference to ASP.NET Core migration analyzers if needed
                if (!project.Items.Any(i => i.ItemType.Equals(PackageReferenceType, StringComparison.OrdinalIgnoreCase) && AnalyzerPackageName.Equals(i.Include, StringComparison.OrdinalIgnoreCase)))
                {
                    Logger.LogInformation("Adding package reference to: {PackageReference}", AnalyzerPackageName);
                    var analyzerReferenceElement = project.CreateItemElement(PackageReferenceType, AnalyzerPackageName);
                    packageReferenceItemGroup.AppendChild(analyzerReferenceElement);
                    analyzerReferenceElement.AddMetadata(VersionElementName, AnalyzerPackageVersion, true);
                }
                else
                {
                    Logger.LogDebug("Analyzer reference already present");
                }

                project.Save();

                return (MigrationStepStatus.Complete, "Packages updated");
            }
            catch (InvalidProjectFileException)
            {
                Logger.LogCritical("Invalid project: {ProjectPath}", projectPath);
                return (MigrationStepStatus.Failed, $"Invalid project: {projectPath}");
            }
        }

        protected override async Task<(MigrationStepStatus Status, string StatusDetails)> InitializeImplAsync(IMigrationContext context, CancellationToken token)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (!File.Exists(PackageMapPath))
            {
                Logger.LogCritical("Package map file {PackageMapPath} not found", PackageMapPath);
                return (MigrationStepStatus.Failed, $"Package map file {PackageMapPath} not found");
            }

            // Initialize package maps from config file
            Logger.LogInformation("Loading package maps from {PackageMapPath}", PackageMapPath);
            using (var config = File.OpenRead(PackageMapPath))
            {
                var packageMaps = await JsonSerializer.DeserializeAsync<IEnumerable<NuGetPackageMap>>(config, cancellationToken: token).ConfigureAwait(false);
                _packageMaps = packageMaps!;
            }

            Logger.LogDebug("Loaded {MapCount} package maps", _packageMaps.Count());

            var projectPath = await context.GetProjectPathAsync(token).ConfigureAwait(false);

            if (!File.Exists(projectPath))
            {
                Logger.LogCritical("Project file {ProjectPath} not found", projectPath);
                return (MigrationStepStatus.Failed, $"Project file {projectPath} not found");
            }

            try
            {
                var project = ProjectRootElement.Open(projectPath);
                project.Reload(false); // Reload to make sure we're not seeing an old cached version of the project

                // Query for the project's package references
                var packageReferences = project.Items
                    .Where(i => i.ItemType.Equals(PackageReferenceType, StringComparison.OrdinalIgnoreCase)) // All <PackageReferenceElements>
                    .Select(p => (Name: p.Include, Version: (p.Children.FirstOrDefault(c => c.ElementName.Equals(VersionElementName, StringComparison.OrdinalIgnoreCase)) as ProjectMetadataElement)?.Value)); // Select name/version

                // Identify any references that need updated
                var outdatedPackages = packageReferences.Where(p => _packageMaps.Any(m => m.ContainsReference(p.Name, p.Version)));

                if (outdatedPackages.Any())
                {
                    Logger.LogInformation("Found {PackageCount} outdated package references", outdatedPackages.Count());
                    return (MigrationStepStatus.Incomplete, $"{outdatedPackages.Count()} packages need updated");
                }

                // Check that the analyzer package reference is present
                if (!packageReferences.Any(p => p.Name.Equals(AnalyzerPackageName)))
                {
                    Logger.LogInformation("Reference to package {AnalyzerPackageName} needs added", AnalyzerPackageName);
                    return (MigrationStepStatus.Incomplete, $"Reference to package {AnalyzerPackageName} needed");
                }

                Logger.LogInformation("No package updates needed");
                return (MigrationStepStatus.Complete, "No package updates needed");
            }
            catch (InvalidProjectFileException)
            {
                Logger.LogCritical("Invalid project: {ProjectPath}", projectPath);
                return (MigrationStepStatus.Failed, $"Invalid project: {projectPath}");
            }
        }
    }
}
