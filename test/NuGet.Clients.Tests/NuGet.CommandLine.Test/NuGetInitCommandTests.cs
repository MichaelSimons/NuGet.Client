﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Test.Utility;
using Test.Utility;
using Xunit;

namespace NuGet.CommandLine.Test
{
    public class NuGetInitCommandTests
    {
        /// TEST CASES
        /// 1. No Destination Feed, or No arguments or '-?' or extra args. SUCCESS with help or usage message.
        /// 2. Destination Feed is provided. SUCCESS.
        /// 3. Destination Feed is provided and is relative. SUCCESS.
        /// 4. Destination Feed does not exist. SUCCESS with messages.
        /// 5. Destination Feed already contains packages. SUCCESS with messages.
        /// 6. Destination Feed already contains packages but are invalid. SUCCESS with messages.
        /// 7. Source Feed is relative. SUCCESS.
        /// 8. Source Feed contains no packages. SUCCESS with messages.
        /// 9. Source Feed contains invalid packages. SUCCESS with warnings.
        /// 10. Source Feed does not exist. FAIL
        /// 11. Source Feed is a http source. FAIL
        /// 12. Destination Feed is a http source. FAIL
        /// 13. Source Feed is an invalid input. FAIL
        /// 14. Destination Feed is an invalid input. FAIL
        /// 15. Source Feed is v2-based folder (packages are at the package id directory under the root). SUCCESS
        /// 16. Source Feed is v3-based folder (packages are at the package version directory under the root). SUCCESS
        /// 17. For -Expand switch, Packages are expanded at the destination feed. SUCCESS

        private class TestInfo : IDisposable
        {
            public string NuGetExePath { get; }
            public string WorkingPath { get; }
            public string SourceFeed { get; }
            public string DestinationFeed { get; }

            public TestInfo(string sourceFeed = null, string destinationFeed = null)
            {
                NuGetExePath = Util.GetNuGetExePath();
                WorkingPath = TestFileSystemUtility.CreateRandomTestFolder();

                if (sourceFeed == null)
                {
                    SourceFeed = TestFileSystemUtility.CreateRandomTestFolder();
                }
                else
                {
                    SourceFeed = sourceFeed;
                }

                if (destinationFeed == null)
                {
                    DestinationFeed = TestFileSystemUtility.CreateRandomTestFolder();
                }
                else
                {
                    DestinationFeed = destinationFeed;
                }
            }

            public static readonly List<PackageIdentity> PackagesSet0 = new List<PackageIdentity>()
                {
                    new PackageIdentity("A", new NuGetVersion("1.0.0")),
                    new PackageIdentity("A", new NuGetVersion("2.0.0")),
                    new PackageIdentity("B", new NuGetVersion("1.0.0-BETA")),
                };

            public static readonly List<PackageIdentity> PackagesSet1 = new List<PackageIdentity>()
                {
                    new PackageIdentity("C", new NuGetVersion("1.0.0")),
                    new PackageIdentity("C", new NuGetVersion("2.0.0")),
                    new PackageIdentity("D", new NuGetVersion("1.0.0-RC")),
                };

            public static readonly List<PackageIdentity> PackagesSet2 = new List<PackageIdentity>()
                {
                    new PackageIdentity("E", new NuGetVersion("1.0.0")),
                    new PackageIdentity("E", new NuGetVersion("2.0.0")),
                    new PackageIdentity("F", new NuGetVersion("1.0.0-ALPHA")),
                };

            public IList<PackageIdentity> AddPackagesToSource()
            {
                return AddPackagesToSource(PackagesSet0, 0);
            }

