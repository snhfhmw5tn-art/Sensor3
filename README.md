# Sensor 3

Visual Studios Windows-bygge behöver MSVC C++ x64/x86 build tools (`Microsoft.VisualStudio.Component.VC.Tools.x86.x64`). Kravet deklareras i `.vsconfig`. Installera komponenten via Visual Studio Installer med Visual Studio stängt. Ett godkänt `dotnet build` ersätter inte kontroll med Visual Studios egen MSBuild.

Installera även MAUI-komponentgrupperna Android, Windows, Blazor och Shared som anges i `.vsconfig`. .NET-workloads installerade via CLI ersätter inte IDE-verktygen. Kontrollerat i Visual Studio 2026 efter installation: hela solutionen byggde med 17 lyckade projekt och felpanelen visade 0 fel/0 varningar.

Byggkonfigurationen hittar även lokalt installerad Android SDK i `%LOCALAPPDATA%\Android\Sdk` och Java i `%LOCALAPPDATA%\Microsoft\Jdk` när inga explicita SDK-sökvägar har angetts. Befintliga explicita sökvägar har företräde.

Native Android/Windows-klient, ASP.NET Core API och Blazor-dashboard, .NET 10 / C# 14. Version 0.1.0. Arbetet följer [masterplanen](docs/MASTERPLAN.md), en iteration i taget.

## Bygg och kör

Windows kräver .NET 10 SDK, MAUI Windows-workload och Windows SDK. Android kräver MAUI Android-workload, Android SDK och JDK.

```powershell
dotnet build Sensor3.sln -p:Sensor3WindowsOnly=true
dotnet test Sensor3.Tests/Sensor3.Tests.csproj
dotnet run --project Sensor3.Api --urls http://localhost:5301
dotnet run --project Sensor3.Dashboard --urls http://localhost:5302
```

Använd HTTP endast för lokal utveckling (`ASPNETCORE_ENVIRONMENT=Development`). Produktion använder HTTPS. API har `/health`, `/api/system/build-info` och `/api/system/iterations`. Dashboard och native-klient delar `/about` och permanent versionsrad.

För båda native-målen: `dotnet build Sensor3.sln` efter installation av båda workloads. iOS byggs inte.

BuildInfo genereras automatiskt och bäddas in i varje assembly: faktisk Git-hash/commitdatum, separat UTC-byggdatum, dirty-status och iterationsmanifest. För reproducerbar spårbarhet bygg från en ren commit. Saknat commitdatum visas som Unknown. Buildnummer/releasekanal anges via `-p:Sensor3BuildNumber=... -p:Sensor3ReleaseChannel=Development`.

Se [iterationshistorik](ITERATIONS.md), [arkitektur](ARCHITECTURE.md) och [verifieringsrapport](docs/iteration-01.md). Inga signerade installationspaket publiceras i iteration 01.

## Android-byggmiljö

Utöver .NET Android-workload behövs Android SDK och en kompatibel JDK. Installera dem genom Visual Studios Android-verktyg eller .NET-målet InstallAndroidDependencies efter att du godkänt SDK-licenserna. Ange ANDROID_HOME till SDK-mappen och JAVA_HOME till JDK-mappen som användarvariabler, och starta om Visual Studio efter ändringar. Maskinspecifika sökvägar ska inte läggas i projektfilen.

Kontrollerat lokalt 2026-10-09: Android SDK med workload 36.1.43, Microsoft OpenJDK 17.0.14; hela solutionen bygger för Android och Windows med 0 fel och 0 varningar. Starttest på Android kräver ansluten enhet eller emulator.
