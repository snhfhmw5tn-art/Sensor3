# SENSOR 3 – MASTERPLAN FÖR CODEX
## Version 3.0 – Android, Windows, .NET 10 och forskningsbaserad inomhuspositionering

# A. Övergripande uppdrag

Skapa en helt ny applikation, Sensor 3, för sensorbaserad rörelseanalys och inomhuspositionering av personer i lager och varuhus.

Projektet ska genomföras i 18 tydliga iterationer.

Arbeta i samma Git-repository genom hela utvecklingen.

Fokusera på två native-klienter:

- Android
- Windows 10/11

iOS är pausat och ska inte implementeras eller byggas. Arkitekturen ska däremot inte förhindra framtida iOS-stöd.

Systemet består av:

1. Native-applikation för Android och Windows.
2. ASP.NET Core-server som tar emot sensordata.
3. Beräkningsmotor för steg, rörelse, hastighet och position.
4. Blazor-dashboard för realtidsövervakning.
5. Webbportal för installation och uppdatering.
6. Verktyg för simulering, inspelning och vetenskaplig validering.

## Viktigaste funktionella mål

Sensor 3 ska kunna uppskatta:

- Position X/Y i meter.
- Gångsträcka.
- Löpsträcka.
- Truckens färdsträcka när den kan mätas tillförlitligt.
- Aktuell hastighet.
- Färdriktning.
- Antal steg.
- Stegfrekvens.
- Aktuell aktivitet.
- Hur enheten hålls.
- Telefonens orientering.
- Positionsosäkerhet.

Den viktigaste tekniska utmaningen är att skilja personens förflyttning från enhetens egna rörelser.

Personen ska kunna:

- Titta på telefonen medan den går.
- Sänka telefonen.
- Gå med pendlande arm.
- Pendla telefonen uppåt och nedåt.
- Vrida telefonen 5–180 grader.
- Stoppa telefonen i fickan.
- Ta upp telefonen ur fickan mitt under gång.
- Svänga små och stora vinklar.
- Gå i mjuka kurvor.
- Stå och scanna varor.
- Springa.
- Köra truck.

Systemet ska försöka följa den verkliga rörelsen även under dessa förändringar.

Detta är utvecklings- och valideringsmål, inte garantier om att alla rörelser alltid kan rekonstrueras med en handhållen IMU.

När position eller riktning inte kan uppskattas tillförlitligt ska systemet redovisa osäkerhet.

---

# B. Teknik och lösningsstruktur

Använd:

- .NET 10.
- C# 14.
- .NET MAUI Blazor Hybrid.
- ASP.NET Core 10.
- Blazor.
- SignalR.
- ASP.NET Core Web API.
- System.Threading.Channels.
- BackgroundService.
- PostgreSQL.
- PostGIS.
- Entity Framework Core 10.
- MSTest.
- OpenTelemetry.
- Visual Studio 2026.

Skapa Sensor3.sln.

Projektstruktur:

- Sensor3.Mobile
- Sensor3.SharedUI
- Sensor3.Dashboard
- Sensor3.Api
- Sensor3.Contracts
- Sensor3.Core
- Sensor3.Sensors
- Sensor3.StepDetection
- Sensor3.ActivityRecognition
- Sensor3.CarryingMode
- Sensor3.SensorFusion
- Sensor3.Positioning
- Sensor3.MapMatching
- Sensor3.Infrastructure
- Sensor3.Distribution
- Sensor3.Simulation
- Sensor3.Tests

Implementera som en modulär monolit.

Använd Dependency Injection och tydliga gränssnitt.

Beräkningsbibliotek får inte vara beroende av UI eller native-plattformarna.

Sensorinsamling ska endast ske på Android och Windows.

Blazor-webbapplikationen ska inte använda webbläsarbaserade rörelsesensorer.

---

# C. Gemensamma utvecklingsregler

Skapa AGENTS.md.

Reglerna ska gälla samtliga iterationer.

