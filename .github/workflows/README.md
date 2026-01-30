# GitHub Actions Workflows

This repository uses GitHub Actions for continuous integration and deployment.

## Workflows

### Build, Test & Package (`build.yml`)

This workflow runs on every push to `main` or `develop` branches, on pull requests to `main`, and when releases are published.

#### Jobs

1. **build-and-test**
   - Restores dependencies
   - Builds the solution in Release mode
   - Runs all tests with code coverage
   - Uploads coverage reports to Codecov
   - Uploads test results as artifacts

2. **package**
   - Creates NuGet packages
   - Version is determined by:
     - Release tag (e.g., `v1.2.3`) for releases
     - Dev build number (e.g., `1.0.0-dev.123`) for commits
   - Uploads NuGet package as artifact

3. **publish**
   - Publishes to NuGet.org (only on releases)
   - Requires `NUGET_API_KEY` secret to be configured

4. **build-samples**
   - Builds both sample applications
   - Verifies they can start successfully
   - Ensures examples stay up-to-date

## Required Secrets

To enable full CI/CD, configure these secrets in your repository settings:

- `NUGET_API_KEY`: Your NuGet.org API key (for publishing packages)
- `CODECOV_TOKEN`: Your Codecov token (optional, for coverage reports)

## Badges

Add these badges to your README.md:

```markdown
![Build Status](https://github.com/evilz/SpecEnforcer/actions/workflows/build.yml/badge.svg)
[![codecov](https://codecov.io/gh/evilz/SpecEnforcer/branch/main/graph/badge.svg)](https://codecov.io/gh/evilz/SpecEnforcer)
[![NuGet](https://img.shields.io/nuget/v/SpecEnforcer.svg)](https://www.nuget.org/packages/SpecEnforcer/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SpecEnforcer.svg)](https://www.nuget.org/packages/SpecEnforcer/)
```

## Local Testing

To test the workflow locally before pushing:

```bash
# Build
dotnet build --configuration Release

# Test
dotnet test --configuration Release --verbosity normal

# Pack
dotnet pack src/SpecEnforcer/SpecEnforcer.csproj --configuration Release --output ./output

# Build samples
dotnet build examples/SampleApi/SampleApi.csproj --configuration Release
dotnet build examples/AdvancedSampleApi/AdvancedSampleApi.csproj --configuration Release
```

## Release Process

1. Create a new release on GitHub
2. Tag the release with a version number (e.g., `v1.0.0`)
3. The workflow will automatically:
   - Build and test
   - Create NuGet package with the release version
   - Publish to NuGet.org

## Troubleshooting

### Package Version Issues
- Ensure release tags follow semantic versioning (e.g., `v1.2.3`)
- The workflow strips the `v` prefix automatically

### NuGet Push Failures
- Verify `NUGET_API_KEY` secret is set correctly
- Ensure the API key has push permissions
- Check that the package version doesn't already exist

### Test Failures
- Review test results in the Actions tab
- Download test results artifacts for detailed analysis
- Check code coverage reports on Codecov
