# Iteration 05 – Komplett sensordiagnostik

Version **0.5.0**, buildnummer 5. Status **Implemented**, inte Verified eller Released. Iteration 06 har inte påbörjats.

## Implementerat

`/diagnostics` och det tidigare `/sensors` öppnar samma delade diagnostikvy i MAUI Android/Windows och Blazor. Den visar hela native-katalogen, inklusive flera fysiska sensorer av samma typ och uttryckliga Unsupported-funktioner. Varje sensor har namn, beskrivning, tillverkare eller Unknown, native ID, tillgänglighet, behörighet, aktivitet, beräkningsanvändning, kanaler/enheter, begärd och faktisk frekvens, tidskälla, native tidsstämpel, mottagningstid, kvalitet, ålder och fel. X/Y/Z visas där källan har dessa kanaler.

Diagnostikstatus skiljer **Available, Unsupported, PermissionRequired, PermissionDenied, Initializing, Active, Inactive, Stale och Error**. Initializing betyder startad utan giltigt prov; Active kräver mottagen data. Kontinuerliga sensorer blir Stale efter tre sekunder utan prov, även innan första provet. Ändringsstyrda sensorer/engångstriggers får inte Stale enbart av tystnad. Stopp behåller senaste värden som Inactive med ålder; sena värden får inte återaktivera dem. Återkommande giltiga prov återställer Active. Fel och nekad behörighet redovisas separat.

Android registrerar uttrycklig nekad behörighet efter en begäran i appen. OS-behörigheten kontrolleras alltid vid ny inventering och har företräde om den beviljats. Senaste nekandet sparas endast under appens livstid; efter omstart visas ett ej beviljat tillstånd som PermissionRequired tills ett svar är känt. Windows läser tillverkare från `System.Devices.Manufacturer` när den finns, annars Unknown. Ett generiskt drivrutinsnamn utges inte för tillverkare.

MAUI tillåter explicit inventering, val, behörighetsbegäran och start/stopp. Förgrunds-/fokusstopp och uppdateringslås från iteration 04 behålls. Inga sensorer används av en positioneringsalgoritm ännu; vyn visar detta uttryckligen.

## Rådata, filter och grafer

Rådata betyder de native-värden som iteration 04 normaliserar till standardenheter; filtreringen ändrar aldrig dessa värden. Ett separat diagnostiskt förstagradslågpass beräknas för kontinuerlig acceleration, gravitation, linjär acceleration, gyro, magnetfält, tryck, ljus och relativ höjd med kända enheter:

`alpha = 1 − exp(−delta_t / tau)`

`filtered = (1 − alpha) * previous_filtered + alpha * raw`

`delta_t` kommer från sensorns egna tidsstämplar, inte UI-uppdateringen. Standard `tau = 0.25 s`. Första provet initierar filtret från råvärdet; ny session, ändrad kanal/enhet eller en native tidspaus över stale-gränsen återinitierar. Ett enskilt prov kan därför ha identiska råa och filtrerade värden. Detta är utjämning för diagnostik, inte sensorfusion, en osäkerhetsmodell eller en positionsberäkning. Den introducerar fördröjning och kan dämpa verkliga rörelser; den uppskattar inte sensorbias eller kalibreringsfel. Kvaternioner, vinklar, kategorier, steg och okända enheter filtreras inte. Native OS-kvalitet bevaras och härleds inte från filtret.

SVG-grafer visar en kurva per kanal med rådata i blått och filtrerat i orange/streckat, tydliga enheter och egen skala. Grafhistorik är högst 180 punkter per sensor, med minst 100 ms mellan punkter. UI uppdateras högst fem gånger per sekund. Alla giltiga prov räknas oberoende av grafens reducerade takt. Historik, sensorer (högst 256) och kanaler (högst 64 per prov) är begränsade; inga växande köer eller inspelningsfiler skapas. Stoppade/stale värden markeras och deras ålder visas.

Tidsgräns, filtertidskonstant, grafintervall och historiklängd finns i validerade `SensorDiagnosticsOptions`; vyn visar de faktiska inställningarna. Olika referensramar har inte roterats till en gemensam ram.

## Dashboard och klientmetadata

`ISensorDiagnosticsReceiver` tar emot en identifierad Android- eller Windows-klient, katalog, status och mätvärden från en betrodd native-adapter. Mottagaren kontrollerar native tidsdomän, katalog-ID, kanaler, ändliga värden och tidsordning. Klientens BuildInfo visas i diagnostiken separat från dashboardens egen version. Dashboardens lager är scoped per Blazor-circuit; klientdata blandas inte med andra användarsessioner.

Dashboarden registrerar ingen sensorprovider och använder inga JavaScript-rörelsesensorer. Utan mottagna native-data visas Unknown för klientversion/iteration/commit och inga mätvärden eller grafer. **Nätverkstransport och autentiserad klientanslutning tillkommer i iteration 07**; iteration 05 inför vyn och mottagarkontraktet, inga öppna sensoruppladdningsendpoints. Testfixturer injiceras enbart i tester och exponeras inte i app/portal.

## Ändrade filer

