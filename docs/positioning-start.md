# Starta positionering och serverloggning

Klientens sida Position har Starta positionering och Stoppa positionering. Förvald HTTPS-origin: https://prototyp.qsys.se. Ange enhetstoken som är registrerad för det visade enhets-ID:t på servern. En redan ansluten telemetrisession återanvänds utan att kräva token igen.

Start inventerar enhetens sensorer, begär saknade behörigheter och inventerar på nytt. Alla Available-sensorer väljs; Unsupported, PermissionDenied och Error väljs inte. Positioneringskedjan konfigureras, serveranslutningen etableras och lokal inspelning startas innan sensorerna börjar leverera. Om serveranslutningen misslyckas börjar inga sensorer samla data. Sensorstartfel återställer sensorer, inspelning och anslutning.

Den inbyggda stegsensorn används fortfarande. IMU tillför riktning och aktivitetsanalys. Startpunkt och personriktning behöver bekräftas av operatören för en känd lokal position; startknappen hittar inte på koordinater. Radio/GPS har separata kontroller och behörigheter i diagnostiken.

Loggens händelseantal och serverns paket/byte/anslutningsstatus visas i klienten. Lokal inspelning är i minnet, begränsad till 60 000 händelser/8 MB. Efter stopp kan inspelningen öppnas och exporteras på Inspelning / simulering. Serverns live-session väljs av administratören under diagnostik. Serverns befintliga registry är i minnet, inte permanent lagring; omstart förlorar sessionerna. Anslut till dashboardens origin, eftersom en separat API-process har en separat registry.

Stoppa positionering, sidbyte eller appavbrott stoppar insamling, avslutar inspelningen och kopplar från telemetri. Återstart är manuell. Behörighetsnekade sensorer hindrar inte de andra tillgängliga sensorerna från att väljas. Delade diagnostikprenumerationer hindrar dubbelregistrering av prov.

Serverkontroll 2026-10-12: lokal namnuppslagning för prototyp.qsys.se pekar på 127.0.0.1 och ingen HTTPS-process svarar på port 443. DNS utan hosts-fil pekar via qsys-srv-nginx01.qsys.se på 146.66.235.11. HTTPS mot den DNS-adressen, med normal certifikatkontroll, svarar 404 på /health och /about och 405 på POST /hubs/sensors/negotiate?negotiateVersion=1. Sensor3-dashboardens routes behöver publiceras och enhetens token registreras innan fysisk telefonöverföring kan verifieras. Ingen hosts-fil, serverkonfiguration eller certifikattrust ändrades.

Validering: 225 MSTest-tester passerade, inklusive startordning före första provet, filtrering av otillgängliga sensorer, behörighetsbegäran, dubbelstart, stopp, serverfel och återanvändning av befintlig anslutning. Hela Android-/Windowslösningen byggd med 0 varningar och 0 fel. API-/dashboard-smoketest passerat. Ny debug-APK installerad på Zebra TC53; positionssidans båda knappar och förvald HTTPS-adress verifierade via UI. Start utan token gav det förväntade felet och behöll noll insamlade händelser/Disconnected. Live-överföring till prototyp.qsys.se är inte verifierad eftersom serverns routes/enhetstoken saknas.
