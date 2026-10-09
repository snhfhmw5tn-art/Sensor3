# Iteration 03 – Android APK, Windows MSIX och uppdateringar

Version 0.3.0. Status: **Implemented**, inte Verified eller Released. Iteration 04 har inte påbörjats.

## Funktioner och ändrade filer

`Sensor3.Contracts/ApplicationUpdates.cs` definierar IApplicationUpdateService, installerad version, signerade besked, OS-installation och gemensamt sessionslås. `Sensor3.Core/ApplicationUpdateService.cs` hämtar serverns kompatibla version, verifierar RSA-PSS/SHA-256 med en inbyggd betrodd publik nyckel och en slumpad nonce, samt kräver ett färskt besked. Klienten kontrollerar plattform, kanal och ökande version/build. SHA-256 och exakt filstorlek kontrolleras före installation. En ny signerad kontroll före OS-dialogen stoppar återkallade eller ersatta releaser. Misslyckade hämtningar raderas; lyckade paket ligger i OS-cache tills OS rensar den.

`Sensor3.Mobile/MauiProgram.cs`, `App.xaml.cs`, `NativeUpdateInstaller.cs`, Android-manifest och `update_paths.xml` registrerar tjänsten, kontrollerar vid appstart och öppnar normal installationsdialog. Android använder en icke exporterad FileProvider och begär vid behov användarens tillstånd att installera från Sensor 3. Windows öppnar MSIX med Windows App Installer. Inga tysta installationer eller ändringar i certifikattillit görs. Appdata återställs inte eller raderas av uppdateringskoden. Android-identiteten är fortsatt `se.qsys.sensor3`; Windows-identiteten är `se.qsys.sensor3`, publisher `CN=Sensor3`. Versionskod 3 är standard och kan höjas vid paketering. Windows-paketet inkluderar .NET och Windows App SDK för att undvika saknade runtime-beroenden på målmaskinen.

`Sensor3.SharedUI/Pages/Updates.razor` visar installerad/tillgänglig version, iteration, commit, commitdatum, release notes, policy och uppdateringsstatus separat. Native-knappar hanterar fel. Webbversionen länkar till nedladdningsportalen och samlar inga sensorer.

`Sensor3.Distribution/DistributionHosting.cs`, `ReleaseStore.cs`, `PackageVerifier.cs` och `AppInstallerManifest.cs` lägger till autentiserade uppdateringsbesked, signaturkontroll före publicering och App Installer per kanal. Android kräver en pinnad signeringscertifikathash samt verifierad appidentitet/version med apksigner och aapt. Windows kräver SignTool-verifiering med betrodd certifikatkedja och samma paketidentitet/version som releasemanifestet. Saknade verktyg/nycklar stoppar flödet. ReleaseStore kräver fortsatt globalt ökande buildnummer per plattform. Uppdateringsmanifest och paketsignering använder separata nycklar.

`tools/Publish-Client.ps1`, `New-AppInstaller.ps1`, `New-UpdateManifestKey.ps1` stöder signerad APK/MSIX och `.appinstaller`. App Installer använder en stabil kanaladress, kontroll vid start, användarprompt och ingen automatisk bakgrundsuppdatering. `UpdateSessionGuard` hindrar installation under mätning och hindrar nya sessioner medan den appstyrda installationen förbereds. Iteration 04 måste använda samma singleton genom `BeginSession()` för hela mätningen. Extern installation via OS är användarstyrd och kan inte låsas av appen.

## Konfiguration och signering

Klientens `Resources/Raw/update-settings.json` är avsiktligt okonfigurerad. Ange en verklig HTTPS-adress, vald kanal och serverns publika manifestnyckel i en separat inställningsfil. Private key, keystore och lösenordsfil måste ligga utanför repositoryt. Spara/backupa samma Android-nyckel för alla uppgraderingar; en debugnyckel får inte bli produktionsidentitet. Windows-certifikatet måste ha privat nyckel, Code Signing och exakt subject `CN=Sensor3`. Publisher och paketnamn måste väljas före första skarpa installationen och behållas. Certifikatet måste vara betrott på servern för verifiering och på klienten för installation; självsignerade testcertifikat blir inte automatiskt betrodda.

Skapa serverns RSA-nyckel manuellt med PowerShell 7:

```powershell
./tools/New-UpdateManifestKey.ps1 -PrivateKeyPath C:/Secure/Sensor3/manifest.private.pem -PublicKeyPath C:/Secure/Sensor3/manifest-public.pem
```

Konfigurera båda serverhostarna med `Distribution:ManifestSigningKeyPath` (privat PEM), `Distribution:PublicBaseUrl` (portalens HTTPS-adress), `Distribution:AndroidApkSignerPath` (SDK build-tools/lib/apksigner.jar), `Distribution:JavaPath` (java.exe), `Distribution:AndroidAaptPath` (aapt.exe), `Distribution:AndroidCertificateSha256` (64 hextecken utan kolon) och `Distribution:WindowsSignToolPath` (signtool.exe). Miljövariabler använder dubbla understreck. Behåll adminautentisering från iteration 02. Gamla kataloger med tidigare okontrollerade paket måste revalideras före drift.

