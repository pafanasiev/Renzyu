# Renzyu

Renzyu is an ASP.NET Core MVC and SignalR application targeting .NET 10 LTS.

## Local development

Install the .NET 10 SDK, Node.js 24 LTS, and npm. The application build restores
the locked browser dependencies and generates the assets under
`Host\wwwroot\Scripts\vendor`.

```powershell
dotnet restore Renzyu.sln --locked-mode
dotnet build Renzyu.sln --no-restore
dotnet test Renzyu.sln --no-build
.\run-local.ps1
```

Pass `-Port` or `-Configuration` to `run-local.ps1` when needed.

## Container

Build and run the Linux image with Docker:

```powershell
docker build --tag renzyu .
docker run --rm --publish 8080:8080 renzyu
```

Open `http://localhost:8080`. The final image uses the official non-root
ASP.NET Core 10 runtime image; the .NET SDK and Node.js remain in build stages.

NuGet and npm dependencies are locked. Use locked restore mode in CI so
dependency graph changes must be committed explicitly.
