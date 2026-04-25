# VytswellRay

Internal workplace proxy client. Fork of [v2RayN](https://github.com/2dust/v2rayN), simplified to a single-purpose tool.

## Requirements

- Windows 10 x64 / arm64
- Administrator rights (required for TUN mode)
- sing-box + wintun.dll in `Resources/bin/sing-box/`

## Build

```
cd v2rayN
dotnet publish v2rayN/v2rayN.csproj -c Release -r win-x64 -p:SelfContained=true -p:EnableWindowsTargeting=true -o ../out/x64
```

## Distribution

Extract the publish output folder and run `v2rayN.exe` as Administrator.

## Branch

Active development: `simplify-workplace-fork`
Base: `master` (upstream v2RayN 7.21.0)

## Plan

See [SIMPLIFICATION_PLAN.md](SIMPLIFICATION_PLAN.md) for full implementation plan.
