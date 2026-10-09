# Sensor 3

.NET 10 / C# 14. Native MAUI Blazor Hybrid för Android och Windows, ASP.NET Core API och Blazor Server-dashboard. Version **0.18.0 / build 18**. Implementerade iterationer 1–18; fysisk verifiering och signerad produktionsrelease är separata steg. Se [VALIDATION.md](VALIDATION.md), [iterationsrapporter](ITERATIONS.md) och [kända begränsningar](KNOWN_LIMITATIONS.md).

## Bygg och kör

Visual Studio 2026 Professional med komponenterna i `.vsconfig`, MSVC x64/x86, .NET 10 och Android/MAUI/Windows-verktyg. Android SDK/JDK hittas i `%LOCALAPPDATA%\Android\Sdk` och `%LOCALAPPDATA%\Microsoft\Jdk` om ingen explicit sökväg finns. Välj **Sensor3.Mobile** som startprojekt; beräkningsbibliotek kan inte startas direkt. Välj Android-enheten eller Windows Machine som mål.

```powershell
dotnet build Sensor3.sln
dotnet test Sensor3.Tests/Sensor3.Tests.csproj
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project Sensor3.Dashboard --urls http://localhost:5302
dotnet run --project Sensor3.Api --urls http://localhost:5301
```

Visual Studios egen MSBuild har också använts för hela Android-/Windows-lösningen. CLI-workloads ersätter inte IDE-komponenter. Browser kan visa diagnostik, karta och spela importerade/syntetiska data; den samlar inga rörelsesensorer.

## Arbetsflöden

- `/diagnostics`: native inventering, explicit sensorval/start/stopp, rå-/filtrerad kanaldiagnostik, HAR/carry, person-/telefonriktning och radioförgrundsprov. Appens bakgrund/fokusförlust eller sidbyte stoppar native-insamlingen.
- `/navigation`: explicit lokal XY-start, samma insamlingskontroller och livekarta. Bekräfta personens heading och börja med rak gång. Metergradering, zoom/pan/follow, rå-/radiokorrigerat-/kartmatchat spår, geometriimport och frivillig kalibrerad Google-vy.
- `/simulation`: starta lokal inspelning **före** native-insamlingen. Bekräfta kända startreferenser, starta sensorer på samma sida, stoppa och exportera JSON/CSV. Import, tidslinje, paus/hastighet, separat ground truth och 15 tydligt märkta syntetiska scenarier. Data sparas bara genom uttrycklig export.
- Truckläge är en operatörsuppgift. GPS-origin + färsk tillförlitlig fix krävs. Trucksträcka är separat; handacceleration integreras inte till trucktranslation.
- WiFi/BLE: registrera uppmätta lokala punkter/identifierare i samma XY-ram. Radiokarta lagras i appdata och kan importeras/exporteras. RSSI är inte exakt avstånd.
- Telemetri: stoppa insamlingen, anslut till en betrodd HTTPS-server med enhetstoken, starta sedan valda sensorer. Administratör väljer session i dashboarden; sessionerna får separata beräkningskedjor. Referenser kan sättas per server-session. API- och Dashboard-processer har varsin registry; anslut till dashboardens HTTPS-origin för dess livevy.

## Distribution och metadata

`/download` visar bara verkliga kontrollerade releasepaket. Inga signerade produktionspaket har publicerats. Debug-APK är ett lokalt testpaket. Signerade APK/MSIX kräver befintliga externa nycklar/certifikat och HTTPS-distribution enligt [DEPLOYMENT.md](DEPLOYMENT.md).

BuildInfo bäddas in med faktisk HEAD, commitdatum, separat byggdatum, dirty-status, plattform och implemented/verified/published. Bygg från en ren commit för spårbara testpaket. `iterations.json` lagrar implementationscommitten i en separat metadatacommit eftersom en commit inte kan innehålla sin egen hash. Version/build är inte bevis på Verified eller Released.

Se [ARCHITECTURE.md](ARCHITECTURE.md), [SENSORS.md](SENSORS.md), [ALGORITHMS.md](ALGORITHMS.md), [RESEARCH.md](RESEARCH.md), [masterplan](docs/MASTERPLAN.md) och rapporterna i `docs/iteration-01.md`–`18.md`.
