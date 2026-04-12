# BikeRentalShop Web

This sample is a standalone Razor Pages client for the bike rental API. It stays separate from the
`Synchronized` and `Concurrent` backends so the API samples remain backend-only.

## Start Against The Synchronized Backend

```powershell
pwsh .\examples\PowerShell\BikeRentalShop\Synchronized\Service.ps1 -Port 5443 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')
pwsh .\examples\PowerShell\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Synchronized
```

## Start Against The Concurrent Backend

```powershell
pwsh .\examples\PowerShell\BikeRentalShop\Concurrent\Service.ps1 -Port 5444 -AllowedCorsOrigins @('https://127.0.0.1:5445', 'https://localhost:5445')
pwsh .\examples\PowerShell\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Concurrent
```

## Custom Backend URL

```powershell
pwsh .\examples\PowerShell\BikeRentalShop\Web\Service.ps1 -Port 5445 -Backend Custom -ApiBaseUrl 'https://api.example.test:9443'
```

## Notes

- The browser talks directly to the backend API, so the backend must allow the web service origin with `-AllowedCorsOrigins`.
- The UI expects the same API shape from either backend sample.
- When using local self-signed certificates in a browser,
trust the certificate or accept it in the browser before testing the cross-origin calls.
