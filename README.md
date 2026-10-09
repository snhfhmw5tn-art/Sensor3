# Sensor 3

Visual Studios Windows-bygge behöver MSVC C++ x64/x86 build tools (`Microsoft.VisualStudio.Component.VC.Tools.x86.x64`). Kravet deklareras i `.vsconfig`. Installera komponenten via Visual Studio Installer med Visual Studio stängt. Ett godkänt `dotnet build` ersätter inte kontroll med Visual Studios egen MSBuild.

Installera även MAUI-komponentgrupperna Android, Windows, Blazor och Shared som anges i `.vsconfig`. .NET-workloads installerade via CLI ersätter inte IDE-verktygen. Kontrollerat i Visual Studio 2026 efter installation: hela solutionen byggde med 17 lyckade projekt och felpanelen visade 0 fel/0 varningar.

Byggkonfigurationen hittar även lokalt installerad Android SDK i `%LOCALAPPDATA%\Android\Sdk` och Java i `%LOCALAPPDATA%\Microsoft\Jdk` när inga explicita SDK-sökvägar har angetts. Befintliga explicita sökvägar har företräde.

Native Android/Windows-klient, ASP.NET Core API och Blazor-dashboard, .NET 10 / C# 14. Version 0.5.0. Arbetet följer [masterplanen](docs/MASTERPLAN.md), en iteration i taget.

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

## Distributionsportal – iteration 02

Öppna dashboardens `/download` i Android- eller Windows-webbläsaren. Plattform föreslås från user agent när möjligt; manuellt val och Development/Beta/Stable finns alltid. Inga exempelpaket läggs i releasekatalogen. Klientpaketens metadata visas separat från portal/serverversion.

Administration: kör `./tools/Configure-DistributionAdmin.ps1` i PowerShell för att välja lösenord och spara PBKDF2-hash i båda hostprojektens lokala user-secrets. Kör sedan API/dashboard i Development och öppna `/admin/releases`. Utan konfigurerat konto är administration avstängd. Lösenordet skrivs aldrig i Git eller loggar. User-secrets är lokal utvecklingskonfiguration, inte krypterad produktionslagring. I produktion används HTTPS och en hemlighetshanterare för `Distribution:AdminUsername` och `Distribution:AdminPasswordHash` (miljövariabler med dubbla understreck stöds). Se [iterationsrapporten](docs/iteration-02.md) för manifest och driftkrav.

## Installation och uppdateringar – iteration 03

Native-klientens `/updates` visar installerad och tillgänglig version samt release notes. Den kontrollerar vid start och verifierar signerade manifest, nonce, kanal, plattform, versionsordning, SHA-256 och filstorlek före OS-installation. Aktiva mätningar skyddas med ett gemensamt sessionslås. Windows-portalen erbjuder `.appinstaller` med kontroll vid start och användarprompt. Publicering kräver verifierad paketsignatur och inbyggd appidentitet/version.

Konfigurera riktig HTTPS-server, betrodd publik manifestnyckel och externa signeringsnycklar före skarp paketering. Defaultinställningen stoppar uppdateringar tills detta finns. Se [iteration 03 med kommandon och testresultat](docs/iteration-03.md). Android/Windows-byggen och automatiska kontroller är gjorda; verkliga signerade v1→v2-installationer och bevarad appdata återstår. Iteration 03 är Implemented, inte Verified eller Released.

API och dashboard ska ha samma `Distribution:StorageRoot`, med skrivrättighet bara för serverkontot och utanför webroot. Standard är `%LOCALAPPDATA%\Sensor3\distribution`. Default max paketstorlek är 100 MiB (`Distribution:MaximumArtifactBytes`, upp till 1 GiB). GET `/api/releases`, `/api/releases/latest?platform=Android&channel=Stable` och `/api/updates/check?platform=Android&channel=Stable&version=0.1.0&buildNumber=1` finns på båda hostarna. GET `/api/releases/{id}/download` kontrollerar återkallelse och integritet. Okänd kanal/plattform ger 400; saknad kompatibel release ger 404 på latest och NoCompatibleRelease på updates/check.

Publicera endast kontrollerade och signerade paket via administratörsformuläret. Paketsignaturer/uppgraderingar ingår i iteration 03; ingen faktisk APK/MSIX-release har publicerats här. Kör `./tools/Distribution-Smoke-Test.ps1` för HTTP-/säkerhetskontroller med tillfälliga syntetiska testfiler. Verify.ps1 kör även dessa kontroller.

## Android-byggmiljö

Utöver .NET Android-workload behövs Android SDK och en kompatibel JDK. Installera dem genom Visual Studios Android-verktyg eller .NET-målet InstallAndroidDependencies efter att du godkänt SDK-licenserna. Ange ANDROID_HOME till SDK-mappen och JAVA_HOME till JDK-mappen som användarvariabler, och starta om Visual Studio efter ändringar. Maskinspecifika sökvägar ska inte läggas i projektfilen.

Kontrollerat lokalt 2026-10-09: Android SDK med workload 36.1.43, Microsoft OpenJDK 17.0.14; hela solutionen bygger för Android och Windows med 0 fel och 0 varningar. Starttest på Android kräver ansluten enhet eller emulator.

## Native sensorinsamling – iteration 04

Öppna **Sensorinsamling** (`/sensors`) i Android- eller Windows-appen, inventera och välj sensorer. Begär vid behov behörighet för valda Android-sensorer och tryck Starta. Standard är 50 Hz; 1–200 Hz kan begäras. OS/driver bestämmer faktisk takt, som beräknas från de mottagna sensorernas egna tidsstämplar. Saknad hårdvara visas som Unsupported, utan exempelvärden. Stoppa avslutar sessionen. Insamlingen stoppas också när sidan lämnas eller appen tappar fokus/går till bakgrunden; återstart är manuell.

Rådata visas lokalt och skickas inte till servern. Webbläsaren erbjuder ingen sensorinsamling. Normaliserade enheter och native tidskällor bevaras; olika referensramar har ännu inte förenats. Full sensordiagnostik hör till iteration 05. Se [iteration 04](docs/iteration-04.md) för kontroller, berörda filer och kvarvarande hårdvaruprov. Status är Implemented; ingen klientrelease har publicerats.
## Sensordiagnostik – iteration 05

Öppna `/diagnostics` (även `/sensors`) i native-appen. Alla inventerade sensorer visas med tillverkare eller Unknown, tillgänglighet, behörighet, aktivitet, kvalitet, fel, frekvens och tidsstämpel. Grafer och kanaltabeller visar råa normaliserade värden och diagnostisk exponentiell utjämning där det är tillämpligt. Ingen positioneringsalgoritm är aktiv ännu.

Blazor-dashboarden har samma mottagarvy, utan egen sensorinsamling. Klientmetadata visas som Unknown tills en native-klient har levererat data via mottagarkontraktet; faktisk nätverksanslutning tillkommer i iteration 07. Se [iteration 05](docs/iteration-05.md) för statusmodell, filtermetod, tester och hårdvarubegränsningar. Status är Implemented, inte Verified eller Released.