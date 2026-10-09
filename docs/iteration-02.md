# Iteration 02 – Webbportal för installation och versionshantering

2026-10-09. Applikationsversion 0.2.0. **Implemented**, inte Verified eller Released. Iteration 03 har inte påbörjats.

## Implementerat

- Dashboardens `/download`: Android/Windows, automatisk plattformsdetektion när user agent medger det, manuellt val och Development/Beta/Stable. Visar version/build, releasekanal, faktisk publiceringstid, release notes, filstorlek, SHA-256, iteration, Git-commit/commitdatum och serverkompatibilitet. Tider visas i svensk lokal tid; lagring är UTC. Ingen påhittad release visas när katalogen är tom.
- Kontrakten ApplicationRelease, ReleaseArtifact, ReleaseChannel, ReleaseManifest, ApplicationVersion, ClientCompatibility och UpdatePolicy. Versionsval använder numerisk semantisk precedence med prerelease, sedan buildnummer. Inga automatiska nedgraderingar; obligatoriska mellanversioner bibehåller uppdateringskravet. Återkallelse och avsaknad av kompatibel release redovisas.
- GET `/api/releases`, `/api/releases/latest`, `/api/updates/check`, `/api/releases/{id}/download` på API och dashboard. Befintliga system-endpoints bibehålls. Senaste release och update-check väljer kompatibilitet mot hostens verkliga version, inte en klientstyrd serverversion.
- `/admin/releases`: cookieautentisering, administratörsroll, publicering och återkallelse. Alla ändringar och login kräver CSRF-token. HttpOnly/SameSite Strict, 30 minuters session, HTTPS i produktion, fem loginförsök per IP/minut, ingen standardinloggning. PBKDF2-SHA256 med 210 000 iterationer, slumpmässigt salt och konstanttidsjämförelse. Konto konfigureras genom tools/Configure-DistributionAdmin.ps1 i Development eller hemlighetshanterare i produktion.
- Lagring utanför webroot. Servergenererade GUID-filnamn; inga filer exekveras/extraheras. Plattformens .apk/.msix, ZIP-struktur och manifest, maxstorlek samt SHA-256 valideras. API saknar möjlighet att tillföra godtyckliga filsökvägar. Katalogen uppdateras atomiskt med exklusivt lås mellan hostprocesser. Avbruten eller felaktig publicering lämnar inget publicerat delpaket. Korrupt nedladdning ger 503; återkallat/okänt paket ger 404. En påbörjad överföring avbryts inte av senare återkallelse.

## Ändrade filer

Releasekontrakten i Sensor3.Contracts/Releases.cs; distributionsmodulens AdminCredentials.cs, DistributionHosting.cs, ReleaseSelection.cs och ReleaseStore.cs; API-/dashboard-hostar och projektreferenser; dashboardens statiska router, PortalLayout, Download/AdminReleases, distribution.js/css; MSTest DistributionTests.cs; Configure-DistributionAdmin.ps1 och Distribution-Smoke-Test.ps1; Verify/Smoke-Test; versionsinställningar i Directory.Build.props/Mobile; README, ARCHITECTURE, ITERATIONS och iterations.json. Native-klienten får version 0.2.0/versionCode 2; inga sensor- eller uppgraderingsfunktioner införs här.

## Utförda kontroller

- Hela Sensor3.sln byggdes med Visual Studios 64-bitars MSBuild, Debug/restore för Android och Windows: avslutningskod 0, inga rapporterade fel eller varningar. Lokal logg: artifacts/iteration-02-build.log.
- **37 MSTest-fall godkända, 0 misslyckade, 0 överhoppade.** Omfattar tidigare metadata samt versionssortering, prerelease, kanal/plattform/serverkompatibilitet, obligatorisk uppdatering, återkallelse, monotona builds, filtyper, traversal, storlek, checksumma, paketstruktur, persistent katalog, korruption, avbrott, samtidighet och lösenordshashning. Lokal TRX: artifacts/test-results/iteration-02-tests.trx.
- **26 HTTP-/säkerhetskontroller godkända**: publik portal och API, saknad release, ogiltig plattform, nekad anonym administration, CSRF, felaktigt lösenord, login, publicering, delad katalog mellan hostar, metadata/SHA-256 på portalen, nedladdning, korrupt fil, obligatorisk uppdatering, duplicerad release, exekverbar fil, återkallelse, logout och login-rate-limit. Endast tydligt märkta syntetiska, ej installerbara paket och tillfälliga konton/kataloger användes; dessa togs bort efter testet. Den riktiga katalogen fick inga releaser.
- Ordinarie API/dashboard-smoketest passerade med korrekt Git-metadata, health, About och samtliga 18 iterationer.
- Faktisk webbläsarkontroll: Windows identifierades automatiskt; manuellt byte till Android/Beta fungerade och rätt installationsinstruktioner visades. Layout granskad vid 390 px bredd. Anonym administration gick till login med avstängt konto enligt standardkonfigurationen. Mobil bredd är layoutkontroll, inte test på fysisk Android-hårdvara.

## Drift och begränsningar

Api och Dashboard behöver samma lokala Distribution:StorageRoot med begränsade filrättigheter. Default är användarens LocalApplicationData/Sensor3/distribution; maxstorlek 100 MiB, konfigurerbart upp till 1 GiB. Använd lokal disk; flera noder/nätverksfilsystem och produktionslast är inte verifierade. Skydda och säkerhetskopiera katalog, paket och serverns Data Protection-nycklar. Publika paket kräver avsiktlig administratörspublicering; iterationens status uppdateras bara genom versionshanterad verifieringsprocess.

Administratörsmanifestet ska innehålla paketets verkliga version, buildnummer, commit och commitdatum. ExpectedSha256 beräknas från filen (PowerShell Get-FileHash -Algorithm SHA256). Servern beräknar storlek, verifierar hash och sätter publiceringsdatum. Release-formuläret visar ett ofullständigt exempel med tydliga platshållare som måste ersättas; exempelmetadata publiceras inte automatiskt.

Kryptografisk paketsignatur och metadata/identitet inuti APK/MSIX verifieras inte automatiskt ännu. Administratören måste kontrollera detta före publicering. Inga verkliga klientpaket har publicerats. Signerad paketering och riktiga installations-/uppgraderingstester hör till iteration 03. Inloggning via webbläsare med ett verkligt administratörskonto och kontroll på fysisk Android-enhet återstår. Därför är iteration 02 **Implemented**, inte Verified. Iteration 03 kan genomföras efter uttrycklig instruktion.

ASP.NET:s officiella riktlinjer har använts för [filuppladdning](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0), [CSRF](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery) och [Blazor-autentisering](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0).

Implementationscommit: `bdefc4b1a3d772472198afe8143f0119e4c28281`, verkligt commitdatum `2026-10-09T12:52:23Z` (14:52:23 svensk tid). Registrerat i iterations.json efter implementationscommitten. Varje bygge bäddar in den exakta byggda HEAD-committen separat.
