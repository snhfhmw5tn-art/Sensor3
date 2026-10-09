# Iteration 14 – Forklift and GPS fusion

Version 0.14.0/build 14. Implemented; fysisk validering NotRun.

Contracts/Vehicle definierar separat IForkliftMotionEstimator och en observationsbus. Positioning/ForkliftMotionEstimator är en konservativ GPS-baseline. Core/VehicleSession prenumererar på faktiskt returnerade native-fixar, läser truckdeklaration och IMU-kontekst. RadioDiagnostics publicerar endast Available-resultat. Native DI registrerar bus/session och VehicleDiagnostics visar origin, separat trucksträcka, XY, fart, kurs, kvalitet och felorsak. Gång-PDR spärras redan av truckkontext. Återgång till gång kräver nytt evidensfönster och adderar inte native stegtotal.

Operatören sätter GPS-origin (öst/nord). Plan approximation gäller lokalt; nära polerna nekas. Aktuell icke-mock fix kräver noggrannhet ≤20 m, ålder ≤5 s, fart ≤15 m/s. Kurs används först vid ≥0.5 m/s. GPS-brus inom noggrannhetsradien ger olöst distans och räknas inte. Luckor/stora innovationer bryter sträckkedjan. Kvaliteten är heuristisk. Handburen IMU integreras inte till fordonsposition. Ingen automatisk truckklassning eller precis inomhusfart utlovas. Mounted-IMU-propagation utan extern referens är avsiktligt Unknown tills validerat fordons-/mount-underlag finns.

196 MSTest passed efter tillägg: färsk GPS, separat sträcka, kurs, gammal fix, gångkontext, mock, stillastående jitter, handvibration och lucka. Syntetisk fix/IMU är tydligt testdata. Full Android/Windows bygge + smoke före commit. Fysisk truck: stillastående, acceleration/broms, sväng, hand/mounted, gå→truck, utomhus→inomhus: NotRun. Kontrollerade fältdata saknas. Modellen är en GPS-quality-gate, inte en kalibrerad vehicle-EKF.

Filer ovan samt VehicleTests, version/manifest/iterationsmetadata. Build/testlogs artifacts/iteration-14-*. Commit/datum i iterations.json. Verified/Released återstår. Nästa iteration 15 godkänd.

Slutresultat: Android/Windows full lösning 0 fel/0 varningar; 196 tester passed, 0 failed/skipped; smoke passed.
