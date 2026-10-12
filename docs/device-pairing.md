# QR-registrering

Dashboardens meny Registrera enhet · QR öppnar `/devices`. Logga in med Sensor3s releaseadministratör (inte Nginx-kontot). Skapa QR-kod, skanna med Android-telefonens systemkamera, öppna HTTPS-länken och välj Öppna Sensor3. I klientens Position-vy visas registreringslänken och servern. Bekräfta med Registrera enhet på denna server. Därefter används token automatiskt med Starta positionering. Windows-klienten stöder att registreringslänken klistras in; Windows-protokollaktivering ingår inte.

Koden är slumpmässig (256 bitar), gäller fem minuter och kan lösas in en gång. URL-fragmentet håller koden utanför vanliga HTTP-accessloggar. Utgivning kräver ReleaseAdministrator, HTTPS och antiforgery-skydd. Redemption kräver HTTPS. Koder försvinner vid serveromstart; skapa då en ny. Serverns beständiga register innehåller endast SHA-256-tokenhashar. Registrering ersätter aldrig befintliga enhetsuppgifter; befintliga Telemetry:DeviceTokenHashes har fortsatt företräde. Klienten sparar server/token i MAUI SecureStorage, knutet till det lokala enhets-ID:t, och skickar inte sparad token till en annan serveradress. Automatiska HTTP-redirects vid registrering är avstängda.

Pairing:PublicServer anger extern HTTPS-server (standard https://prototyp.qsys.se). Pairing:StorageDirectory ska vara en beständig katalog utanför wwwroot med skrivrätt för den egna programpoolen. Vid IIS-uppdateringen användes artifacts/iis-data/pairing, och endast IIS AppPool/sensor3 fick Modify där. IIS:s befintliga Production-inställningar bevarades.

Lokal kontokonfiguration för första administratören:

```powershell
dotnet run --project tools/ConfigureSensor3Admin -- "C:\Users\boris.gasic\source\repos\snhfhmw5tn-art\Sensor3\artifacts\iis-publish\appsettings.Production.json"
```

Verktyget frågar interaktivt efter användarnamn och ett dolt lösenord på minst 16 tecken, sparar bara PBKDF2-hash, ersätter inte befintliga konton och laddar om IIS-applikationen. Inga lösenord ska skickas i chatten. Förkomna lokala token eller återställningar kräver administrativ hantering av den registrerade hashposten; detta flöde skriver inte över befintlig registrering automatiskt.

2026-10-12: Dashboard publicerad i lokal IIS. Hälsokontroll via publik HTTPS svarar 200; SignalR negotiate utan token svarar 401. API/SignalR-fel re-exekveras inte längre som HTML-felsidor, vilket rättar tidigare 500 från antiforgery på POST. Android och Windows byggde utan fel/varningar. 239 tester passerade, inklusive engångsanvändning, samtidiga anrop, utgång, hashpersistens, länkar och faktisk HTTPS-redemption följd av SignalR-autentisering. Ingen fysisk QR-skanning är verifierad ännu. Android-installation nekades med INSTALL_FAILED_UPDATE_INCOMPATIBLE: befintlig app har annan signeringsnyckel. Appen avinstallerades inte och data raderades inte. Ny APK behöver signeras med samma nyckel som den installerade appen, eller användaren behöver ta ställning till en separat ominstallation.

Windows hosts-filens gamla domänrad pekar fortfarande prototyp.qsys.se på 127.0.0.1. IIS har HTTP, inte HTTPS, lokalt. En administratör behöver ta bort just den gamla domänöverskrivningen för att datorns webbläsare/Windows-klient ska använda publik HTTPS via befintlig proxy. Telefonens externa väg når nu IIS. Administratörskontot och fysisk klientregistrering återstår att verifiera.