            /// <summary>
            /// TEST method: Adds packages to the test source.
            /// </summary>
            /// <param name="packages">List of packages to be added</param>
            /// <param name="packageLevel">0 if nupkg is at {root}\, 1 if nupkg is at directory {root}\{packageId}\
            /// and 2 if nupkg is at directory {root}\{packageId}\{packageVersion}\. </param>
            /// <returns></returns>
            public IList<PackageIdentity> AddPackagesToSource(
                List<PackageIdentity> packages,
                int packageLevel)
            {
                foreach(var package in packages)
                {
                    var packageDirectory = SourceFeed;
                    if (packageLevel == 2)
                    {
                        packageDirectory = Path.Combine(SourceFeed, package.Id, package.Version.ToString());
                        Directory.CreateDirectory(packageDirectory);
                    }
                    else if (packageLevel == 1)
                    {
                        packageDirectory = Path.Combine(SourceFeed, package.Id);
                        Directory.CreateDirectory(packageDirectory);
                    }

                    TestPackagesGroupedByFolder.GetLegacyTestPackage(packageDirectory,
                        package.Id,
                        package.Version.ToString());
                }

                return packages;
            }

            public void Dispose()
            {
                TestFileSystemUtility.DeleteRandomTestFolders(WorkingPath, SourceFeed, DestinationFeed);
            }
        }


        [Theory]
        [InlineData("init")]
        [InlineData("init -?")]
        [InlineData("init srcFolder")]
        [InlineData("init srcFolder destFolder extraArg")]
        public void InitCommand_Success_InvalidArguments_HelpMessage(string args)
        {
            // Arrange & Act
            var result = CommandRunner.Run(
                Util.GetNuGetExePath(),
                Directory.GetCurrentDirectory(),
                args,
                waitForExit: true);

            // Assert
            Util.VerifyResultSuccess(result, "usage: NuGet init <srcPackageSourcePath> <destPackageSourcePath> [options]");
        }

