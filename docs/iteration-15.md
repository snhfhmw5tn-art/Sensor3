# Iteration 15 – WiFi and BLE positioning

Version 0.15.0/build 15. Implemented, fältverifiering återstår.

Contracts/RadioPositioning, Positioning/RadioFingerprintEstimator, Core/RadioPositionSession och RadioPositionDiagnostics implementerar verklig referensregistrering, lokal atomisk JSON-lagring i native appdata, validerad import/export samt observation→matchning→PDR-korrigering. BSSID/RSSI/frekvens/native tidsstämpel/cache och punktens XY/UTC/kvalitet bevaras. Ingen exempelradiokarta laddas automatiskt. Kända punkter och beacons måste ligga i samma ram som PDR.

Baseline: viktad kNN (k=3), gemensamma BSSID med samma frekvens, minst tre AP, RMS-RSSI-fel + saknad-AP-penalty, vikt quality/(1+error)^2. Cache/gamla observationer och stora RSSI-avvikelser nekas. Spatial spridning och RSSI-fel ger heuristisk osäkerhet, inte kalibrerad sannolikhet. BLE kräver känt ID, färsk observation och tydligt starkaste ankare; radien är operatörens uppgift, inte RSSI-räckvidd. Roterande BLE-ID kan förstöra ankarmatchningen.

PDR-korrigeringen begränsas av innovation och vikt ≤0.5. Rå XY/råspår sparas separat. Ingen förflyttningssträcka skapas av korrigeringshopp. Inomhusgeometri hanteras nästa iteration.

Forskningsbakgrund: [Microsoft Research RADAR](https://www.microsoft.com/en-us/research/publication/enhancements-to-the-radar-user-location-and-tracking-system/) beskriver referensprofiler och radio-positioneringens praktiska problem. [BLE DFW-WKNN](https://www.mdpi.com/1424-8220/20/24/7269) är alternativ för fingerprint-matchning, inte den implementerade närhetsankarmodellen. Ingen av artiklarnas precision överförs till Sensor3.

Tester: syntetiskt fingerprint vid (4,8) ger 0 m matematiskt referensfel men minimum 2 m modellradie. Cache, två AP, frekvensbyte och 40 dB störning ger Unknown. BLE-tvetydighet nekas. PDR-korrigering bevarar råspår och distans. Ingen fysisk radiomätning eller lagerreferensdataset fanns: fält-RMSE/95-percentil NotRun/Unknown. Kontrollerade radiokartor per telefon/lager behöver samlas in.

Android/Windows och MSTest/smoke körs före commit; artifacts/iteration-15-*. Filer ovan plus PositioningContracts/PDR/StepSession/DI/versionmetadata och RadioPositioningTests. Commit/datum registreras i iterations.json. Verified/Released återstår. Nästa iteration 16 godkänd.

Slutresultat: Android/Windows 0 fel/varningar; 200 tester passed, 0 failed/skipped; API/dashboard-smoke passed.
