# Slutvalidering – iteration 18

Version 0.18.0. Iterationerna 6–18 har genomförts med separata implementations- och metadatacommittar. Exakt commit/datum finns i iterations.json och varje iterationsrapport.

## Automatiska bevis

- 214 MSTest: 214 passed, 0 failed, 0 skipped. TRX: artifacts/test-results/iteration-18.trx.
- Full Visual Studio MSBuild av Sensor3.sln: Android, Windows och .NET-serverprojekt. Slutresultat dokumenteras i artifacts/iteration-18-build.log och final-build.log.
- Åtta parallella gång-/trucksessioner använder samma analyskedja, med paketdeduplicering och isolerad sessionstate. Felaktiga observationer avvisas före partiell uppdatering. Bortkoppling gör aktuell fart/heading okänd.
- TLS/SignalR-integration, inspelningens kontextändringar, begränsad minnesbudget, JSON/CSV-rundresa, kartfilter och förlorad matchning ingår i testerna.
- API/dashboard-smoke och distributionssmoke använder lokal testmiljö och temporär katalog. Debugsignerad test-APK blir inte produktionsrelease.

## Modellresultat och begränsningar

Alla 15 scenarier körs genom den gemensamma kedjan och är märkta SYNTHETIC. 12 s rak gång ger stegfel 0, distansfel ca −2.70 m och positionsfel ca 5.00 m. Armfixturen ger 0 av 24 steg; detta dokumenteras som modellbrist. Startfönstret räknas med i klassifikationsprecisionen. CPU-värdet är processens CPU och kan inkludera samtidig aktivitet. Ingen syntetisk siffra är fysisk noggrannhet.

TC53-installation kontrolleras med faktisk appversion i skärmen, inte enbart APK-manifestet. Visual Studios gamla fast-deployment-assemblies upptäcktes och separat APK konfigureras med EmbedAssembliesIntoApk=true. Slutlig installerad version, byggd HEAD, APK-storlek och signatur kontrolleras efter sista commit.

Windowsbygge kontrolleras separat. Visuellt Windows-körtest, fysisk positionsprecision, långa batteritester, oberoende ground truth, produktionsserver och signerad Windowsrelease kräver ytterligare bevis. Ingen iteration markeras Verified eller Released på grund av simulering eller en lyckad kompilering.

Se KNOWN_LIMITATIONS.md, SENSORS.md, ALGORITHMS.md, ARCHITECTURE.md och DEPLOYMENT.md. Produktionsnycklar och HTTPS-origin har inte tillhandahållits; ingen produktionsrelease har publicerats.

## Faktiskt native-prov på TC53

2026-10-09: standalone-APK installerades över befintlig app med bibehållen appdata. Gammal Visual Studio-cache files/.__override__ flyttades reversibelt till files/codex-fastdeployment-backup-20261009 i samma appkatalog. Efter uppgradering/start visade appen version0.18.0/build18. Första tomma startvyn följdes upp med ny installation och faktisk UI-kontroll; ingen bestående renderingskrasch konstaterades.

Ett manuellt förgrundsprov av bmi26x Accelerometer Non-wakeup på ansluten Zebra TC53 gav 2810 giltiga och 0 avvisade prov, begärd50Hz/faktisk50.04Hz. Native monotonic timestamp11406543318626ns, AndroidElapsedRealtime, m/s², Android-device-ram och High-kvalitet rapporterades. Efter explicit stopp: Inactive och bevarad historik. Underlaget finns i artifacts/iteration-18-android-collected-ui.xml. Detta bevisar insamling/livscykel för detta prov, inte steg-/positionsnoggrannhet.

Tidigare APK-manifest/installationskontroller ska inte tolkas som bevis för faktisk managed runtime-version. Slutkontrollen efter ren commit jämför skärmens commit med git HEAD.

Browserprov: 20s syntetisk gång spelades vid8×, 4004händelser/40steg, tydlig SYNTHETIC-märkning och felmått. CSV-export skapade faktiskt Downloads/sensor3-session.csv (2271670byte). Kartans 1/5/10m-rutnät och läsbara 5m-etiketter kontrollerades visuellt. Äldre browserfel vid avsiktligt stoppad preview är inte fel under detta körprov.
