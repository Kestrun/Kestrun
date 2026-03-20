---
layout: default
title: Production Deployment (No Containers)
parent: Guides
nav_order: 36
---

# Production Deployment (No Containers)

This guide shows how to deploy and update a custom Kestrun PowerShell application to production without containers using
`dotnet kestrun service install` and `dotnet kestrun service update`.

## Scope

- Target: VM or bare-metal hosts.
- Runtime model: native OS service/daemon.
- Packaging model: `.krpack` service package (recommended).
- Also supported: descriptor-driven `--content-root` installs from folder/archive/HTTP(S) archive.
- Not covered: container deployments (Docker/Kubernetes).

## Prerequisites

- .NET 10 runtime/SDK available on the host.
- `Kestrun.Tool` installed:

```powershell
dotnet tool install -g Kestrun.Tool
```

- Your app entry script exists (usually `Service.ps1` or `scripts/start.ps1`).
- If you build packages on the host, ensure the `Kestrun` PowerShell module is available.
- The target host account has permissions to install/start services.

## Prepare a Service Package (Recommended)

Build a `.krpack` package from your app folder before deployment:

```powershell
New-KrServicePackage -SourceFolder .\MyServiceApp -OutputPath .\my-service.krpack
```

The package embeds `Service.psd1` metadata and app content used by `service install --package` and `service update --package`.

## Service Descriptor Basics (`Service.psd1`)

When using package or descriptor-based content deployments, `Service.psd1` should define at least:

- `FormatVersion` (`'1.0'`)
- `Name`
- `Description`
- `Version`
- `EntryPoint`

Optional keys include `ServiceLogPath` and `PreservePaths`.

Example:

```powershell
@{
  FormatVersion = '1.0'
  Name = 'my-service'
  Description = 'Production Kestrun service'
  Version = '1.2.0'
  EntryPoint = './Service.ps1'
  PreservePaths = @(
    'config/production.json'
    'data/'
    'logs/'
  )
}
```

`PreservePaths` values must be relative paths inside the app root (no absolute paths and no `..` traversal). During `service update --package`, those paths are
staged from the current install and restored after the package content is applied.

## Content-Root Input (Optional)

You can still deploy from:

1. A folder that contains your full app content.
2. An archive (`.zip`, `.tar`, `.tgz`, or `.tar.gz`).
3. An HTTP(S) URL that serves one of the supported archive formats.

Example app layout:

```text
MyServiceApp/
  Service.ps1
  config/
    production.json
  assets/
```

If you package an archive, include everything needed by the configured `EntryPoint` at runtime.

## Optional: Generate a Checksum

For production safety, verify package/archive integrity at install or update time.

Windows PowerShell:

```powershell
Get-FileHash .\my-service.krpack -Algorithm SHA256 | Select-Object -ExpandProperty Hash
```

Linux/macOS shell:

```bash
sha256sum ./my-service.krpack
```

Keep the resulting hex hash for `--content-root-checksum`.

## Install the Service (Package Input)

```powershell
dotnet kestrun service install --package .\my-service.krpack
```

With checksum verification:

```powershell
dotnet kestrun service install --package .\my-service.krpack --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL:

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL with bearer token auth:

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack --content-root-bearer-token <token> --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL with custom request headers:

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack --content-root-header x-api-key:<key> --content-root-header x-env:prod --content-root-checksum <sha256-hex>
```

Ignore HTTPS certificate validation (insecure):

```powershell
dotnet kestrun service install --package https://downloads.example.com/my-service.krpack --content-root-ignore-certificate --content-root-checksum <sha256-hex>
```

## Install the Service (Content-Root Input)

```powershell
dotnet kestrun service install --content-root .\MyServiceApp
```

## Install the Service (Archive Input)

```powershell
dotnet kestrun service install --content-root .\MyServiceApp.zip
```

With checksum verification:

```powershell
dotnet kestrun service install --content-root .\MyServiceApp.tgz --content-root-checksum <sha256-hex>
```

With explicit checksum algorithm:

```powershell
dotnet kestrun service install --content-root .\MyServiceApp.tar.gz --content-root-checksum <hex> --content-root-checksum-algorithm sha512
```

From an HTTP(S) URL:

```powershell
dotnet kestrun service install --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL with bearer token auth:

```powershell
dotnet kestrun service install --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-bearer-token <token> --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL with custom request headers:

```powershell
dotnet kestrun service install --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-header x-api-key:<key> --content-root-header x-env:prod --content-root-checksum <sha256-hex>
```

Ignore HTTPS certificate validation (insecure):

```powershell
dotnet kestrun service install --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-ignore-certificate --content-root-checksum <sha256-hex>
```

## Choose Account and Bundle Location

Use `--service-user` when the service/daemon must run as a dedicated account.

```powershell
dotnet kestrun service install --package .\my-service.krpack --service-user svc-kestrun
```

Use `--deployment-root` to control where bundles are staged.

```powershell
dotnet kestrun service install --package .\my-service.krpack --deployment-root D:\KestrunServices
```

## Start and Validate

```powershell
dotnet kestrun service start --name my-service
dotnet kestrun service query --name my-service
```

Recommended checks:

- Confirm service is running.
- Confirm your API endpoints respond.
- Confirm bootstrap logs and app logs are written to expected paths.

## Upgrade Workflow (No Containers)

1. Build and publish a new `.krpack` payload (with incremented `Version` in `Service.psd1`).
2. Stop the service.
3. Run `service update --package`.
4. Start service.
5. Query service and API health.

Example:

```powershell
dotnet kestrun service stop --name my-service
dotnet kestrun service update --name my-service --package .\my-service-v2.krpack --content-root-checksum <sha256-hex>
dotnet kestrun service start --name my-service
dotnet kestrun service query --name my-service
```

Optional module update (repository module only when newer than bundled):

```powershell
dotnet kestrun service stop --name my-service
dotnet kestrun service update --name my-service --package .\my-service-v2.krpack --kestrun
dotnet kestrun service start --name my-service
```

Fail back to latest backup when needed:

```powershell
dotnet kestrun service stop --name my-service
dotnet kestrun service update --name my-service --failback
dotnet kestrun service start --name my-service
```

## Production Hardening Notes

- Prefer `.krpack` deployments with checksum validation for repeatability.
- Use least-privilege service accounts where possible.
- Keep app configuration and secrets externalized where feasible.
- Store service logs on durable storage and ship them to monitoring.
- Validate restore/redeploy steps in a staging environment first.

## Current Limits

- `service install --package` and `service update --package` support local `.krpack` files and HTTP(S) package URLs.
- `--content-root` remains supported for descriptor-driven installs from local folder/archive or HTTP(S) archive URL.
- Private package/archive URLs can use bearer token auth via `--content-root-bearer-token`.
- HTTPS certificate bypass is available via `--content-root-ignore-certificate` and should be used only for controlled environments.
- `.7z` archives are not currently supported.

---

Return to the [Guides index](./index).
