# Lokal IIS för Sensor3

Dashboard publiceras med `dotnet publish Sensor3.Dashboard/Sensor3.Dashboard.csproj -c Release --artifacts-path artifacts/iis-build -o artifacts/iis-publish`.

Kör `tools/Install-Sensor3Iis.ps1` från Windows PowerShell som administratör. Skriptet skapar webbplats och separat app pool sensor3, utan managed CLR, och bindningen `http *:80:prototyp.qsys.se`. Publicerade filer kopieras till C:\inetpub\sensor3. App pool har läsrätt där och skrivrätt endast till distributionslagringen C:\ProgramData\Sensor3\distribution. Befintlig webbplats, pool, upptagen värdnamnsbindning eller icke-tom målkatalog stoppar installationen.

IIS och ASP.NET Core Module V2 finns på datorn. Publiceringen lyckades 2026-10-12 och genererade web.config för in-process hosting. Installationen kunde inte köras i Codex-sessionen eftersom IIS-konfigurationen kräver administratörsrättigheter. Webbplatsen är därför ännu inte skapad eller driftverifierad.

Kontrollera därefter http://prototyp.qsys.se/health lokalt. Den befintliga hosts-raden pekar domänen på localhost. För telefonen gäller fortfarande HTTPS: antingen behöver IIS en separat HTTPS-bindning med giltigt certifikat, eller Nginx måste vidarebefordra till http://10.0.23.80:80 med Host prototyp.qsys.se. Betrodd proxy-IP behöver konfigureras för HTTPS-headern. En HTTP-bindning ensam uppfyller inte klientens HTTPS-krav.

Visual Studios användarhemligheter följer inte med en Production-publicering. Administratörskonfiguration och enhetsregistrering måste tillföras IIS-installationen säkert innan autentiserad positionering kan verifieras. Inga hemligheter har kopierats automatiskt. Inga brandväggsregler eller befintliga IIS-webbplatser har ändrats.
