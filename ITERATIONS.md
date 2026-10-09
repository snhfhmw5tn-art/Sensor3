# Iterationshistorik

Version 0.1.0. Implementerade: 1/18. Verifierade: 0/18. Publicerade: 0/18.

| Nr | Namn | Status |
|---|---|---|
| 01 | Grundplattform, Git och versionsinformation | Implemented |
| 02 | Webbportal för installation och versionshantering | NotStarted |
| 03 | Android APK, Windows MSIX och uppdateringar | NotStarted |
| 04 | Native sensorinsamling | NotStarted |
| 05 | Komplett sensordiagnostik | NotStarted |
| 06 | GPS, WiFi och Bluetooth | NotStarted |
| 07 | Realtidskommunikation | NotStarted |
| 08 | Stegräknaren från Sensor App 2 | NotStarted |
| 09 | Koordinattransformation och sensorfusion | NotStarted |
| 10 | Aktivitetsigenkänning | NotStarted |
| 11 | Dynamisk bärpositionsklassificering | NotStarted |
| 12 | Personens färdriktning oberoende av telefonen | NotStarted |
| 13 | PDR, position och hastighet | NotStarted |
| 14 | Truckkörning och GPS-fusion | NotStarted |
| 15 | WiFi- och BLE-positionering | NotStarted |
| 16 | Lagerkarta och kartmatchning | NotStarted |
| 17 | Inspelning, simulering och jämförelse | NotStarted |
| 18 | Slutvalidering, säkerhet och publicering | NotStarted |

Iteration 01: se docs/iteration-01.md. Övriga iterationer har inte påbörjats.

Kontrollerad process: kör tools/Verify.ps1 och tools/Smoke-Test.ps1; dokumentera testresultat och manuella acceptanskriterier i iterationsrapporten. Uppdatera iterations.json i en granskbar Git-commit. Verified kräver datum och uppfyllda kriterier på Android och Windows; Released kräver separat publiceringsbevis. Ingen publik API-metod ändrar status.

Commitfältet pekar på implementationscommitten när den finns; en commit kan inte bädda in sin egen hash i en versionshanterad fil. Byggmetadata hämtar alltid den exakta byggda committen.