- `Sensor3.Contracts/SensorDiagnostics.cs`, `Sensors.cs`: diagnostikkontrakt, nio statusar, behörighet, klientmetadata, alternativ och PermissionDenied för native källor.
- `Sensor3.Sensors/SensorDiagnosticsStore.cs`: status/timeout, begränsad historik, filter, data- och identitetsvalidering, snapshot-isolering.
- `Sensor3.SharedUI/Components/SensorDiagnosticsView.razor`, `SensorChannelGraph.razor` med CSS, `Pages/Sensors.razor`, `Home.razor`, `Layout/MainLayout.razor`, projektfil: delad vy, kanaltabeller, grafer, kontrollflöde och navigering.
- `Sensor3.Dashboard/Program.cs`, `Components/Routes.razor`: circuit-scoped mottagarlager och interaktiv serverrendering för diagnostik.
- `Sensor3.Mobile/MauiProgram.cs`, Android-behörighets- och backendfiler, Windows-backend, `wwwroot/index.html`: DI, nekade behörigheter, Windows-tillverkare och svenskt HTML-språk.
- `Sensor3.Tests/SensorDiagnosticsTests.cs`, `SensorDiagnosticsRenderingTests.cs`, projektfil, `BuildMetadataTests.cs`: status/timeout/filter och verklig Razor-rendering med uttryckliga testfixturer.
- Versions-/byggmanifest, `tools/Smoke-Test.ps1`, `Distribution-Smoke-Test.ps1`, README, arkitektur, iterationshistorik och `iterations.json`: version/build 0.5.0/5, regression och resultat.

## Kontroller och resultat

- Hela solutionen via Visual Studio 2026 MSBuild för Android och Windows Debug: **0 fel, 0 varningar** i slutbygget.
- MSTest: **116 passed, 0 failed, 0 skipped**. 25 nya fall täcker alla statusar, initializing/stale/återkomst, eventbaserad tystnad, stopp, nekad behörighet, native tidsdomäner, UTC/frekvens, rådata/filter, reset, begränsad historik, invalid data, snapshot-isolering, klientmetadata och Razor-rendering av kanaler/grafer.
- Ett renderingstest upptäckte en felaktig stringbindning till grafkanaler. Bindningen korrigerades och renderingstestet passerar med riktiga SVG-kurvor från testdata. Ett inledande bygge blockerades av en dashboardprocess startad från Visual Studio; den lokala processen stoppades och slutbygget passerade.
- HTTP: health, About, 18 iterationer, version 0.5.0, Git-metadata samt `/sensors` och `/diagnostics` utan native-kontroller eller fabricerade grafer godkända. **32 distributions-/säkerhetskontroller** godkända med debug-signerad APK i tillfällig katalog.
- Native Windows-app startad och diagnostiklänken öppnad med computer-use. Vyn visar version 0.5.0/build 5, native metadata och **15 Unsupported** sensorfunktioner. Starta/Stoppa är inaktiverade när ingen tillgänglig sensor är vald. Inga falska mätvärden visas.
- Native Windows-inventering: inga tillgängliga sensorer på denna dator. Android-enhet/emulator saknas. Verkliga sensorkurvor, native stale-återkomst, filtervärden och Androids grant/deny-dialoger är **NotRun** på hårdvara.

Slutbygglogg: `artifacts/iteration-05-build.log`. Tester: `artifacts/test-results/iteration-05.trx`. Dessa artefakter är ignorerade av Git.

## Återstående verifiering

Installera/starta på Android och Windows med sensorer. Inventera samtliga, neka/bevilja relevant behörighet och kontrollera korrekta statusar. Välj IMU och andra sensorer, verifiera kanalernas enheter, tidsstämplar, native kvalitet och faktisk takt vid minst två begärda frekvenser. Kontrollera grafer mot rörelse och filterutjämning mot rådata; utjämning får inte ersätta råvärden. Prova inga prov före start, avbrott, återkomst, fokus-/bakgrundsstopp, nya sessioner och fel. Granska läsbarhet i Android och Windows vid små fönster. Markera Verified först efter dokumenterade hårdvaruprov; ingen release publiceras av denna iteration.

Implementationscommit med UTC-datum registreras efter godkänt iterationsbygge; BuildInfo visar exakt byggd HEAD. Nästa möjliga iteration är **06 – GPS, WiFi och Bluetooth**, efter användarens instruktion.

## Korrigering efter iterationens bygge

Diagnostikvyn slog tidigare upp ISensorProvider vid varje åtkomst, även i DisposeAsync. Om WebView/Blazor-circuit stängde sin tjänstecontainer först kunde uppslaget kasta ObjectDisposedException. Native-tjänster hämtas nu en gång vid initiering; avslut är idempotent, timerjobb respekterar avslut och en redan stängd native provider hanteras vid avregistrering. Inga nya timerjobb skapas om vyn avslutats under inventering.

Regressionstestet stänger tjänstecontainern före HTML-renderern/komponenten och verifierar att avslut fungerar. Uppdaterad testsuite: 117 passed, 0 failed, 0 skipped. Android/Windows-bygge verifieras även efter korrigeringen; logg artifacts/diagnostics-disposal-build.log och tester artifacts/test-results/diagnostics-disposal.trx.