## ADDED Requirements

### Requirement: The NuGet package README links to the product site

The CLI's NuGet package README (`src/ReleaseTwin.Cli/README.md`, packed as the
tool's `PackageReadmeFile`) SHALL contain a link to the ReleaseTwin product site,
in addition to its existing links to `docs/*.md`, so a reader who discovers the
package via NuGet search can reach the product site without first finding the
GitHub repository.

#### Scenario: The packaged README links to the product site

- **WHEN** `src/ReleaseTwin.Cli/README.md` is inspected
- **THEN** it contains a link to the ReleaseTwin product site
