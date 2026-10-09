# Arkitektur

17 projekt, Android/Windows utan iOS. Contracts äger alla plattformsoberoende DTO/interface. StepDetection, SensorFusion, ActivityRecognition, CarryingMode, Positioning och MapMatching är rena .NET-beräkningsbibliotek utan UI/native-beroenden. Core komponerar kedjan, versionsmetadata, inspelning och säker uppdateringsklient. Simulation använder samma Core-kedja med en inmatningsadapter och kontrollerad replay-klocka. SharedUI används av MAUI och Blazor Server; ingen ISensorProvider registreras i dashboarden.

## Dataflöde

Native backend → SensorProvider (normalisering/livscykel) → rådiagnostik, StepSession, explicit RecordingSession och HTTPS-telemetrikö. StepSession: tidsalignerad fusion → world-frame gait/features → HAR → carry → human heading → accepterade steg → PDR. Human heading är separat från telefonpose. Gait är spärrad i deklarerat truckläge. NativeObservationBus förmedlar faktiskt hämtade GPS/WiFi/BLE till separat truckmodell, radiofingerprints, inspelning och telemetri. Radio korrigerar XY utan att skapa gångsträcka; råspår behålls.

SignalR över HTTPS kräver konfigurerad enhetstokenhash och matchande enhets-ID. Channels-köer är begränsade, callback skriver utan väntan, klient återförsöker samma batch/sekvens tills kvittens. Klienthändelser begränsas till 256 KiB, batch till 400 KiB observationsbudget/256 event; server validerar ≤512 KiB. Förluster rapporteras explicit, saknad rapport är Unknown. RTT mäts med monotonic Stopwatch, ingen falsk envägslatens härleds från osynkroniserade UTC-klockor.

Infrastructure/TelemetryRegistry äger per-session SensorDiagnosticsStore + AnalysisSession. Full paketvalidering föregår applicering. Dedup/hashkonflikt och enhetsägarskap hindrar dubbelräkning/korskoppling. Varje session har egna fusion-/classifier-/PDR-/radio-/trucktillstånd. Source-adaptern tar emot förvärvade data; den startar inte browser-sensorer. Radio/reference-konfiguration för server-session är separat från native-lokal karta. Sessionsminne max64/max4 per device, 15 min inaktivitet städas vid ny registrering; processrestart kräver ny session och räknare är inte beständiga.

Raw sensor-tidsstämplar bevaras: Android elapsed realtime ns och Windows UTC. SI-normalisering ändrar inte raw koordinatram. Fusion hanterar Windows motsatta g-force-tecken och tidsalignering; fysisk geografisk ramkalibrering återstår. Unknown/saknad gyro redovisas som begränsat underlag, inte mätt noll. Beräkningsosäkerheter är heuristiska, inte statistiskt kalibrerade.

## Livscykel och säkerhet

Native samling stoppas vid sidbyte/bakgrund/fokusförlust. Sena callbacks skyddas med generationstoken. Diagnostikgraf är begränsad och UI uppdateras med timer som avslutas vid disposal; IServiceProvider slås inte upp genom properties efter disposal. UpdateSessionGuard spärrar install medan sensor/radio/inspelning pågår. Recordings är explicit lokal minnesinsamling, 60k event/8MB observationsbudget; Truncated stoppar insamling och frigör uppdateringslåset. Operator-context-event bevarar heading/start/carry/truck/GPS-origin under replay. Radiokartan fångas initialt; ändringar av radiokarta mitt i inspelning är ännu inte tidsmarkerade.

Distribution är separat från implemented-version. Cookie-administration med roll och CSRF; sensormottagning med enhetscredential och revokationskontroll. Paket lagras utanför webroot under GUID, katalogskrivning är atomisk/låst mellan API/dashboard. Endast verifierade APK/MSIX-signaturer/appidentitet/version/hash godtas. Native verifierar signerat manifest, HTTPS, nonce, utgångstid och paket; inga TLS-bypass eller defaulttokens finns. Serverkonfiguration och signeringsnycklar hålls utanför Git. Produktion kräver separata nycklar/endpoint/installationsbevis.

PostgreSQL/PostGIS, EF och OpenTelemetry är inte aktiva beroenden. File-backed releasekatalog och in-memory telemetry är den implementerade arkitekturen. Se DEPLOYMENT/KNOWN_LIMITATIONS för drift och datalagring.
