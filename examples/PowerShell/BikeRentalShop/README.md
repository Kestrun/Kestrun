# BikeRentalShop Samples

This folder groups the two bike-rental sample variants under a single parent so they can share documentation, API comparisons, and future UI work.

## Layout

- `Synchronized/` keeps the domain state as a familiar PowerShell object graph and serializes multi-record writes plus persistence with a single lock.
- `Concurrent/` keeps the in-memory database keyed with concurrent dictionaries end to end and still uses a lock around compound updates and persistence.

## Choosing a Variant

- Use `Synchronized/` when readability and a simple shared-state model matter more than maximizing concurrent collection access.
- Use `Concurrent/` when you want keyed concurrent collections throughout the in-memory store and you want the sample to demonstrate that pattern explicitly.

## Package Commands

```powershell
New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Synchronized -OutputPath .\bike-rental-shop-1.0.0.krpack
New-KrServicePackage -SourceFolder .\examples\PowerShell\BikeRentalShop\Concurrent -OutputPath .\bike-rental-shop-concurrent-1.0.0.krpack
```

## Run Commands

```powershell
pwsh .\examples\PowerShell\BikeRentalShop\Synchronized\Service.ps1 -Port 5443
pwsh .\examples\PowerShell\BikeRentalShop\Concurrent\Service.ps1 -Port 5444
```

## Notes

- Both variants expose the same HTTP API shape so their behavior is easy to compare.
- Both variants persist state under each sample's `data/` folder using `Export-KrSharedState` and `Import-KrSharedState`.
- This parent folder is the right place to add a shared web interface later without mixing it into either backend variant.
