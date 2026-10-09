# Funktionstest – iteration 01, 2026-10-09

## Komplettering: Visual Studios byggmiljö

Kontroll med Visual Studios egen MSBuild reproducerade först MSB4018: installationen saknade `VC\Tools\MSVC`. Detta fel syntes inte i tidigare `dotnet build`-kontroller. Efter installation av MSVC 14.51.36231 bygger hela solutionen med Visual Studios egen MSBuild för Android och Windows: 0 fel och 0 varningar (45,78 sekunder). Kontroll utförd på ren commit `4f7113d`.

SDK-sökvägar med kontrollerade lokala standardmappar har lagts till i Directory.Build.props för att åtgärda XA5300 när IDE har gamla miljövariabler. MSVC-komponenten deklareras i .vsconfig. Sju MSTest-fall passerar efter ändringen.

Installationen slutfördes efter att användaren stängt Visual Studio. Installationsloggen rapporterade 94 aktiviteter utan fel och avslutningskod 0. Efter bygget passerade alla sju MSTest-fall samt API/dashboard-smoketest med korrekt Git-metadata.

Byggkommando från repots rot:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe' Sensor3.sln /restore /t:Build /p:Configuration=Debug /m:1 /nr:false /v:minimal /nologo
```

Bygglogg: `artifacts/visual-studio-build.log` (lokal, inte versionshanterad). IDE:s F5-start och native About-navigation är separata manuella kontroller enligt nedan.

Testad checkout: `C:\Users\boris.gasic\source\repos\snhfhmw5tn-art\Sensor3`. Version 0.1.0. Testomfattningen är den implementerade grundplattformen, inte sensorfunktionerna i senare iterationer.

## Resultat

- Android och Windows kompilerar. Både den första och slutliga byggkontrollen efter CSS-rättningen hade 0 fel och 0 varningar.
- MSTest: 7 godkända, 0 misslyckade, 0 överhoppade.
- API och dashboard startar. Båda hälsokontrollerna, build-info, iterationsmanifestet och About svarar korrekt.
- Git-hash och commitdatum jämfördes med Git-metadata i Smoke-Test.ps1.
- Dashboardens Start → About testades genom faktisk webbläsarnavigation. About visar version 0.1.0, rätt commit, svensk tid, 1 implementerad / 0 verifierade / 0 publicerade iterationer samt alla 18 historikposter. Felpanelen visas inte på dashboardens fungerande startsida.
- Windows-klienten startades från byggd exe. Startsidan och permanent versionsrad lästes genom Windows accessibility API. Version 0.1.0, iteration 01 och rätt commit visas.
- Android debug-APK genereras, cirka 15,7 MB. Den är ett utvecklingspaket, ingen publicerad release.

## Fel som hittades och rättades

MAUI-hostens index.html refererade till `Sensor3.styles.css` efter att projektet bytt namn till Sensor3.Mobile. Filen finns som `Sensor3.Mobile.styles.css`. Länken har rättats. Innan rättningen syntes texten för Blazors felpanel även på fungerande startsida; efter ombyggnad och omstart är texten inte längre med i Windows-klientens synliga accessibility-innehåll.

En mellanliggande byggkontroll fick temporära filkopieringsvarningar eftersom testdashboarden fortfarande körde. Testservern stoppades inför den slutliga kontrollen.

## Återstående kontroller

- `adb devices` returnerar ingen ansluten Android-enhet/emulator. Installation, start och About på Android är inte testade.
- Automatisk Windows-navigation kunde inte slutföras: fönsterverktyget rapporterade `failed to activate captured window` och `coordinate input geometry is unavailable`. Detta är ett hinder i testverktyget; Windows-klientens startsida kunde läsas. About-navigation och visuell granskning kräver fortfarande manuell kontroll.
- Ingen komplett validering av sensorer, positionering, uppdateringar eller signerade produktionspaket har gjorts; dessa funktioner ingår i senare iterationer.

Status kvarstår **Implemented**, inte Verified. Iteration 02 har inte påbörjats.

## Manuell start i Visual Studio

### Komplettering: SDK-sökvägar från IDE

**Slutlig IDE-kontroll, 2026-10-09 kl. 14:17:** MAUI-grupperna Android, Windows, Blazor och Shared installerades via Visual Studio Installer (avslutningskod 0), inklusive OpenJDK 21.0.8. Rätt solution öppnades därefter i Visual Studio utan administratörsläge. Byggning startades i IDE:n med Ctrl+Shift+B. Visual Studios Output rapporterade **17 succeeded, 0 failed, 0 up-to-date, 0 skipped**, byggtid **1:38,524**. Error List med Entire Solution och Build + IntelliSense visade **0 Errors, 0 Warnings, 0 Messages**. XA5300 och tidigare Java-undantag med `homePath` återkom inte. Sju MSTest-fall och API/dashboard-smoketest passerade efter IDE-bygget med korrekt metadata för ren commit `badba0a`. Visual Studios byggfel är därmed verifierat löst i denna installation. Native runtime-kontroller enligt ovan återstår; iterationsstatus ändras inte.

Följande stycken beskriver de tidigare felsökningsstegen före installationen:

Ny byggkontroll på ren commit `ca33b6a`: hela solutionen byggdes med Visual Studios MSBuild (Debug, restore, båda native-målen), avslutningskod 0 utan rapporterade fel/varningar. Android `Compile` med DesignTimeBuild=true, BuildingInsideVisualStudio=true och BuildProjectReferences=false passerade med 0 fel/0 varningar på 2,93 sekunder. Alla sju MSTest-fall samt API/dashboard-smoketest passerade med korrekt Git-metadata. Lokala loggar: `artifacts/visual-studio-build.log` och `artifacts/android-design-time-build.log`. XA5300 reproducerades inte i dessa kontroller. IDE-komponenterna saknas fortfarande i installationslistan; resultatet bekräftar inte att IDE:s aktiva felpanel är rättad.

Fortsatt XA5300 ledde till kontroll av Visual Studio-installationens `selectedPackages`: MAUI-grupperna Android, Windows, Blazor och Shared saknas. Dessa deklareras nu i `.vsconfig`. Installation återstår medan Visual Studio körs. .NET SDK-workloads via CLI är installerade sedan tidigare; IDE-komponenterna är ett separat installationskrav.

Användarens SDK/JDK-inställningar har registrerats i 32-bitars registervyn under `HKCU\SOFTWARE\Novell\Mono for Android`, enligt SDK-resolverns implementation. Tidigare värden sparades lokalt i `artifacts/android-settings-before.json`. `_ResolveSdks` med projektets Directory.Build-filer och ANDROID_HOME/JAVA_HOME avstängda hittade båda installationerna och avslutades med kod 0. Design-time Compile för Android passerade också. Detta verifierar SDK-upptäckt utanför projektets reservlösning; XA5300 i IDE är ännu inte bekräftat löst.

Visual Studios felpanel visade fortfarande XA5300 trots korrekt checkout. Sensor3.Mobile tillåter nu lokal omvärdering av AndroidSdkDirectory/JavaSdkDirectory, och ett mål före `_ResolveSdks` använder installerade SDK/JDK i användarens lokala appdatamapp om tillförd sökväg saknar adb/java. Giltiga alternativa installationer behålls. Kontroll med Visual Studios MSBuild, DesignTimeBuild=true och avsiktligt ogiltiga globala SDK/JDK-sökvägar valde rätt lokala installationer och passerade. Hela solutionbygget för Android och Windows avslutades med kod 0.

IDE-inställningssökningen gav inga Android-inställningar. UI-verktygets klick misslyckades med `coordinate input geometry is unavailable`; IDE:s felpanel hade kvar XA5300 vid sista avläsningen. Omstart/omladdning i IDE återstår att kontrollera. Ingen direkt ändring av IDE:s inställningsdialog har verifierats.

Välj Sensor3.Mobile som startprojekt och Windows Machine för native-klienten, eller Sensor3.Dashboard för webbgränssnittet. Beräkningsbiblioteken är inte körbara startprojekt. Kontrollera Start → Om / iterationshistorik → Start. För Android behövs en ansluten enhet eller konfigurerad emulator.
