# BikeRentalShop Samples

This folder groups the bike-rental backends and the standalone Razor web client under a single parent so they can share documentation and API comparisons.

## Layout

- `Synchronized/` keeps the domain state as a familiar PowerShell object graph and serializes multi-record writes plus persistence with a single lock.
- `Concurrent/` keeps the in-memory database keyed with concurrent dictionaries end to end and still uses a lock around compound updates and persistence.
- `Web/` is a separate Razor Pages service that talks to either backend over HTTP and keeps browser concerns out of the API samples.

## Choosing a Variant

- Use `Synchronized/` when readability and a simple shared-state model matter more than maximizing concurrent collection access.
- Use `Concurrent/` when you want keyed concurrent collections throughout the in-memory store and you want the sample to demonstrate that pattern explicitly.
- Use `Web/` when you want a browser-facing experience and you want to point it at either backend variant without changing the API shape.

## Package Commands

```powershell
New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized -OutputPath .\bike-rental-shop-1.0.0.krpack
New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent -OutputPath .\bike-rental-shop-concurrent-1.0.0.krpack
New-KrServicePackage -SourceFolder .\docs\_includes\examples\pwsh\BikeRentalShop\Web -OutputPath .\bike-rental-shop-web-1.0.0.krpack
```

## Run Commands

```powershell
pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Synchronized\Service.ps1 -Port 5443
pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Concurrent\Service.ps1 -Port 5444
pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Synchronized
```

## Cleanup

```powershell
pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Cleanup.ps1
pwsh .\docs\_includes\examples\pwsh\BikeRentalShop\Cleanup.ps1 -IncludeLogs
```

## Notes

- Both variants expose the same HTTP API shape so their behavior is easy to compare.
- Both backend variants persist state under each sample's `data/` folder using `Export-KrSharedState` and `Import-KrSharedState`.
- The web client is intentionally separate, so browser assets and Razor Pages do not leak into either backend sample.
- When you run `Web/` against a backend on another origin, start that backend with `-AllowedCorsOrigins` for the web service origin.
- `Cleanup.ps1` removes the generated shared root cache plus each sample's local `data/` artifacts, and it can also remove `logs/` with `-IncludeLogs`.