1. Läs befintlig kod innan förändringar.
2. Bevara fungerande implementationer.
3. Bygg och testa efter varje iteration.
4. Implementera inte påhittad data som verkliga sensorvärden.
5. Markera simulerad data tydligt.
6. Dokumentera vetenskapliga algoritmer och begränsningar.
7. Undvik duplicerad kod.
8. Använd gemensamma C#-kontrakt.
9. Hantera undantag och nätverksavbrott robust.
10. Använd ILogger och strukturerad loggning.
11. Använd konfigurationsobjekt för algoritmparametrar.
12. Använd säkra standardvärden.
13. Gör inga tysta, inkompatibla API-ändringar.
14. Uppdatera dokumentation efter varje iteration.
15. Gör Git-commit efter färdig iteration när Git-repository finns.
16. Skapa inte commits som påstår att tester är godkända om de inte har körts.
17. Ändra inte användarens befintliga Git-ändringar utan anledning.
18. Använd aldrig force push.

## Teststandard

Använd MSTest.

Alla testmetoder börjar med TestThat och namnges med snake_case.

Exempel:

TestThat_phone_rotation_does_not_change_human_heading

TestThat_step_counter_continues_when_device_enters_pocket

TestThat_forklift_vibration_does_not_generate_false_steps

Använd:

- TestMethod.
- DataRow med DisplayName.
- Assert.ThrowsExactly<T>.
- Hjälpmetoder i PascalCase.

Använd inte DataTestMethod.

Följ reglerna:

IDE0007, IDE0017, IDE0018, IDE0020, IDE0022, IDE0031 och IDE0038.

Skapa automatiserade tester för varje beräkningskomponent.

---

# D. Permanent iteration- och versionshantering

Detta är ett obligatoriskt grundkrav redan från iteration 01.

Skapa en gemensam BuildInfo-modell.

Den ska innehålla:

- ApplicationName.
- ApplicationVersion.
- BuildNumber.
- GitCommitHash.
- GitCommitShortHash.
- GitCommitDateUtc.
- BuildDateUtc.
- GitBranch.
- IsDirtyBuild.
- TargetPlatform.
- ReleaseChannel.
- LatestImplementedIteration.
- LatestVerifiedIteration.
- LatestPublishedIteration.
- BuildIdentifier.

Använd Git och byggsystemet för att generera värdena automatiskt.

Commitdatum ska komma från Git-committens faktiska metadata.

Byggdatum ska komma från byggprocessen.

Blanda inte ihop dessa datum.

Visa datum i svensk lokal tid i gränssnittet, men lagra tidpunkterna i UTC.

## D.1 Skillnad mellan iterationsstatus

Definiera:

- NotStarted.
- InProgress.
- Implemented.
- Verified.
- Blocked.
- Released.

En iteration kan vara Implemented utan att vara Verified.

En release ska inte automatiskt markeras som verifierad bara för att dess kod kompilerar.

Skapa ITERATIONS.md och ett maskinläsbart iterationsmanifest, exempelvis iterations.json.

En kontrollerad verifieringsprocess ska uppdatera iterationsstatus.

Git-historiken ska inte ensam användas för att gissa vilka iterationer som är färdiga.

Manifestet ska versionshanteras tillsammans med koden.

## D.2 Build-metadata

Generera build-info vid varje bygge.

Hämta commit-hash och commitdatum från den commit som faktiskt byggs.

Använd exempelvis:

git rev-parse HEAD

git show -s --format=%cI HEAD

Hämta även branch och eventuell information om lokala ändringar.

Vid publicering måste paketet innehålla metadata från den exakta commit som användes i bygget.

Om Git-metadata saknas ska värden rapporteras som Unknown.

Använd aldrig dagens datum som ersättning för ett okänt commitdatum.

Om arbetskatalogen har ändringar som inte ingår i committen ska bygget markeras som dirty.

## D.3 Synlig versionsinformation

Visa en permanent kompakt informationsrad i:

- Android-appen.
- Windows-appen.
- Blazor-dashboarden.

Exempel:

Sensor 3 | Version 1.8.0 | Iteration 08/18 | Commit 8f2a91c

Skapa dessutom en detaljerad sida:

/about

Visa:

- Appversion.
- Plattform.
- Senaste implementerade iteration.
- Senaste verifierade iteration.
- Senaste publicerade iteration.
- Beskrivning av respektive iteration.
- Git-commit.
- Commitdatum.
- Byggdatum.
- Releasekanal.
- Serverversion.
- Uppdateringsstatus.

Visa klientens och serverns versionsinformation separat.

Om Android-klienten kör iteration 08 och servern iteration 09 måste båda värdena visas korrekt.

## D.4 Iterationshistorik

Visa alla 18 iterationer.

För varje iteration:

- Iterationsnummer.
- Namn.
- Status.
- Beskrivning.
- Senaste commit.
- Commitdatum.
- Datum då iterationen verifierades.
- Testresultat.
- Eventuella blockerande problem.

Visa en progressindikator för verifierade iterationer.

Räkna inte Implemented som Verified.

Skapa en administratörsvy där hela iterationshistoriken kan granskas.

Iterationsstatus får bara ändras genom den kontrollerade utvecklings- och verifieringsprocessen, inte genom godtycklig publik API-användning.

---

# ITERATION 01 – Grundplattform, Git och versionsinformation

Skapa hela Visual Studio-solutionen och nödvändiga projekt.

Implementera:

- Gemensamma projektberoenden.
- Dependency Injection.
- ASP.NET Core Web API.
- Blazor-dashboard.
- MAUI-app för Android och Windows.
- Gemensamma Razor-komponenter.
- Grundläggande navigering.
- Hälsokontroll för servern.
- Konfiguration.
- Loggning.

Implementera BuildInfo och iterationsmanifest redan nu.

Skapa:

- AGENTS.md.
- ITERATIONS.md.
- iterations.json.
- README.md.
- ARCHITECTURE.md.

Skapa en About-vy i Android, Windows och Blazor.

Visa:
- Version.
- Iteration.
- Commit-hash.
- Commitdatum.
- Byggdatum.

Om commitdatum saknas ska Unknown visas.

Skapa en automatiserad byggprocess som genererar BuildInfo.

Skapa tester för versionsinformation och iterationsstatus.

Acceptanskriterier:

- Solutionen kompilerar för tillgängliga byggmål.
- API och Blazor startar.
- MAUI-klienten kan startas på tillgänglig Android/Windows-miljö.
- About-vyn fungerar.
- BuildInfo innehåller verkliga metadata.
- Inga simulerade commitdatum presenteras som verkliga.

Commit:

Iteration 01 - Solution and build information

---

# ITERATION 02 – Webbportal för installation och versionshantering

Skapa distributionsportalen:

/download

Sidan ska fungera i en vanlig webbläsare på Android och Windows.

Identifiera plattform när det är möjligt men tillåt alltid manuellt plattformsval.

Visa:

- Android-klient.
- Windows-klient.
- Senaste version.
- Releasekanal.
- Publiceringsdatum.
- Versionsinformation.
- Filstorlek.
- Installationsinstruktioner.
- Nedladdningslänk.
- Aktuell iteration.
- Git-commit.
- Commitdatum.

Skapa releasehantering med:

- ApplicationRelease.
- ReleaseArtifact.
- ReleaseChannel.
- ReleaseManifest.
- ApplicationVersion.
- ClientCompatibility.
- UpdatePolicy.

Stöd Development, Beta och Stable.

Varje release ska innehålla:

- Plattform.
- Versionsnummer.
- Buildnummer.
- Git-commit.
- Commitdatum.
- Iterationsnummer.
- Filstorlek.
- SHA-256.
- Publiceringsdatum.
- Release notes.
- Kompatibel serverversion.
- Om uppdatering är obligatorisk.
- Om releasen är återkallad.

Skapa API:

