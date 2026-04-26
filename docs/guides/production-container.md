---
layout: default
title: Production Deployment (Container)
parent: Guides
nav_order: 37
---
<!-- markdownlint-disable MD029 -->

# Production Deployment (Container)

This guide shows how to package and run a custom Kestrun PowerShell application in Docker using
`New-KrDockerDeployment`.

The generated deployment bundle is based on:

- the Microsoft ASP.NET Core .NET 10 runtime image
- PowerShell 7.6 installed from the Microsoft Linux package repository
- a packaged Kestrun service application (`.krpack`)

## Scope

- Target: Docker hosts and Docker Compose deployments.
- Runtime model: container image with a packaged PowerShell Kestrun app.
- Packaging model: `.krpack` service package.
- Not covered: native VM/bare-metal services and daemons. See [Production Deployment (Service/Daemon)](./production-service-daemon).
- Not covered: Kubernetes manifests or Helm charts.

## Prerequisites

- Docker Engine or Docker Desktop with `docker compose` available.
- A valid `.krpack` package for your Kestrun service.
- The `Kestrun` PowerShell module available on the machine generating the bundle.
- If you build the `.krpack` locally, PowerShell 7.4 or greater is recommended.

## Container Flow Overview

The recommended workflow is:

1. Build a `.krpack` from your app.
2. Generate a Docker deployment bundle with `New-KrDockerDeployment`.
3. Build the image.
4. Run it with Docker Compose or `docker run`.
5. Rebuild and redeploy when your package changes.

## Prepare a Service Package

As with service/daemon deployments, start from a `.krpack`.

If you already have an application folder with a valid `Service.psd1`:

```powershell
New-KrServicePackage -SourceFolder .\MyServiceApp -OutputPath .\my-service.krpack
```

If you want to package directly from a single script:

```powershell
New-KrServicePackage `
  -ScriptPath .\Service.ps1 `
  -Name 'my-service' `
  -Description 'Containerized Kestrun service' `
  -Version 1.2.0 `
  -OutputPath .\my-service.krpack
```

Quick verification:

```powershell
Get-KrServiceDescriptor -Path .\MyServiceApp\Service.psd1
```

Or, if you are validating an unpacked package staging folder:

```powershell
Expand-Archive .\my-service.krpack .\tmp\my-service -Force
Get-KrServiceDescriptor -Path .\tmp\my-service
```

## Generate a Docker Deployment Bundle

Use `New-KrDockerDeployment` to create a self-contained Docker build context from the `.krpack`.

Minimal usage:

```powershell
New-KrDockerDeployment -PackagePath .\my-service.krpack
```

By default this creates:

- an output folder named `<service-name>-docker`
- a Docker image name like `kestrun-<service-name>:<version>`
- a Docker Compose service/container name based on the package descriptor name
- host/container port mapping `8080:8080`

Explicit output and ports:

```powershell
New-KrDockerDeployment `
  -PackagePath .\my-service.krpack `
  -OutputPath .\deploy\docker `
  -PublishedPort 5000 `
  -ContainerPort 8080
```

Custom image and service names:

```powershell
New-KrDockerDeployment `
  -PackagePath .\my-service.krpack `
  -ServiceName 'frontend-api' `
  -ImageName 'registry.example.com/my-service:1.2.0'
```

Overwrite an existing generated bundle:

```powershell
New-KrDockerDeployment -PackagePath .\my-service.krpack -OutputPath .\deploy\docker -Force
```

## `New-KrDockerDeployment` Parameters

Required:

- `-PackagePath <string>`: path to the `.krpack` service package.

Optional:

- `-OutputPath <string>`: output directory for the generated Docker bundle. Defaults to `<service-name>-docker`.
- `-ImageName <string>`: image name written into `docker-compose.yml`.
- `-ServiceName <string>`: Compose service name and container name.
- `-PublishedPort <int>`: host port exposed by Docker Compose. Defaults to `8080`.
- `-ContainerPort <int>`: container port exposed by the image and used for `PORT`/`ASPNETCORE_URLS`. Defaults to `8080`.
- `-KestrunModulePath <string>`: optional path to a local `Kestrun` module root to stage into the image.
- `-Force`: overwrite an existing output bundle.
- `-WhatIf` and `-Confirm`: standard PowerShell safety switches.

The cmdlet returns a summary object with paths and resolved names, including:

- `DeploymentPath`
- `ComposePath`
- `DockerfilePath`
- `EntrypointPath`
- `ServiceName`
- `ImageName`
- `PublishedPort`
- `ContainerPort`

## Generated Bundle Layout

The generated output folder contains:

- `docker-compose.yml`
- `Dockerfile`
- `entrypoint.sh`
- `app.krpack`
- `Kestrun/`
- `.dockerignore`

Purpose of each file:

- `docker-compose.yml`: local/default deployment manifest with image name, build context, ports, and environment variables.
- `Dockerfile`: image definition for ASP.NET Core + PowerShell + staged Kestrun module + app package.
- `entrypoint.sh`: runtime startup script that extracts the package, resolves `Service.psd1`, and launches the packaged entry script.
- `app.krpack`: your packaged Kestrun app copied into the image build context.
- `Kestrun/`: a staged copy of the current Kestrun module used during image build.
- `.dockerignore`: keeps the Docker build context minimal and predictable.

## Runtime Behavior Inside the Container

The generated image does the following:

1. Starts from `mcr.microsoft.com/dotnet/aspnet:10.0`.
2. Installs PowerShell from the Microsoft package repository.
3. Copies the staged `Kestrun` module into the first PowerShell module path as:

```text
Kestrun/<version>
```

4. Creates `/opt/microsoft/powershell/7/profile.ps1` with:

```powershell
if (Get-Module -ListAvailable Kestrun) {
    Import-Module Kestrun
}
```

