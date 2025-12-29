# Kestrun Launcher Example

This directory contains a simple example app for testing the `kestrun-launcher` tool.

## Files

- `server.ps1` - A minimal Kestrun server script that can be launched

## Usage

### Run directly with launcher

```powershell
# Run the app
dotnet run --project ../../../src/CSharp/Kestrun.Launcher -- run ./server.ps1

# Or using the built executable
../../../src/CSharp/Kestrun.Launcher/bin/Debug/net10.0/kestrun-launcher run ./server.ps1
```

### Install as Windows Service (Windows only)

```powershell
# Install the service
kestrun-launcher install ./server.ps1 -n KestrunTestService

# Start the service
kestrun-launcher start -n KestrunTestService

# Stop the service
kestrun-launcher stop -n KestrunTestService

# Uninstall the service
kestrun-launcher uninstall -n KestrunTestService
```

## Testing

Once the server is running, test it with:

```bash
# Health check
curl http://127.0.0.1:5555/health

# Test endpoint
curl http://127.0.0.1:5555/test
```

Expected response:
```json
{
  "message": "Hello from launcher test app!",
  "timestamp": "2025-12-29T21:04:00.000Z"
}
```
