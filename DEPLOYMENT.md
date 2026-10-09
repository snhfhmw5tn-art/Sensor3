# Deployment och signerad release

## Utveckling

.NET10 SDK/MAUI Android+Windows, Android SDK/JDK och VS2026-komponenterna i `.vsconfig`. Native startprojekt Sensor3.Mobile. `tools/Smoke-Test.ps1` startar/stänger lokala Development API5301/Dashboard5302 och kontrollerar health/metadata/browser. `tools/Distribution-Smoke-Test.ps1` använder testkatalog, aldrig en publicerad produktionsrelease. Kör inte samtidig preview på samma portar under dessa skript.

Releaseadministration kräver befintligt konfigurerat konto/hash, rollen ReleaseAdministrator och CSRF. Utan konto är administration avstängd. `Distribution` konfigurerar gemensamt StorageRoot utanför webroot, signeringsverktyg, betrodda APK/MSIX-identiteter och manifestnyckel. Se existerande appsettings/Publish-Client.ps1 för exakta options; håll privata nycklar/credentials utanför repo och loggar.

## Native live-telemetry

Publicera Dashboard/API på en betrodd HTTPS-origin med korrekt certifikatkedja. Konfigurera `Telemetry:DeviceTokenHashes:<device-id>` med SHA256 av en befintlig stark enhetstoken, inte token i Git/appsettings. Ingen defaulttoken finns. Native genererar ett app-lokalt UUID, inte telefonens serienummer. Anslut native till dashboardens origin för dess livevy (separat API har separat registry). Stoppa insamling före ny anslutning, anslut sedan och starta valda sensorer. Token är minnesbaserad och autentiseras/revokationskontrolleras. Browser administratör väljer session och dess referenser. Ingen anonym route lämnar ut privata sensor/sessiondata. HTTPS krävs även för telemetry-GET.

SignalR endpoint `/hubs/sensors`; read-only admin endpoints `/api/telemetry/sessions`, `/{id}` och `/{id}/analysis`. Proxy måste stödja HTTPS/WebSockets; ingen generell trust av forwarded-proxyheaders eller certifikatbypass införs. Processrestart kräver ny native session. In-memory data är inte fler-nodsproduktion.

## Kontrollerad signerad publicering

Bygg från ren commit med monotoniskt buildnummer. `tools/Publish-Client.ps1` kräver befintliga externa signeringsnycklar/certifikat och explicita HTTPS-uppdateringsinställningar. Android: keystore+alias+passwordfile utanför repo. Windows: giltigt Code Signing-certifikat med privat nyckel, subject CN=Sensor3, betrott på målmaskinen, och riktiga HTTPS PackageUrl/AppInstallerUrl. Manifest signeras separat; native pinning måste använda rätt publika nyckel.

Skriptet skapar signerade APK/MSIX, checksums och klientmetadata; Windows AppInstaller använder verklig version/identitet/HTTPS-URL. Publicera genom releaseadministrationen först efter signatur-/identitets-/installationskontroll. Katalogen accepterar endast kontrollerade paket. Verifiera nedladdning och uppgradering på Android och Windows inklusive stopp under aktiv insamling. Återkalla release vid fel.

APK som VS Debug bygger med utvecklingscertifikat kan installeras lokalt med `adb install -r` men ska inte markeras Stable/Released. Windows unpackaged Debug.exe är ett utvecklingsmål, inte ett installerbart signerat MSIX. Inga produktionsnycklar eller HTTPS-release-URL har tillhandahållits i detta arbete; signerad produktion/publicering återstår. Den tomma publika katalogen är avsiktligt korrekt och får inte fyllas med exempelpaket för att verka färdig.

## Integritet

GPS, BSSID/BLE-ID och recordings kan vara känsliga. Insamling sker uttryckligen i förgrunden; export och HTTPS-anslutning är användarstyrda. Källidentifierare exponeras bara för den egna native-klienten eller auktoriserad admin. Lokal radiokarta finns i appdata; exporteras bara uttryckligen. Redigera/radera egna exportfiler enligt lokal datalagringspolicy. Git ignorerar artifacts, TestResults, recordings och privata nyckelformat. Ingen automatisk molnlagring eller offentlig telemetry införs.