5. Copies `app.krpack` and the startup script into the image.
6. At container startup:
   - extracts `app.krpack` into `/opt/kestrun/service`
   - reads `Service.psd1`
   - resolves `EntryPoint`
   - executes the packaged PowerShell script with `pwsh -File`

The generated Compose file sets:

- `PORT`
- `ASPNETCORE_URLS`

This means a typical app using:

```powershell
Add-KrEndpoint -Port $Port
```

can run without additional container-specific code.

## Why the Profile Import Matters

Containerized Kestrun apps can define classes, attributes, and other types that need the `Kestrun` module available when the entry script is evaluated.

The generated profile ensures that when the packaged entry script starts, `Kestrun` is already imported in the PowerShell session.

This keeps the startup path simpler than maintaining a custom bootstrap wrapper for every app.

## Build the Image

From the generated bundle folder:

```powershell
cd .\my-service-docker
docker build -t kestrun-my-service:1.2.0 .
```

Or use Compose:

```powershell
cd .\my-service-docker
docker compose build
```

## Run the Container with Docker Compose

The generated `docker-compose.yml` is the recommended starting point:

```powershell
cd .\my-service-docker
docker compose up
```

Run detached:

```powershell
docker compose up -d
```

Stop and remove:

```powershell
docker compose down
```

Rebuild after package changes:

```powershell
docker compose build --no-cache
docker compose up -d
```

## Run the Container with `docker run`

If you do not want to use Compose:

```powershell
docker run --rm -p 5000:8080 --name my-service kestrun-my-service:1.2.0
```

Use `-d` for detached mode:

```powershell
docker run -d -p 5000:8080 --name my-service kestrun-my-service:1.2.0
```

Inspect logs:

```powershell
docker logs my-service
```

Open an interactive PowerShell session:

```powershell
docker exec -it my-service pwsh
```

## Environment Variables and Port Binding

The generated Compose file sets:

```yaml
environment:
  PORT: "8080"
  ASPNETCORE_URLS: "http://+:8080"
```

This aligns with Kestrun’s container-friendly endpoint behavior.

If your service script does:

```powershell
param(
    [int]$Port = $env:PORT ?? 8080
)

Add-KrEndpoint -Port $Port
```

the container will bind correctly to all interfaces inside the container.

To change ports, regenerate the bundle with different values:

```powershell
New-KrDockerDeployment `
  -PackagePath .\my-service.krpack `
  -PublishedPort 5000 `
  -ContainerPort 5001 `
  -Force
```

That produces:

- host port `5000`
- container port `5001`
- `PORT=5001`
- `ASPNETCORE_URLS=http://+:5001`

## Configuration, Secrets, and Volumes

The generated bundle is intentionally minimal. For production use, extend `docker-compose.yml` as needed.

Typical additions:

- bind mounts or named volumes for durable app data
- environment-specific configuration via `environment:` or `env_file:`
- reverse proxy integration
- restart policy tuning
- secret injection through your deployment platform

Example additions:

```yaml
services:
  my-service:
    volumes:
      - my-service-data:/opt/kestrun/service/data
    env_file:
      - .env.production

volumes:
  my-service-data:
```

If your app writes logs or mutable state under the extracted service root, add an explicit volume for those paths.

## Update Workflow

When your service changes:

1. increment the package version in `Service.psd1`
2. build a new `.krpack`
3. regenerate the Docker deployment bundle
4. rebuild the image
5. redeploy the container

Example:

```powershell
New-KrServicePackage `
  -SourceFolder .\MyServiceApp `
  -OutputPath .\my-service-1.2.1.krpack `
  -Force

New-KrDockerDeployment `
  -PackagePath .\my-service-1.2.1.krpack `
  -OutputPath .\deploy\docker `
  -Force

cd .\deploy\docker
docker compose build --no-cache
docker compose up -d
```

## Production Hardening Notes

- Treat the generated bundle as a starting point, not the final shape for every environment.
- Pin image tags intentionally; avoid ambiguous rollout practices.
- Use volumes for durable data and logs when needed.
- Put secrets outside the image.
- Prefer a reverse proxy, ingress, or load balancer in front of the container for TLS termination and public exposure.
- Validate your generated bundle in staging before promoting it.
- If you need a non-root container, extend the generated `Dockerfile` and verify all file and port permissions.

## Troubleshooting

### The container starts but the app is not reachable

Check the mapped host and container ports:

```powershell
docker ps
docker logs my-service
```

Verify your app uses the generated `PORT` or `ASPNETCORE_URLS` settings rather than a hard-coded port.

### Compose validation fails

Inspect the generated Compose file:

```powershell
Get-Content .\docker-compose.yml
```

Regenerate the bundle with `-Force` if the folder contains stale output.

### PowerShell module import issues inside the container

Confirm the module is installed:

```powershell
docker exec -it my-service pwsh -Command "Get-Module -ListAvailable Kestrun"
```

Confirm the profile exists:

```powershell
docker exec -it my-service pwsh -Command "Get-Content /opt/microsoft/powershell/7/profile.ps1"
```

### The service package entry script is wrong

Inspect `Service.psd1` inside the extracted app:

```powershell
docker exec -it my-service pwsh -Command "Import-PowerShellDataFile /opt/kestrun/service/Service.psd1 | Format-List"
```

Make sure `EntryPoint` resolves to a real file under the packaged app root.

## Current Limits

- `New-KrDockerDeployment` targets Docker and Docker Compose output today.
- The generated image is Linux-based and built from the .NET 10 ASP.NET runtime image.
- The generated bundle does not create Kubernetes manifests.
- Persistent volumes, secrets management, health orchestration, and reverse-proxy wiring are left to the deployment environment.

---

Return to the [Guides index](./index).
