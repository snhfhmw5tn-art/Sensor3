# Arkitektur

Sensor 3 är en modulär monolit. Sensor3.Contracts äger plattformsoberoende kontrakt. Sensor3.Core hanterar byggmetadata och validering. Beräkningsmoduler refererar Contracts och har inga UI/native-beroenden.

Sensor3.SharedUI är ett Razor-bibliotek som används av Sensor3.Mobile (MAUI Blazor Hybrid, Android/Windows) och Sensor3.Dashboard (Blazor Server). Båda registrerar sin egen assemblys BuildInfo genom DI. Sensor3.Api är ett separat ASP.NET Core-hostprojekt med hälsokontroll och read-only system-endpoints. Klient/serverkoppling och serverns versionsvisning införs i iteration 07; Unknown visas tills dess.

Sensors, StepDetection, ActivityRecognition, CarryingMode, SensorFusion, Positioning, MapMatching, Infrastructure, Distribution och Simulation är förberedda modulprojekt. Algoritmer, PostgreSQL/PostGIS/EF, SignalR, Channels och OpenTelemetry ansluts i de iterationer där funktionerna implementeras. Tomma modulprojekt är inte färdiga beräkningskomponenter.

Versionsmanifestet är versionshanterat. Inget publikt skriv-API för iterationsstatus finns. Implemented, Verified och Released är olika statusar; en release måste inte vara verifierad. Alla datum lagras i UTC och UI konverterar till Europe/Stockholm.

Installationsportal och autentiserad administratörsvy ingår i iteration 02. Iteration 01 har en read-only historik i About. Sensorinsamling sker endast native i senare iterationer.