        [Fact]
        public void InitCommand_Success_DestinationProvided()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationIsRelative()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    ".",
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.WorkingPath); // Working path is the destination feed
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationDoesNotExist()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var testInfo = new TestInfo(TestFileSystemUtility.CreateRandomTestFolder(), nonExistentPath))
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationAlreadyContainsPackages()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);

                // Main Act
                result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Main Assert
                Util.VerifyResultSuccess(result);
                var output = result.Item2;
                foreach(var p in packages)
                {
                    output.Contains(string.Format(
                    NuGetResources.AddCommand_PackageAlreadyExists,
                    p.ToString(),
                    testInfo.DestinationFeed));
                }
            }
        }

        [Fact]
        public void InitCommand_Success_DestinationAlreadyContainsInvalidPackages()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);

                var firstPackage = packages.First();
                var packageId = firstPackage.Id.ToLowerInvariant();
                var packageIdDirectory = Path.Combine(testInfo.DestinationFeed, packageId);
                Assert.True(Directory.Exists(packageIdDirectory));

                var packageVersion = firstPackage.Version.ToNormalizedString();
                var packageVersionDirectory = Path.Combine(packageIdDirectory, packageVersion);
                Assert.True(Directory.Exists(packageVersionDirectory));

                var nupkgFileName = Util.GetNupkgFileName(packageId, packageVersion);
                var nupkgFilePath = Path.Combine(packageVersionDirectory, nupkgFileName);
                File.Delete(nupkgFilePath);

                // Main Act
                result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Main Assert
                Util.VerifyResultSuccess(result, string.Format(
                    NuGetResources.AddCommand_ExistingPackageInvalid,
                    firstPackage.ToString(),
                    testInfo.DestinationFeed));

                var output = result.Item2;
                foreach (var p in packages.Skip(1))
                {
                    output.Contains(string.Format(
                    NuGetResources.AddCommand_PackageAlreadyExists,
                    p.ToString(),
                    testInfo.DestinationFeed));
                }
            }
        }

        [Fact]
        public void InitCommand_Success_SourceIsRelative()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    ".",
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.SourceFeed, // Source Feed is the working path
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_SourceNoPackages()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                // Add no packages to the source.

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedMessage = string.Format(
                    NuGetResources.InitCommand_FeedContainsNoPackages,
                    testInfo.SourceFeed);

                Util.VerifyResultSuccess(result, expectedMessage);
            }
        }

        [Fact]
        public void InitCommand_Success_SourceContainsInvalidPackages()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                // Add an invalid package. Following calls add an invalid package to SourceFeed.
                var tempFile = Path.GetTempFileName();
                var invalidPackageIdentity = new PackageIdentity("Invalid", new NuGetVersion("1.0.0"));
                var invalidPackageFile = Path.Combine(
                    testInfo.SourceFeed,
                    invalidPackageIdentity.Id + "." + invalidPackageIdentity.Version.ToString() + ".nupkg");
                File.Move(tempFile, invalidPackageFile);

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedWarningMessage = string.Format(
                    NuGetResources.NupkgPath_InvalidNupkg,
                    invalidPackageFile);

                Util.VerifyResultSuccess(
                    result,
                    expectedOutputMessage: expectedWarningMessage);

                Util.VerifyPackagesExist(packages, testInfo.DestinationFeed);

                // Verify that the invalid package was not copied
                Util.VerifyPackageDoesNotExist(invalidPackageIdentity, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Fail_SourceDoesNotExist()
        {
            // Arrange
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            using (var testInfo = new TestInfo(nonExistentPath, TestFileSystemUtility.CreateRandomTestFolder()))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedErrorMessage
                    = string.Format(NuGetResources.InitCommand_FeedIsNotFound, testInfo.SourceFeed);
                Util.VerifyResultFailure(result, expectedErrorMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_SourceIsHttpSource()
        {
            // Arrange
            var invalidPath = "https://api.nuget.org/v3/index.json";
            using (var testInfo = new TestInfo(invalidPath, TestFileSystemUtility.CreateRandomTestFolder()))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedErrorMessage
                    = string.Format(NuGetResources.Path_Invalid_NotFileNotUnc, testInfo.SourceFeed);
                Util.VerifyResultFailure(result, expectedErrorMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_DestinationIsHttpSource()
        {
            // Arrange
            var invalidPath = "https://api.nuget.org/v3/index.json";
            using (var testInfo = new TestInfo(TestFileSystemUtility.CreateRandomTestFolder(), invalidPath))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedErrorMessage
                    = string.Format(NuGetResources.Path_Invalid_NotFileNotUnc, testInfo.DestinationFeed);
                Util.VerifyResultFailure(result, expectedErrorMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_SourceIsInvalid()
        {
            // Arrange
            var invalidPath = "foo|<>|bar";
            using (var testInfo = new TestInfo(invalidPath, TestFileSystemUtility.CreateRandomTestFolder()))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedMessage = string.Format(NuGetResources.Path_Invalid, invalidPath);
                Util.VerifyResultFailure(result, expectedMessage);
            }
        }

        [Fact]
        public void InitCommand_Fail_DestinationIsInvalid()
        {
            // Arrange
            var invalidPath = "foo|<>|bar";
            using (var testInfo = new TestInfo(TestFileSystemUtility.CreateRandomTestFolder(), invalidPath))
            {
                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                var expectedMessage = string.Format(NuGetResources.Path_Invalid, invalidPath);
                Util.VerifyResultFailure(result, expectedMessage);
            }
        }

        [Fact]
        public void InitCommand_Success_V2Style_DestinationProvided()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packagesAtRoot = testInfo.AddPackagesToSource(TestInfo.PackagesSet0, 0);
                var packagesAtIdDirectory = testInfo.AddPackagesToSource(TestInfo.PackagesSet1, 1);

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packagesAtRoot, testInfo.DestinationFeed);
                Util.VerifyPackagesExist(packagesAtIdDirectory, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_V3Style_DestinationProvided()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packagesAtVersionDirectory = testInfo.AddPackagesToSource(TestInfo.PackagesSet2, 2);

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyPackagesExist(packagesAtVersionDirectory, testInfo.DestinationFeed);
            }
        }

        [Fact]
        public void InitCommand_Success_ExpandSwitch()
        {
            // Arrange
            using (var testInfo = new TestInfo())
            {
                var packages = testInfo.AddPackagesToSource();

                var args = new string[]
                {
                    "init",
                    testInfo.SourceFeed,
                    testInfo.DestinationFeed,
                    "-Expand"
                };

                // Act
                var result = CommandRunner.Run(
                    testInfo.NuGetExePath,
                    testInfo.WorkingPath,
                    string.Join(" ", args),
                    waitForExit: true);

                // Assert
                Util.VerifyResultSuccess(result);
                Util.VerifyExpandedLegacyTestPackagesExist(packages, testInfo.DestinationFeed);
            }
        }
    }
}