GET /api/releases

GET /api/releases/latest

GET /api/updates/check

GET /api/releases/{id}/download

GET /api/system/build-info

GET /api/system/iterations

Implementera autentiserad administrationssida:

/admin/releases

Administratören ska kunna publicera och återkalla versioner.

Installationsfiler ska lagras säkert och inte kunna exekveras av webbservern.

Validera filtyper, storlek, checksumma och åtkomst.

Skapa tester för releaseval, versionsjämförelse och säkerhet.

Commit:

Iteration 02 - Application distribution portal

---

# ITERATION 03 – Android APK, Windows MSIX och uppdateringar

Implementera verklig installation och versionsuppgradering.

## Android

Generera signerade APK-filer för direktdistribution.

Använd stabilt application ID.

Använd monotont ökande versionCode.

Konfigurera säker signering utan nycklar i Git.

Skapa IApplicationUpdateService.

Klienten ska:

- Läsa installerad version.
- Kontrollera serverns senaste kompatibla version.
- Visa när uppdatering finns.
- Visa release notes.
- Ladda ner uppdateringen.
- Kontrollera paketets checksumma mot ett autentiserat manifest.
- Starta Androids normala installationsflöde.

Respektera Androids krav på användarinteraktion och behörigheter.

Ingen otillåten tyst installation.

## Windows

Använd signerade MSIX-paket med App Installer.

Generera .appinstaller-manifest.

Stöd uppdatering vid programstart.

Använd Windows egna stödda uppdateringsfunktioner.

Hantera certifikattillit och stabil paketidentitet.

## Gemensamt

Visa:

- Installerad version.
- Tillgänglig version.
- Installerad iteration.
- Tillgänglig iterationsnivå.
- Commit för installerad version.
- Commitdatum.
- Uppdateringsstatus.

Behåll normalt appdata vid kompatibel uppgradering.

Skydda pågående sensormätningssessioner vid uppdatering.

Skapa tester för versionskontroll, korrupta filer, fel kanaler och nätverksavbrott.

Genomför verkliga version-1-till-version-2-uppdateringstester när signerade paket och testhårdvara finns.

Commit:

Iteration 03 - Client installation and updates

---

# ITERATION 04 – Native sensorinsamling

Skapa gemensamma C#-gränssnitt:

- ISensorProvider.
- SensorDescriptor.
- SensorReading.
- SensorCapability.
- SensorStatus.
- SensorQuality.

Android:

Använd SensorManager och getSensorList(TYPE_ALL).

Windows:

Använd Windows.Devices.Sensors.

Upptäck alla tillgängliga sensorer.

Inkludera:

- Accelerometer.
- Gyroskop.
- Gravitation.
- Linjär acceleration.
- Magnetometer.
- Rotationsvektor.
- Orientering.
- Barometer.
- Stegdetektor.
- Stegräknare.
- Övriga exponerade sensorer.

Upptäck faktisk hårdvara.

Visa Unsupported när en funktion saknas.

Använd korrekta tidsstämplar och normaliserade enheter.

Stöd konfigurerbar samplingsfrekvens.

Utgå från 50 Hz för IMU, men mät faktisk frekvens.

Skapa tester för sensorupptäckt, konvertering och avbrott.

Commit:

Iteration 04 - Native sensor collection

---

# ITERATION 05 – Komplett sensordiagnostik

Skapa Sensordiagnostik.

Visa samtliga upptäckta sensorer.

För varje sensor:

- Namn.
- Beskrivning.
- Tillverkare.
- Tillgänglighet.
- Behörighet.
- Aktivitet.
- Användning i beräkningar.
- Mätvärden.
- Uppdateringsfrekvens.
- Tidsstämpel.
- Datakvalitet.
- Fel.

Skilj på:

Available, Unsupported, PermissionRequired, PermissionDenied, Initializing, Active, Inactive, Stale och Error.

Visa realtidsgrafer.

Visa X/Y/Z där det är relevant.

