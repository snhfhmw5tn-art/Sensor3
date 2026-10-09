# Iteration 11 – Carrying mode recognition

Version 0.11.0/build 11. Implemented; fysisk verifiering återstår. Iteration 12 är redan godkänd.

Contracts/Carrying.cs definierar alla tolv bärpositionshypoteser, ett eget ICarryingClassifier och ICarryingSession. CarryingMode/BaselineCarryingClassifier.cs är en separat, utbytbar research-baseline. Den använder 2 s begränsat fönster med 50% överlapp, rå device-acceleration inklusive rekonstruerad gravity, tilt, gyro, vertikal/horisontell rörelse, aktivitet, eventuellt ljus/proximity och operatörsprior. Cross-window skillnad i max/min per axel ger övergångsevidens; within-window range visas separat. Trösklarna är experimentella konfigurerbara CarryingOptions.

Automatiska hypoteser omfattar stabil pose/Viewing, pendlande hand, vertikal rörelse, rotation, täckt/mörk Pocket, proximity-växling PickingUp/PuttingAway och DeviceStationary. Ljus/proximity måste verkligen finnas; saknad gyro/orientation ger Unknown. Fickhypotesen kan förväxla ficka med täckt sensor och mörk miljö. Viewing beskriver pose, inte uppmätt blick. Phone turn kan även vara människosväng och bedöms separat i nästa iteration. Scanning och Mounted kräver deklarerad operatörsprior eftersom IMU inte identifierar skanning eller fysisk montering pålitligt. UI tillåter deklaration av alla scenarier men märker dem som deklarerade, utan påstådd automatisk classifier-verifiering.

Probabilities-tabellen innehåller normaliserade heuristiska hypotespoäng, med explicit märkning att de inte är statistiskt kalibrerade sannolikheter. Confidence och Unknown visas. Ny sensorlucka återställer bara evidensfönstret. Inga koordinater eller steg genereras av klassificeraren.

Core/StepSession.cs matar bärposition separat från aktivitet och nollställer aldrig totalsumman vid bärbyte. Pending steg avvisas konservativt under detekterad transition, PickingUp/PuttingAway och VerticalSwinging för att inte tillverka förflyttning från lyft. Detta kan missa legitima steg under övergången och behöver mätvalidering. Native DI och CarryingDiagnostics visar aktuell hypotes, övergång, osäkerhet och fönsterfeatures.

## Resultat

- MSTest: 161 passed, 0 failed, 0 skipped före slutbygge. Hand/ficka/proximityövergång, posebyte, pendlande hand, Viewing/HeldStable, rotation/vertikala lyft, stillastående, saknad gyro/orientation och deklarerad Mounted/Scanning. Tidigare sessionsräknartest säkerställer att Pocket/Viewing-byte behåller totalsumman.
- Android + Windows VS MSBuild samt metadata/browser-smoke körs före commit; artifacts/iteration-11-*.
- Fysisk hand→ficka/ficka→hand, pendlande arm, TC53-skanning och montering: NotRun. Ingen precision/recall eller kalibrerad sannolikhet på verkliga data påstås. Inga märkt verkliga IMU-inspelningar hittades i App 2-repot (JSON-filer var endast projektkonfiguration).

Forskningsunderlag: [Deng et al. 2016](https://pmc.ncbi.nlm.nih.gov/articles/PMC4883368/) lästes via PMC och offentlig Europe PMC full-text XML (artifacts/heading-paper-2016.xml). Studien kombinerar bärpositionsigenkänning och övergångsdetektion med val av headingmetod. Sensor3 använder en enklare tröskelbaseline och utökat scenario-kontrakt; artikelns tränade modeller och rapporterade noggrannhet återanvänds inte som resultat för denna implementation.

Commit/datum registreras i iterations.json efter bygge. Ingen release publicerad.
