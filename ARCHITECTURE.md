# Arkitektur

Sensor 3 är en modulär monolit. Sensor3.Contracts äger plattformsoberoende kontrakt. Sensor3.Core hanterar byggmetadata och validering. Beräkningsmoduler refererar Contracts och har inga UI/native-beroenden.

Sensor3.SharedUI är ett Razor-bibliotek som används av Sensor3.Mobile (MAUI Blazor Hybrid, Android/Windows) och Sensor3.Dashboard (Blazor Server). Båda registrerar sin egen assemblys BuildInfo genom DI. Sensor3.Api är ett separat ASP.NET Core-hostprojekt med hälsokontroll och read-only system-endpoints. Klient/serverkoppling och serverns versionsvisning införs i iteration 07; Unknown visas tills dess.

Sensors, StepDetection, ActivityRecognition, CarryingMode, SensorFusion, Positioning, MapMatching, Infrastructure, Distribution och Simulation är förberedda modulprojekt. Algoritmer, PostgreSQL/PostGIS/EF, SignalR, Channels och OpenTelemetry ansluts i de iterationer där funktionerna implementeras. Tomma modulprojekt är inte färdiga beräkningskomponenter.

Versionsmanifestet är versionshanterat. Inget publikt skriv-API för iterationsstatus finns. Implemented, Verified och Released är olika statusar; en release måste inte vara verifierad. Alla datum lagras i UTC och UI konverterar till Europe/Stockholm.

Iteration 02 implementerar Sensor3.Distribution: releaseval, kompatibilitet, uppdateringspolicy, säker fillagring och HTTP-endpoints. Contracts innehåller releasekontrakten utan ASP.NET-/native-beroenden. Api och Dashboard registrerar samma distributionsmodul; Dashboard har statiskt serverrenderade /download och /admin/releases så vanliga HTML-formulär och cookieautentisering fungerar utan Blazor-circuit. Native-klientens delade router är oförändrad. Administration är avstängd utan konfigurerat konto; alla ändringar kräver administratörsroll och CSRF-token.

Api och Dashboard använder samma konfigurerade lokala StorageRoot. Paket lagras utanför webroot under servergenererade GUID-namn och exekveras/extraheras aldrig. Katalogskrivning sker genom atomisk ersättning med exklusivt fillås mellan processer. Endast APK/MSIX med plattformsmanifest godtas; storlek och SHA-256 kontrolleras vid publicering och nedladdning. Återkallelse spärrar nya nedladdningar, inte överföringar som redan startat. PostgreSQL eller objektlagring kan ersätta filkatalogen vid framtida produktionsskalning; nätverksfilsystem/multipla servernoder har inte verifierats.

Paketens kryptografiska signaturer och identitet/metadata i installationspaketen verifieras inte av denna modul ännu. Administratören ansvarar för kontroll före publicering; signerad paketering och uppgraderingar införs i iteration 03. Ingen faktisk release är publicerad. Iteration 01 har en read-only historik i About. Sensorinsamling sker endast native i senare iterationer.