Visa rådata och filtrerade data.

Visa om sensorn används av den aktiva positioneringsalgoritmen.

Skapa en vy både i MAUI och Blazor.

Dashboarden får endast visa sensordata mottagna från native-klienterna.

Visa klientens version, iteration och commit även i diagnostiken.

Skapa tester för status och timeout.

Commit:

Iteration 05 - Sensor diagnostics

---

# ITERATION 06 – GPS, WiFi och Bluetooth

Implementera:

- ILocationProvider.
- IWifiScanner.
- IBluetoothScanner.
- IStepSensorProvider.

Android:

Använd tillgängliga native API:er för WiFi, BLE och platsinformation.

Windows:

Använd motsvarande Windows API:er.

Samla in:

GPS:
- Latitude.
- Longitude.
- Accuracy.
- Speed.
- Heading.
- Altitude.
- Timestamp.

WiFi:
- SSID.
- BSSID.
- RSSI.
- Frekvens.
- Timestamp.

BLE:
- Identifierare.
- RSSI.
- Annonseringsinformation.
- Timestamp.

Respektera behörigheter, skanningsbegränsningar och plattformsskillnader.

Visa råinformationen i Sensordiagnostik.

Implementera ingen ogrundad positionsberäkning från WiFi.

Skapa tester.

Commit:

Iteration 06 - GPS WiFi and BLE collection

---

# ITERATION 07 – Realtidskommunikation

Implementera effektiv kommunikation från Android och Windows till servern.

Använd:

- HTTPS.
- ASP.NET Core Web API.
- SignalR.
- Tidsstämplade sensorpaket.
- Sekvensnummer.
- Enhets-ID.
- Sessions-ID.
- Batchöverföring.
- Lokal buffring.
- Paketdeduplicering.
- Återanslutning.
- Backpressure.

Använd konfigurerbart batchintervall, initialt 100–250 ms.

Behåll den tidsupplösning som algoritmerna kräver.

Skapa BackgroundService för serverbehandling.

Använd Channels när lämpligt.

Implementera Research Mode och Production Mode.

Visa paket/sekund, byte/sekund, latens och paketförlust.

Låt klienten rapportera BuildInfo och iterationsinformation vid anslutning.

Servern ska lagra och visa vilken version varje ansluten klient kör.

Skapa integrationstester för parallella enheter och störningar.

Commit:

Iteration 07 - Realtime sensor communication

---

# ITERATION 08 – Stegräknaren från Sensor App 2

Återanvänd i första hand stegdetekteringsalgoritmen från Sensor App 2.

Sök källkoden i tillgängligt workspace.

Läs den verkliga implementationen innan den ändras.

Om den inte finns tillgänglig, dokumentera detta och implementera en separat baseline.

Skapa en oberoende stegräknarmodul.

Visa:

- Totala steg.
- Gångsteg.
- Löpsteg.
- Steg per minut.
- Stegfrekvens.
- Uppskattad steglängd.
- Gångsträcka.
- Tillförlitlighet.

Jämför med Androids inbyggda stegsensorer där tillgängliga.

Undvik dubbelräkning.

Stegräknaren får inte nollställas vid bärpositionsbyte.

Testa:

- Normal gång.
- Löpning.
- Pendlande arm.
- Telefon i ficka.
- Telefonrotation.
- Stående skanning.
- Truckvibrationer.
- Stopp och start.

Visa stegräknaren permanent i mobilklienten.

Skapa tester för stegdetektering och sessionsräkning.

Commit:

Iteration 08 - Step counter

---

# ITERATION 09 – Koordinattransformation och sensorfusion

Implementera:

- Kvaternioner.
- Rotationsmatriser.
- Gravitationskompensation.
- Enhetsnormalisering.
- Koordinattransformation.
- Filtrering.
- Gyroskopbias.
- Sensorbrus.
- Tidsjustering mellan sensorer.

Separera:

- DeviceOrientation.
- DeviceMotion.
- HumanHeading.
- HumanPosition.

