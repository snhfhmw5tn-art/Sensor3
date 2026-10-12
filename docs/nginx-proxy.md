# Sensor3 bakom Nginx Proxy Manager

504 betyder att proxyn inte fick svar från sin backend i tid. Vid felsökningen 2026-10-12 svarade den körande Dashboard-processen 200 på http://127.0.0.1:5302/health, men lyssnade bara på loopback. Sensor3-datorns LAN-IP var 10.0.23.80. Anrop till domänens publika DNS-adress fick inget svar inom 15 sekunder. Administrationssidan https://npm.qsys.se svarar med Nginx Proxy Managers inloggningssida. Proxyns faktiska IP mot backend är ännu inte identifierad.

Dashboard har nu den valbara utvecklingsprofilen Nginx LAN: localhost:5302 och 10.0.23.80:5302. Välj den som Dashboardens debugmål när proxyn ska ansluta över LAN. Den befintliga lokala http-profilen är kvar. Justera LAN-adressen om datorns adress ändras. Om Windows-brandväggen blockerar anslutningen behövs en regel begränsad till proxyns verkliga IP och port 5302; administratörsrättigheter krävs.

Proxy Host för prototyp.qsys.se ska använda backend Scheme http, Forward Hostname/IP 10.0.23.80, Forward Port 5302 och Websockets Support. Domänens HTTPS-certifikat och HTTPS för klienten används fortsatt. Backend pekar på Dashboard, eftersom dess registry används av dess livevy.

Servern stöder ReverseProxy:KnownProxies som en lista med exakta IP-adresser. Ange den verkliga adress som ansluter från Nginx till Kestrel, exempelvis via miljövariabeln ReverseProxy__KnownProxies__0 på Dashboard-processen. Konfigurationen är tom som standard och inga forwarded headers accepteras då. Bara angivna proxy-IP-adresser får ange X-Forwarded-Proto och X-Forwarded-For; wildcardadresser och nätverksintervall accepteras inte. Middleware kör före HTTPS-redirection och autentisering. Detta behövs när Nginx avslutar HTTPS och använder HTTP till backend. Se [ASP.NET Core proxykonfiguration](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).

Datorns hosts-fil har också en äldre rad som pekar prototyp.qsys.se mot 127.0.0.1 (kommentar Qsys Sensor 2). Windows-klienten använder därför den lokala datorn, där ingen HTTPS-lyssnare finns på port 443. När domänen ska gå via Nginx måste den gamla domänspecifika överskrivningen rättas av en administratör. Filen har inte ändrats automatiskt.

Token kontrolleras först när klienten når Sensor3. 504 är inte ett avvisat tokenvärde. En korrekt proxyanslutning ger 401 om enhetstoken saknas eller är fel; HTTPS-kravet och enhetsautentiseringen är kvar.

Validering: 231 MSTest-tester passerade, inklusive betrodd/okänd proxy, tom proxykonfiguration och avvisade wildcard/subnät/hostnamn. Testerna byggdes i separat artifacts-katalog för att låta användarens F5-processer fortsätta köra. Byggmetadata stöder nu även absoluta IntermediateOutputPath-värden från --artifacts-path; både isolerad byggning och Visual Studio MSBuilds vanliga relativa metadata-sökväg har kontrollerats.

Liveanslutningen genom Nginx är inte åtgärdad eller verifierad ännu. Inloggning på proxyadministrationssidan, faktisk proxy-IP, eventuell brandväggsregel och korrekt hosts-uppslagning behövs för nästa steg. Inga tokens, proxybehörigheter, brandväggsregler eller hosts-ändringar har skapats av detta arbete.
