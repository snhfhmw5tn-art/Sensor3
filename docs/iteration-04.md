# Iteration 04 – Native sensorinsamling

Version 0.4.0, buildnummer 4. Status: **Implemented**, inte Verified eller Released. Iteration 05 har inte påbörjats.

## Funktioner och berörda filer

- `Sensor3.Contracts/Sensors.cs`: ISensorProvider, SensorDescriptor, SensorReading, SensorCapability, SensorStatus, SensorQuality samt frekvens, statistik och native behörighetskontrakt.
- `Sensor3.Sensors/SensorProvider.cs`, `SensorNormalization.cs`, projektfil: plattformsoberoende livscykel, SI-konvertering, datavalidering, faktisk frekvens, avbrott och återkomst. Sessionslåset från iteration 03 används under hela insamlingen. Sen registrering som misslyckas stänger redan öppnade källor; sena callbacks från avslutade sessioner ignoreras.
- `Sensor3.Mobile/Platforms/Android/AndroidSensorBackend.cs`, `AndroidSensorPermissions.cs`, `SensorPermissionRationaleActivity.cs`, `AndroidManifest.xml`: SensorManager/GetSensorList(TYPE_ALL), alla returnerade sensorer, kontinuerliga/ändringsstyrda sensorer och engångstriggers. Steg- och kroppssensorbehörighet begärs explicit för valda sensorer. Android 16 har separat hjärtfrekvensbehörighet och en native sida som förklarar användningen.
- `Sensor3.Mobile/Platforms/Windows/WindowsSensorBackend.cs`, `Package.appxmanifest`: Windows.Devices.Sensors med enhetsenumerering per tillgänglig sensorselector, absoluta och relativa orienteringsvarianter, altimeter och inventering av övriga Windows-sensorgränssnitt. Numeriska CustomSensor-kanaler behålls med sina ursprungliga namn. OS/driverfel redovisas separat från saknad hårdvara.
- `Sensor3.Mobile/MauiProgram.cs`, `App.xaml.cs`, projektfil: DI och stopp när appen lämnar förgrunden, tappar fokus eller stängs. Återstart sker explicit.
- `Sensor3.SharedUI/Pages/Sensors.razor`, `Pages/Home.razor`, `Layout/MainLayout.razor`: enkel native sida för inventering, val, behörighet, start/stopp, senaste värden, tidskälla, kvalitet och faktisk frekvens. Detta är grundinsamling; komplett diagnostik hör till iteration 05. Webbläsaren har ingen sensorprovider eller JavaScript-insamling.
- `Sensor3.Tests/SensorTests.cs`, testerprojektet, `BuildMetadataTests.cs`: testdubblar för algoritm/livscykel och metadata. Syntetiska testvärden visas aldrig som uppmätt hårdvarudata.
- `tools/Windows-Sensor-Probe/`: konsolprov som kompilerar och använder samma Windows-adapter som appen, utan testdubblar. `Smoke-Test.ps1` kontrollerar även webbgränsen; distributionsprovet följer version/build 0.4.0/4.
- `Directory.Build.props`, `Directory.Build.targets`, native versionsmanifest, `README.md`, `ARCHITECTURE.md`, `ITERATIONS.md`, `iterations.json`: version, byggmetadata och dokumentation.

## Mätvärden och begränsningar

50 Hz är standardbegäran för IMU. 1–200 Hz kan konfigureras; Windows-driverns minsta rapportintervall respekteras och Android får ett intervall i mikrosekunder. Händelsestyrda sensorer saknar periodisk garanti. Faktisk frekvens är `(antal − 1) / (sista − första native tidsstämpel)` och visas först efter två giltiga prov. Den omfattar eventuella pauser och är ingen påhittad OS-frekvens.

Android använder sensorernas monotona elapsed-realtime-nanosekunder. Windows använder sensorernas UTC-tidsstämplar, med ticks i frekvensberäkningen för att undvika overflow. Mottagningstid lagras separat. Duplicerade/bakåtgående tidsstämplar, ändrad tidsdomän, tomma kanaler och icke ändliga värden avvisas och räknas.

Acceleration normaliseras till m/s², rotation till rad/s eller rad, magnetfält till T, tryck till Pa och avstånd till m. Androids redan normaliserade acceleration/gyro behålls. Okända leverantörskanaler märks `native`; enheter gissas inte. Rotationens originalkanaler bevaras utan att saknade komponenter skapas. Referensramar bevaras; ingen koordinatfusion eller färdriktningsberäkning görs här. Kvalitet är OS-kvalitet när tillgänglig, annars Unknown.

Kontinuerliga källor utan nya giltiga prov i tre sekunder får Interrupted; återkommande prov ger Running. Ändringsstyrda sensorer och engångstriggers markeras inte avbrutna enbart för att inget förändras. En engångstrigger kräver en ny session för att aktiveras igen. Native behörighetsändringar, urkoppling och OEM-beteenden behöver också provas på verklig hårdvara.

Saknade sensorfunktioner visas som Unsupported. Alla sensorer som respektive native API returnerar inventeras; Windows-gränssnitt utan läsbar offentlig API-källa redovisas som oläsbara. Altimeter erbjuder bara GetDefault i Windows API. Ingen hårdvarufunktion ersätts med simulerade värden. Rådata visas lokalt, utan lagring eller serveröverföring.

## Kontroller och resultat

- Visual Studio 2026 MSBuild, hela solutionen inklusive Android och Windows Debug: godkänt, 0 fel och 0 varningar.
- MSTest: **91 passed, 0 failed, 0 skipped**. Upptäckt, saknade sensorer, enheter, frekvens, tidsstämplar, avbrott/återkomst, startfel, stopp, sena callbacks, nekad behörighet och sessionslås omfattas.
- HTTP: API/dashboard health, version 0.4.0, 18 iterationer, About, Git-metadata och browserns sensorgräns godkända. Distributions-/uppdateringsregression: **32 kontroller godkända** med verklig debug-signerad APK i tillfällig katalog, ingen release publicerad.
- Native Windows-inventering på denna dator: **0 Available, 15 Unsupported, 0 PermissionRequired, 0 Error**. Ingen IMU finns att mäta här. Detta verifierar upptäcktsvägen och är inget mätprov.
- Android APK: native behörigheter, rationale-activity och packageversion kontrollerade i byggmanifest/paket. Ingen Android-enhet/emulator är ansluten.

Loggar finns i ignorerade `artifacts/iteration-04-build.log`, `artifacts/test-results/iteration-04.trx` och `artifacts/iteration-04-windows-sensors.json`.

## Kvarvarande acceptanskriterier

Verklig insamling är **NotRun** på Android och Windows eftersom Android-enhet saknas och Windows-datorn saknar sensorer. Native sidans interaktion, behörighetsdialoger, enheter/tidsstämplar, faktisk frekvens vid flera begärda takter, fokus-/bakgrundsstopp, återkallad behörighet och fysisk sensorurkoppling behöver verifieras på hårdvara. Kontrollera också att uppdateringsinstallation spärras under insamling. Markera Verified först efter dokumenterade prov på båda plattformarna. Ingen release är publicerad.

Windows-provet körs med:

```powershell
dotnet run --project tools/Windows-Sensor-Probe/Windows-Sensor-Probe.csproj -- --measure-imu --output=artifacts/iteration-04-windows-sensors.json
```

Implementationscommit och UTC-datum registreras i iterations.json efter committen; BuildInfo visar exakt byggd HEAD. Nästa möjliga iteration är 05 – Komplett sensordiagnostik, efter användarens instruktion.