Telefonens rotation får inte automatiskt generera personförflyttning.

Implementera tester för rotation runt X, Y och Z.

Dokumentera matematik och axelkonventioner.

Commit:

Iteration 09 - Sensor fusion

---

# ITERATION 10 – Aktivitetsigenkänning

Implementera klassificering av:

- Stationary.
- Walking.
- Running.
- Forklift.
- Unknown.

Använd forskningsbaserade metoder från Human Activity Recognition.

Undersök:

- Varians.
- Frekvensanalys.
- Periodicitet.
- Rörelseenergi.
- Accelerationsmönster.
- Gyroskopmönster.
- Stegfrekvens.
- Sekvensbaserad klassificering.

Skapa en reproducerbar baseline och stöd framtida tränade modeller.

Visa klassificeringssäkerhet.

Använd Unknown när informationen inte räcker.

Truckklassificering ska inte påstås vara validerad utan verkliga truckdata.

Skapa tester för aktivitetstillstånd och övergångar.

Commit:

Iteration 10 - Activity recognition

---

# ITERATION 11 – Dynamisk bärpositionsklassificering

Identifiera:

- Viewing.
- HeldStable.
- HandSwinging.
- VerticalSwinging.
- HandRotating.
- Scanning.
- Pocket.
- PickingUp.
- PuttingAway.
- DeviceStationary.
- Mounted.
- Unknown.

Identifiera växling mellan olika bärpositioner mitt under gång.

Klassificeringen ska vara separat från aktivitetsklassificeringen.

En bärpositionsförändring får inte automatiskt skapa steg eller ändra färdriktningen.

Använd forskning inom Phone Carrying Mode Recognition.

Visa osäkerhet och sannolikhet.

Testa hand till ficka, ficka till hand och pendlande arm.

Commit:

Iteration 11 - Carrying mode recognition

---

# ITERATION 12 – Personens färdriktning oberoende av telefonen

Detta är projektets viktigaste forskningsiteration.

Undersök:

Carrying Position Independent User Heading Estimation for Indoor Pedestrian Navigation with Smartphones.

https://pmc.ncbi.nlm.nih.gov/articles/PMC4883368/

Implementera separat:

- IDeviceOrientationEstimator.
- IHumanHeadingEstimator.

Undersök och jämför:

- PCA.
- Gyroskopbaserade metoder.
- Gångriktning från rörelsemönster.
- Adaptiv riktningsuppskattning.
- Byte av metod efter bärposition.

Testa telefonrotation:

5, 10, 15, 20, 30, 45, 60, 90 och 180 grader.

Testa verkliga personsvängar med samma vinklar.

Testa:

- Rak gång med telefonrotation.
- Pendlande arm.
- Telefon i ficka.
- Telefon tas upp under gång.
- Telefon vrids samtidigt som personen svänger.
- Rotation i motsatt riktning.
- Mjuka kurvor.
- S-formade rörelser.

Visa telefonens riktning och personens uppskattade färdriktning separat.

Rapportera osäkerhet.

Skapa automatiserade tester och jämförelser med verklig sensorinspelning där sådan finns.

Commit:

Iteration 12 - Human heading estimation

---

# ITERATION 13 – PDR, position och hastighet

Implementera Pedestrian Dead Reckoning.

Använd:

- Registrerade steg.
- Steglängd.
- HumanHeading.
- ActivityState.
- CarryingMode.
- SensorQuality.

Beräkna:

- Lokal X/Y-position.
- Gångsträcka.
- Löpsträcka.
- Hastighet.
- Färdriktning.
- Positionsosäkerhet.

Stöd känd startposition och startriktning.

Beräkna positionsförändring från steg och uppskattad färdriktning.

Använd adaptiv steglängdsmodell med dokumenterade parametrar.

Använd inte dubbelintegration av handhållen accelerometer som huvudsaklig långvarig positioneringsmetod.

Testa 10, 20, 40 och 100 meter.

