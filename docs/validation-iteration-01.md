# Funktionstest – iteration 01, 2026-10-09

## Komplettering: Visual Studios byggmiljö

Senare kontroll med Visual Studios egen MSBuild reproducerade MSB4018: installationen saknar `VC\Tools\MSVC`. Detta fel syntes inte i tidigare `dotnet build`-kontroller. Windows-bygge i IDE är därför ännu inte godkänt.

SDK-sökvägar med kontrollerade lokala standardmappar har lagts till i Directory.Build.props för att åtgärda XA5300 när IDE har gamla miljövariabler. MSVC-komponenten deklareras i .vsconfig. Sju MSTest-fall passerar efter ändringen.

Installationsförsöket med administratörsrättigheter stoppades av Visual Studio Installer med `VSProcessesRunning`. Användaren behöver spara och stänga Visual Studio innan installationen kan fortsätta. Inga användarprocesser stängdes med tvång. Verifiera hela solutionen med Visual Studios MSBuild efter installation.

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

Välj Sensor3.Mobile som startprojekt och Windows Machine för native-klienten, eller Sensor3.Dashboard för webbgränssnittet. Beräkningsbiblioteken är inte körbara startprojekt. Kontrollera Start → Om / iterationshistorik → Start. För Android behövs en ansluten enhet eller konfigurerad emulator.
