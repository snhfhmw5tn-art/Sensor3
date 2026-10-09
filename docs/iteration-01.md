# Iteration 01 – Grundplattform, Git och versionsinformation

Version: 0.1.0. Status: Implemented. Verified: nej. Publicerad iteration: 0.

## Implementerat

Sensor3.sln med samtliga 17 projekt, Android/Windows MAUI Blazor Hybrid, delade Razor-komponenter, Blazor Server-dashboard och ASP.NET Core API. DI, konfiguration via ASP.NET-standardfiler, ILogger och hälsokontroller finns. Gemensam BuildInfo, inbäddat iterationsmanifest och permanent informationsrad/About visar version, iteration, commit och separata commit-/byggdatum. Saknat commitdatum visas som Unknown. UI visar svensk lokal tid, metadata lagras i UTC.

Byggprocessen genererar metadata från Git inför varje bygge, inklusive dirty-status. API-systemendpoints är read-only. Serverinformation på klienten visas som Unknown tills anslutning implementeras. Ingen sensorinsamling eller simulerad mätdata visas.

## Ändrade filer

Ny solution och alla projektkällor/resurser; Directory.Build.props/targets, Directory.Packages.props, .editorconfig, .gitignore; AGENTS.md, iterations.json, ITERATIONS.md, README.md, ARCHITECTURE.md, docs/MASTERPLAN.md och denna rapport; tools/Generate-BuildInfo.ps1, Verify.ps1, Smoke-Test.ps1 samt GitHub Actions-workflow. Beräkningsmodulerna är förberedda projekt och implementerar ännu inga algoritmer.

## Utförda kontroller

- `dotnet build Sensor3.sln -p:Sensor3WindowsOnly=true`: godkänt, 0 fel, 0 varningar efter korrigeringar.
- MSTest: 7 godkända, 0 misslyckade, 0 överhoppade/ej körda automatiserade testfall.
- HTTP: API och dashboard startade; `/health` båda, `/api/system/build-info`, `/api/system/iterations` (18 poster) och `/about` svarade korrekt.
- Ett tidigt testvarv hade 3 fel eftersom resurser bäddades in för sent i MSBuild. Detta är korrigerat; slutvarvet har 0 fel.
- Första bygget av tomt Git-repo rapporterade hash/datum Unknown, inte fabricerad metadata. Efter commit kontrolleras hash och commitdatum mot Git genom Smoke-Test.ps1.

## Återstående manuella tester och begränsningar

Android-workload, Android SDK och Microsoft OpenJDK 17 är nu installerade. Android- och Windows-bygget är godkänt med 0 fel och 0 varningar. Ingen Android-enhet/emulator är ansluten; starttest på Android återstår. Windows-klienten kompilerar; navigation, About och layout kräver manuell native-kontroll. Dashboardens HTML är kontrollerad via HTTP, ingen visuell granskning är genomförd. Inga signerade APK/MSIX-releasepaket eller verkliga sensorresultat finns i denna iteration. GitHub Actions är konfigurerad, men dess körresultat måste kontrolleras separat.

Administrationsvy/auth och distribution införs i iteration 02. PostgreSQL/PostGIS, EF, SignalR, Channels, OpenTelemetry och beräkningsfunktioner tillkommer vid respektive funktion, enligt arkitekturen. iOS/MacCatalyst-målen och deras mallkod har tagits bort.

## Git och fortsatt arbete

Implementationscommit: se iterations.json efter första committen. Commitdatum kommer från Git, inte från rapportens datum. Aktuell build innehåller alltid exakt hash/datum för committen som byggs.

Nästa iteration kan genomföras på användarens instruktion. Iteration 02 påbörjas inte automatiskt. Iteration 01 får inte markeras Verified innan återstående native-acceptanskriterier har verifierats.
 
Implementationscommit: 53e3d6032114af7d9ec82ea584eb19682672f714. Commitdatum UTC: 2026-10-09T06:47:59.0000000+00:00.


## Kompletterande byggkontroll 2026-10-09

Kontroll genomförd i C:\Users\boris.gasic\source\repos\snhfhmw5tn-art\Sensor3 efter användarens godkännande av Android SDK-licenserna:

- Android-workload 36.1.43 och Android SDK installerade; Microsoft OpenJDK 17.0.14 installerat.
- ANDROID_HOME och JAVA_HOME registrerade som användarvariabler. Starta om Visual Studio för att läsa dem.
- `dotnet build Sensor3.sln`: Android och Windows godkända, 0 fel, 0 varningar.
- MSTest: 7 godkända, 0 misslyckade, 0 överhoppade.
- APK genererad lokalt i Sensor3.Mobile/bin/Debug/net10.0-android. Debugsignering är avsedd för utveckling; ingen release är publicerad.
- `adb devices`: ingen ansluten enhet. Status kvarstår Implemented, inte Verified.

Denna komplettering ersätter den tidigare uppgiften om att Android-byggstödet saknas.