Testa svängar och bärpositionsbyten.

Vid otillräcklig riktningsinformation ska osäkerheten öka.

Commit:

Iteration 13 - PDR positioning and speed

---

# ITERATION 14 – Truckkörning och GPS-fusion

Implementera en separat fordonsmodell.

Skapa IForkliftMotionEstimator.

Vid truckläge ska vanlig gångbaserad PDR inte användas för truckens förflyttning.

Använd tillgängliga:

- GPS-positioner.
- GNSS Speed.
- GNSS Heading.
- IMU-mätningar.
- Externa positionsreferenser.

Kontrollera kvalitet.

Visa Unknown när truckens hastighet inte kan uppskattas tillförlitligt.

Skilj på gångsträcka och trucksträcka.

Testa:

- Stillastående truck.
- Vibrationer.
- Acceleration.
- Bromsning.
- Svängar.
- Telefon i handen.
- Telefon monterad.
- Växling mellan gång och truck.
- GPS-avbrott.

Dokumentera begränsningar.

Commit:

Iteration 14 - Forklift and GPS fusion

---

# ITERATION 15 – WiFi- och BLE-positionering

Implementera WiFi-fingerprinting.

Skapa stöd för att registrera kända referenspunkter i ett lager.

Lagra:

- X/Y-koordinater.
- BSSID.
- RSSI.
- Frekvens.
- Tidsstämpel.
- Kvalitet.

Implementera forskningsbaserad fingerprinting-baseline.

Undersök exempelvis viktad k-nearest neighbors.

Skapa möjlighet att beräkna en sannolik position utifrån uppmätta accesspunkter.

Implementera BLE-beacons som kompletterande positionsankare.

Använd observationerna för att korrigera PDR.

Undvik att direkt tolka RSSI som exakt avstånd.

Testa på referensdata och dokumentera positionsfel.

Commit:

Iteration 15 - WiFi and BLE positioning

---

# ITERATION 16 – Lagerkarta och kartmatchning

Implementera livekarta i Blazor och MAUI.

Två lägen:

1. Lokal karta utan bakgrund.
2. Google Maps som frivillig bakgrund.

Lokal karta ska visa:

- X/Y i meter.
- Metergradering.
- 1-, 5- och 10-metersmarkörer.
- Zoom.
- Panorering.
- Auto Follow.
- Startpunkt.
- Position.
- Färdspår.
- Hastighet.
- Färdriktning.
- Antal steg.
- Positionsosäkerhet.

Stöd lagergeometri:

- Väggar.
- Ställage.
- Gångar.
- Truckvägar.
- Spärrade ytor.
- Positionsankare.

Implementera forskningsbaserad kartmatchning, exempelvis partikelfilter.

Visa både rå och kartmatchad position.

Google Maps ska vara frivilligt och använda korrekt koordinatkalibrering.

Commit:

Iteration 16 - Indoor mapping and map matching

---

# ITERATION 17 – Inspelning, simulering och jämförelse

Implementera inspelning och uppspelning.

Stöd:

- Start/stopp.
- Sessionsnamn.
- JSON-export.
- CSV-export.
- Import.
- Uppspelning.
- Paus.
- Valbar hastighet.
- Tidslinje.
- Ground truth.

Skapa syntetiska scenarier:

- Rak gång.
- Pendlande arm.
- Ficka.
- Telefonrotation.
- Små personsvängar.
- Mjuka kurvor.
- Löpning.
- Stillastående.
- Skanning.
- Truckkörning.
- Truckvibrationer.
- GPS-avbrott.
- WiFi-brus.
- Sensoravbrott.
- Nätverksfördröjning.

Skapa jämförelseverktyg.

Visa:

- Stegfel.
- Sträckfel.
- Riktningsfel.
- Positionsfel.
- Klassificeringsprecision.
- Modellfördröjning.
- Datamängd.
- CPU-belastning.

Simulering får inte presenteras som verklig validering.

Commit:

Iteration 17 - Recording and simulation