Publiceringskommandon med egna externa nycklar:

```powershell
./tools/Publish-Client.ps1 -Platform Android -BuildNumber 4 -UpdateSettingsPath C:/Secure/Sensor3/update-settings.json -ReleaseNotes 'Installation och säkra uppdateringar' -AndroidKeyStore C:/Secure/Sensor3/sensor3.keystore -AndroidKeyAlias sensor3 -AndroidPasswordFile C:/Secure/Sensor3/android-password.txt
./tools/Publish-Client.ps1 -Platform Windows -BuildNumber 4 -UpdateSettingsPath C:/Secure/Sensor3/update-settings.json -ReleaseNotes 'Installation och säkra uppdateringar' -WindowsCertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT -PackageUrl https://YOUR_HOST/Sensor3.msix -AppInstallerUrl https://YOUR_HOST/Sensor3.appinstaller
```

Skriptet bäddar in den externa inställningsfilen via `Sensor3UpdateSettingsPath` och ändrar inga versionshanterade filer. Artefakter hamnar under ignorerad `artifacts/releases`. Publicera via adminportalen först efter kontroll av signatur, metadata och installation. Portalens `/api/releases/Sensor3.appinstaller?channel=Stable` genereras från senaste kompatibla, ej återkallade Windows-paketet; använd portalens kanaladress för fortsatt versionskontroll. Statisk `.appinstaller` kan användas för separat HTTPS-hosting. Öka buildnummer vid varje release; skriptet tillåter 3–65535 för gemensam Windows/Android-versionering. Releasekatalogen avvisar återanvända nummer. Standardgränsen är 100 MiB i klient och server; ändra båda om ett giltigt paket behöver mer utrymme.

Microsofts dokumentation: [signerad Android-publicering](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0), [App Installer-manifest](https://learn.microsoft.com/en-us/windows/msix/app-installer/how-to-create-appinstaller-file), [kontroll vid start och uppdateringsprompt](https://learn.microsoft.com/en-us/windows/msix/app-installer/update-settings).

## Kontroller och resultat

- Android + Windows Debug via Visual Studio 2026 MSBuild: 0 fel, 0 varningar.
- Windows Release, självförsörjande MSIX-paketering utan signering: 0 fel, 0 varningar. Verkligt paket 91 497 874 byte, identitet `se.qsys.sensor3`, publisher `CN=Sensor3`, version `0.3.0.3`, x64. Inga separata PackageDependency-runtimekrav. Paketet är **osignerat**, inte publicerat och inte ett installationsbevis.
- `New-AppInstaller.ps1` kördes mot detta verkliga MSIX: korrekt identitet/version, HTTPS-URL och startprompt i genererad XML. `updates.example` är en testadress, ingen driftsatt tjänst.
- MSTest: **57 passed, 0 failed, 0 skipped**. Omfattar versionskontroll, signatur, replay/nonce, utgånget manifest, fel plattform/kanal, nedgradering, checksumma, ofullständig fil, nätavbrott, återkallelse under hämtning, avbrott och sessionslås. Testinstallern är en testdubbel och ersätter inte native installationsprov.
- **32 HTTP-kontroller passerade** med verklig debug-signerad APK i en tillfällig katalog. Kontroll av signatur, inbyggd version, autentiserat besked, CSRF, rättigheter, revokering och korruption; osignerad APK och fel inbyggd version avvisades. API/dashboard health, About, 18 iterationer och Git-metadata passerade separat. Ingen testrelease lämnas kvar i verklig katalog.
- Paketets Android-provider och installationsbehörighet kontrollerades i den byggda APK-filen med aapt.

## Kvarvarande manuella acceptanskriterier

Ingen Android-enhet/emulator är ansluten (`adb devices` tom). Produktions-HTTPS, keystore och Windows-certifikat har inte tillhandahållits. Skarp releasepaketering, Androids tillstånds-/installationsdialog, Windows-certifikattillit, installation via App Installer, startkontroll och bevarad appdata vid verklig v1→v2-uppgradering är **NotRun**. Native uppdateringssida behöver också manuell kontroll på båda plattformarna.

Den automatiska godkännandekontrollen avvisade provkörningar som skapade privata signeringsnycklar med `blocked by policy`, utan mer detaljerad orsak. Därför har nyckelskriptets körning och signerad Windows-MSIX inte verifierats. Den osignerade MSIX-kontrollen bekräftar paketering och metadata enbart.

Manuellt: installera v1 med samma stabila signerare/identitet, spara appdata, publicera v2 med högre buildnummer och korrekt manifest. Kontrollera att klienten visar nya metadata, godkänn OS-installationen och verifiera version, återstart och kvarvarande appdata. Prova också återkallelse, avbruten hämtning och sessionsskydd. Uppdatera status till Verified först när alla kriterier har bevis.

Nästa möjliga iteration är 04 – Native sensorinsamling, efter användarens instruktion. Senaste implementationscommit och UTC-datum registreras i `iterations.json` efter committen; exakt byggd HEAD visas i BuildInfo.
