# Renzyu

## Build

The application requires Node.js, npm, and Visual Studio 2022 or Build Tools with
the ASP.NET and web development workload.

```powershell
npm ci
npm run build:client
msbuild Renzyu.sln /restore /p:Configuration=Debug
```

NuGet dependencies use `PackageReference`. Browser dependencies are locked in
`package-lock.json` and generated under `Host\Scripts\vendor`; neither restored
dependency tree is committed.

Use `/p:RestoreLockedMode=true` in CI to reject dependency graph changes that
are not reflected in the committed NuGet lock files.