---

# ITERATION 18 – Slutvalidering, säkerhet och publicering

Genomför en fullständig kvalitetssäkring.

Kontrollera:

- Android-installation.
- Windows-installation.
- Uppdateringar.
- Versionsinformation.
- Git-commit och commitdatum.
- Iterationshistorik.
- Sensorinsamling.
- Sensordiagnostik.
- Serverkommunikation.
- Stegräknare.
- Aktivitetsklassificering.
- Bärpositionsklassificering.
- Färdriktning.
- PDR.
- Truckmodell.
- GPS.
- WiFi.
- BLE.
- Lagerkarta.
- Simulering.
- Integritet.
- Autentisering.
- Auktorisering.
- Prestanda.

Verifiera att alla installerade klienter visar rätt bygg- och versionsmetadata.

Kontrollera att webbportalen visar verkliga tillgängliga versioner.

Verifiera att senaste publicerade iteration inte förväxlas med senaste implementerade iteration.

Kör samtliga tillgängliga MSTest-tester.

Genomför belastningstester med flera samtidiga klienter.

Kontrollera att sessioner inte blandas ihop.

Kontrollera att uppdateringar hanterar aktiva insamlingssessioner säkert.

Skapa dokumentation:

- README.md.
- ARCHITECTURE.md.
- RESEARCH.md.
- SENSORS.md.
- ALGORITHMS.md.
- ITERATIONS.md.
- VALIDATION.md.
- DEPLOYMENT.md.
- KNOWN_LIMITATIONS.md.

Skapa en rapport över:

- Byggresultat.
- Testresultat.
- Installerbara paket.
- Verifierade iterationer.
- Publicerade iterationer.
- Faktiska mätresultat.
- Kända begränsningar.
- Kvarstående arbete.

Publicera endast signerade och kontrollerade releasepaket.

Commit:

Iteration 18 - Validation and release

---

# E. Obligatorisk slutrapport efter varje iteration

Efter varje iteration ska Codex rapportera:

1. Iterationsnummer och namn.
2. Implementerade funktioner.
3. Ändrade filer.
4. Build-resultat.
5. Antal godkända tester.
6. Antal misslyckade tester.
7. Antal ej körda tester.
8. Manuella tester som återstår.
9. Eventuella kända begränsningar.
10. Senaste Git-commit.
11. Commitdatum.
12. Aktuell applikationsversion.
13. Status i ITERATIONS.md.
14. Om iterationen är Implemented eller Verified.
15. Om nästa iteration kan genomföras.

Skapa inte falska testresultat.

Om miljön saknar Android-enhet eller andra beroenden, rapportera detta.

En iteration får inte markeras Verified enbart för att projektet kompilerar, när acceptanskriterierna också kräver verklig hårdvara.

---

# F. Instruktion till Codex

Börja med att:

1. Skapa eller öppna Git-repositoryt Sensor3.
2. Skapa AGENTS.md.
3. Skapa ITERATIONS.md.
4. Skapa iterations.json.
5. Registrera alla 18 iterationer.
6. Implementera iteration 01.
7. Skapa den automatiska BuildInfo-genereringen.
8. Visa iterationsnummer, versionsnummer, Git-commit och commitdatum i webb- och MAUI-applikationen.
9. Kör byggen och tester.
10. Dokumentera resultatet.
11. Gör commit när implementationen uppfyller kriterierna.

Genomför bara en iteration i taget.

Påbörja inte nästa iteration automatiskt efter avslutad implementation.

Vid fortsatt instruktion från användaren ska du fortsätta med nästa iteration enligt denna masterplan.

Fråga inte om vanliga tekniska beslut som redan beskrivs.

Prioritera fungerande, verifierbar kod framför omfattande men otestade implementationer.

Målet är en forskningsbaserad, modern och utbyggbar plattform för realtidspositionering med Android- och Windows-klienter, automatiserad distribution och full spårbarhet mellan iterationer, versioner och Git-commits.