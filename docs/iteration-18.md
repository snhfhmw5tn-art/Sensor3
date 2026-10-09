# Iteration 18 – Validation and release

Version 0.18.0/build 18. Implemented. Verified/Released återstår tills fysisk acceptans respektive korrekt signering/installationsbevis finns.

Slutlig integration binder servertelemetri till samma analyskedja som native och replay, isolerat per session. Initiala referenser och tidsmarkerade start/heading/carry/truck/GPS-kontext följer telemetri och inspelning. Sessionbortkoppling/föråldrade data sänker aktuell kvalitet utan att ersätta råvärden eller radera ackumulerade räknare.

Payloadvalidering sker före mutation, med exakt en observation, kontrollerade sensorkällor/enheter/tider/enum/värden och begränsade storlekar. Klientens begränsade kö och batchstorlek redovisar rapporterade tappade händelser; deduplicering kräver samma payload för samma sekvens. Inspelning begränsas och håller uppdateringsspärr under aktiv capture. Kartfilter som förlorar matchning kräver ny referens.

Androids fristående Debug-APK innehåller aktuella managed assemblies. Ett faktiskt telefonprov avslöjade att tidigare fast deployment kunde visa iteration 05 trots nytt APK-manifest; version kontrolleras därför i faktisk runtime efter installation. Appdata bevaras under uppgraderingen.

README, ARCHITECTURE, SENSORS, ALGORITHMS, DEPLOYMENT, KNOWN_LIMITATIONS och VALIDATION beskriver aktuell implementation, experimentella parametrar, dataproveniens, testbevis och återstående fysisk validering. Syntetisk arm-rörelse missar steg och heading/PDR har uppstartsfel; dessa brister döljs inte.

214 MSTest passed, 0 failed/skipped. Slutligt Android/Windows-bygge, API/browser-smoke, distributionssmoke och faktiskt installerad buildmetadata dokumenteras i VALIDATION.md och artifacts. Exakt implementationscommit/datum registreras separat i iterations.json. Produktionssignering, fysisk ground truth och HTTPS-endpoint saknas; ingen release publiceras.

Native förgrundsprov på TC53: 2810/0 giltiga/avvisade accelerometerprov, faktisk50.04Hz vid begärd50Hz, korrekt nativeklocka/enheter och Inactive efter stopp. Slutlig diagnostik redovisar verkligt valt analysunderlag i stället för text från iteration05. Källvalsregression säkerställer en primär sensor per typ och linear-preferens. Browserreplay och faktisk CSV-nedladdning provade; 32 distributionskontroller passed. Se VALIDATION.md.
