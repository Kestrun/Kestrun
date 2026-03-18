---
layout: default
title: Production Deployment (No Containers)
parent: Guides
nav_order: 36
---

# Production Deployment (No Containers)

This guide shows how to deploy a custom Kestrun PowerShell application to production without containers, using `dotnet kestrun service install`.

## Scope

- Target: VM or bare-metal hosts.
- Runtime model: native OS service/daemon.
- Packaging model: folder bundle or archive bundle.
- Not covered: container deployments (Docker/Kubernetes).

## Prerequisites

- .NET 10 runtime/SDK available on the host.
- `Kestrun.Tool` installed:

```powershell
dotnet tool install -g Kestrun.Tool
```

- Your app entry script exists (usually `server.ps1` or `scripts/start.ps1`).
- The target host account has permissions to install/start services.

## Prepare Your Application Payload

You can deploy from either:

1. A folder that contains your full app content.
2. An archive (`.zip`, `.tar`, `.tgz`, or `.tar.gz`).
3. An HTTP(S) URL that serves one of the supported archive formats.

Example app layout:

```text
MyServiceApp/
  server.ps1
  config/
    production.json
  assets/
```

If you package an archive, include everything needed by the script at runtime.

## Optional: Generate a Checksum for Archive Integrity

For production safety, verify archive integrity at install time.

Windows PowerShell:

```powershell
Get-FileHash .\MyServiceApp.tgz -Algorithm SHA256 | Select-Object -ExpandProperty Hash
```

Linux/macOS shell:

```bash
sha256sum ./MyServiceApp.tgz
```

Keep the resulting hex hash for `--content-root-checksum`.

## Install the Service (Folder Input)

```powershell
dotnet kestrun service install --name my-service --content-root .\MyServiceApp --script .\server.ps1
```

## Install the Service (Archive Input)

```powershell
dotnet kestrun service install --name my-service --content-root .\MyServiceApp.zip --script .\server.ps1
```

With checksum verification:

```powershell
dotnet kestrun service install --name my-service --content-root .\MyServiceApp.tgz --content-root-checksum <sha256-hex>
```

With explicit checksum algorithm:

```powershell
dotnet kestrun service install --name my-service --content-root .\MyServiceApp.tar.gz --content-root-checksum <hex> --content-root-checksum-algorithm sha512
```

From an HTTP(S) URL:

```powershell
dotnet kestrun service install --name my-service --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL with bearer token auth:

```powershell
dotnet kestrun service install --name my-service --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-bearer-token <token> --content-root-checksum <sha256-hex>
```

From an HTTP(S) URL with custom request headers:

```powershell
dotnet kestrun service install --name my-service --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-header x-api-key:<key> --content-root-header x-env:prod --content-root-checksum <sha256-hex>
```

Ignore HTTPS certificate validation (insecure):

```powershell
dotnet kestrun service install --name my-service --content-root https://downloads.example.com/MyServiceApp.tgz --content-root-ignore-certificate --content-root-checksum <sha256-hex>
```

## Choose Account and Bundle Location

Use `--service-user` when the service/daemon must run as a dedicated account.

```powershell
dotnet kestrun service install --name my-service --content-root .\MyServiceApp --service-user svc-kestrun
```

Use `--deployment-root` to control where bundles are staged.

```powershell
dotnet kestrun service install --name my-service --content-root .\MyServiceApp --deployment-root D:\KestrunServices
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

1. Prepare a new folder/archive payload.
2. Stop service.
3. Remove service.
4. Re-install service with new payload.
5. Start service.
6. Query health/status endpoints.

Example:

```powershell
dotnet kestrun service stop --name my-service
dotnet kestrun service remove --name my-service
dotnet kestrun service install --name my-service --content-root .\MyServiceApp-v2.tgz --content-root-checksum <sha256-hex>
dotnet kestrun service start --name my-service
dotnet kestrun service query --name my-service
```

## Production Hardening Notes

- Prefer archive deployments with checksum validation for repeatability.
- Use least-privilege service accounts where possible.
- Keep app configuration and secrets externalized where feasible.
- Store service logs on durable storage and ship them to monitoring.
- Validate restore/redeploy steps in a staging environment first.

## Current Limits

- `--content-root` supports local folders and local archive files.
- URL-based content roots are supported for HTTP(S) archive files.
- Private archive URLs can use bearer token auth via `--content-root-bearer-token`.
- HTTPS certificate bypass is available via `--content-root-ignore-certificate` and should be used only for controlled environments.
- `.7z` archives are not currently supported.

---

Return to the [Guides index](./index).
