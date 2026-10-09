# Iteration 17 – Recording and simulation

Version 0.17.0/build 17. Implemented; fysisk verifiering återstår.

Contracts/Recording och Core/RecordingSession spelar in faktiskt levererade sensorer, status, GPS, WiFi och BLE. Start/stopp, sessionsnamn, monotonic mottagnings-offset, ursprungliga native-tider, startkalibrering, truck/carry-kontext, GPS-origin och radiokarta bevaras. Inspelning är uttrycklig och lokal i minnet, max 60 000 händelser/8 MB observationsbudget med Truncated-markering. Uppdateringsinstallation spärras under inspelningen. Ingen automatisk uppladdning eller beständig rådatainsamling. Stopp/export krävs för att spara. Start ska ske innan sensorer startas; OS-/sidlivscykeln stoppar native-samling som tidigare.

Simulation/RecordingCodec erbjuder validerad JSON och RFC4180-quotad Sensor3 CSV med full event-/ground-truth-payload, kulturstabila tal, metadata och tidslinje. Import högst 16 MB. Payload måste ha exakt en observation, giltiga sensorkällor/klockor/värden och sorterad 0–86400 s-tidslinje. Ingen importerad kod exekveras. SensorInputStream är en adapter för redan förvärvade data och kan inte starta hårdvara/webbläsarsensorer.

Core/AnalysisSession och ReplaySession använder samma StepSession/Fusion/HAR/Carry/Heading/PDR/Vehicle/Radio-kod som native. Replay har separat instans och kontrollerad UTC-klocka för radio/GPS-färskhet. Seek återskapar kedjan och spelar från början; inget dubbelräknat steg. Metadata återställer initiala referenser. Operatörskontext som ändras mitt under en inspelning behöver särskilda tidsmarkerade kontext-event; detta kontrolleras i slutlig integration.

RecordingView i native och InteractiveServer erbjuder JSON/CSV nedladdning, filimport, paus, 0.25/1/2/4/8× hastighet, tidslinje, explicit ground truth och LiveIndoorMap. Jämförelse: stegfel, sträckfel, vinkelinnovation, positionsfel, klassifikation med Unknown/startfönster inkluderade, första-steg-fördröjning, faktisk serialiserad JSON-storlek, beräkningsväggtid och processens uppmätta CPU-tid. Saknad referens ger Unknown. CPU-måttet kan inkludera annat arbete i processen och är ingen exklusiv algoritmprofil. Truckkurs är GPS-kurs, inte phone heading.

15 syntetiska scenarier: rak gång, arm, ficka, phone rotation, liten body turn, mjuk kurva, running, stilla, scanning, truck, truckvibration, GPS-gap, WiFi-brus, sensorgap och 200 ms simulerad nätfördröjning. Nätfördröjningen flyttar mottagning/tidslinje och behåller sourcetid; den är inte ett test av en fysisk nätverkslänk. Alla har SYNTHETIC-märkning och analytisk ground truth. Gait-fixturens horisontella amplitud är 0.8 m/s² (running 1.4), för att testa gait-fallet separat från modellens vertikala telefonlyft-grind. Ett tidigt fixturetest fångade att vertikalt dominerande rörelse avvisades; algoritmgrinden försvagades inte. Syntetisk gait ska inte tolkas som tränings-/fältdata.

207 tester passed efter codec, alla 15 scenarier genom hela kedjan, native tids-/ground-truth-rundresa genom JSON/CSV, quoted/multiline namn, seek/idempotens och saknad ground truth/ogiltig import. Android/Windows full build och API/browser-smoke före commit, artifacts/iteration-17-*. Realistiska oberoende inspelningar, carry-labelprecision, positionsnoggrannhet på lagerbanor, CPU/batteri på TC53 under långtid: NotRun.

Filer Contracts/Recording/Telemetry/Vehicle, Core/AnalysisSession/RecordingSession/SensorEventValidation/VehicleSession/RadioPositionSession, Simulation/*, SharedUI/RecordingView/Simulation/download.js/nav/projektreferenser, Dashboard Routes, native DI, RecordingTests/versionmetadata. Commit/datum i iterations.json. Ingen release. Nästa iteration 18 godkänd.

Slutresultat: Android/Windows 0 fel/varningar; 207 MSTest passed, 0 failed/skipped; API/dashboard-smoke passed.
