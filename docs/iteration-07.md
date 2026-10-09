# Iteration 07 – Realtime sensor communication

Version 0.7.0/build 7. Status Implemented; nästa iteration 08 är redan godkänd.

Contracts/Telemetry.cs, Sensors/SensorTelemetryClient.cs och Infrastructure/TelemetryRegistry.cs/TelemetryHosting.cs inför tidsstämplade råprover, enhets-ID, session, sekvens, HTTPS SignalR och Web API. Native-klienten skickar Research 100 ms eller Production 250 ms; varje råprov behåller sin native tidsupplösning. Tomma hjärtslag anger anslutning utan att tillverka mätvärden. Klientkön är 4096 händelser, serverkön 64 paket; servern väntar vid belastning och native-klienten räknar förlorade händelser när dess kö är full. Ett ej kvitterat paket återförs med samma sekvens efter återanslutning. Servern deduplicerar inom processlivslängden och avvisar konflikter med samma senaste sekvens. Återanslutning behåller katalog, version och session; fördröjt disconnect från gammal anslutning stör inte den nya.

API/Dashboard registrerar samma värdtjänster. Sensorhistorik begränsas av befintlig diagnostikstore. Högst 64 sessioner/4 per enhet; gamla sessioner rensas efter 15 min inaktivitet vid nyregistrering. Native lifecycle och komponentdisposal stoppar överföringen. Webbläsaren mäter inga rörelsesensorer. RemoteDiagnostics visar administratörens valda native-enhet, version, commit, rådata, grafer, kvalitet och frekvens. TelemetryControls visar serverversion, paket/JSON-byte, tappade händelser och faktiskt uppmätt tur-retur-latens. UTC-klockskillnad används inte som påstådd nätverkslatens.

## Konfiguration och säkerhet

Välj en HTTPS-origin i native-appen. Kopiera dess pseudonyma enhets-ID och konfigurera `Telemetry:DeviceTokenHashes:<id>` med SHA-256-hex av en separat lång slumpmässig token via user-secrets eller skyddad serverkonfiguration. Ange token manuellt i appen; den sparas inte i repo, loggar eller browserquery. Certifikatvalidering används oförändrad i produktionsklienten. Lokal datorns localhost-certifikat fungerar inte automatiskt från telefonen; använd certifikat med korrekt DNS-namn och betrodd kedja. Lokal TLS-testning använder endast befintligt devcert och exakt certifikatpinning i testkod. Tom credentials-konfiguration nekar alla enheter.

Servern binder autentiserad identitet till sessionen. Alla telemetrirutter kräver HTTPS, och läs-API kräver ReleaseAdministrator. Revocation kontrolleras även vid Register/Send och avslutar befintlig anslutning. Native-klientmetadata är klientrapporterad, inte serverns fältverifiering. API och Dashboard är separata processer med separata register; anslut native-klienten till Dashboard-hostens HTTPS-origin för denna livevy. Persistenta sessionsköer/kvittens över serveromstart införs inte; nya sessioner behövs efter omstart. Historik är processminne och lagras inte på disk.

## Bygge och tester

- Android + Windows Debug med Visual Studio MSBuild: godkänt, 0 fel/varningar.
- MSTest: 132 passed, 0 failed, 0 skipped. Nya fall: återföring, konflikt, identitetsisolering, sekvensluckor, fördröjd disconnect, 8 parallella enheter × 100 prov, hjärtslag, cancellable backpressure och verklig loopback HTTPS/SignalR med nekad token, reconnect/deduplicering samt revocation.
- HTTP smoke och distributionskontroller körs före commit; resultat i artifacts.
- Fysisk TC53→HTTPS-serveröverföring, radio/data under nätverksbortfall och Windows native-visning är NotRun. Lokal loopback är inte internet- eller mobilnätverksvalidering. Ingen release publicerad.

Commit och datum: iterations.json, registrerat efter godkänt iterationsbygge. Primärkällor: [SignalR .NET-klient](https://learn.microsoft.com/en-us/aspnet/core/signalr/dotnet-client), [SignalR-behörigheter](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz), [bounded Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels).
