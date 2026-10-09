# Iteration 16 – Indoor mapping and map matching

Version 0.16.0/build 16. Implemented; lager-/hårdvaruverifiering NotRun.

Contracts/IndoorMap: validerad geometri och lokal XY→lat/lon-kalibrering med origin och +Y-bäring. MapMatching/IndoorParticleFilter: 256 partiklar, rörelsedelta från position, processbrus, råpositionslikelihood, gång-/truckvägspreferens och resampling. Väggsegment och Rack/Blocked-rektanglar stoppar både slutpunkt och korsande rörelse. Anchor-punkter visas. Bästa giltiga partikel visas eftersom medelvärdet kan hamna inne i ett ställage. Förlorad matchning låses till Unknown och kräver bekräftad geometri/ny start, utan automatisk teleportering genom en vägg. Kvalitet är heuristisk.

LiveIndoorMap/NavigationView visar meterkoordinat, 1/5/10 m-gradering, knappzoom/panorering, Auto Follow, start/position, rå-/radiokorrigerat-/matchat spår, fart, heading, steg och osäkerhet. Spår är begränsade till 4000 punkter. Truckläge visar separat GPS-spår; PDR används inte som truckposition. Geometri importeras/exporteras via validerad JSON i aktuell vy, inga verkliga lagerobjekt antas. Geometrin måste importeras igen när vyn återskapas; radioankare lagras separat beständigt. Dashboardens navigation använder InteractiveServer och native samma Blazor-komponent.

Frivillig Google Maps Embed-vy kräver uttrycklig aktivering, API-nyckel och origin/bäring. Den visar kalibrerad geografisk position separat från lokal lagergeometri; inga oregistrerade bildöverlägg antas sammanfalla. Google-vyn uppdateras vid uttrycklig aktivering, lokal livekarta fortsätter i realtid. Ingen Google-nyckel finns eller sparas; skarp Google API-test NotRun. [Google Embed](https://developers.google.com/maps/documentation/embed/quickstart) dokumenterar nyckel/API-kraven.

Forskningsbakgrund: [Map-Based Indoor Pedestrian Navigation Using an Auxiliary Particle Filter](https://pmc.ncbi.nlm.nih.gov/articles/PMC6189818/). Den egna förenklade partikelfilterbaselinen är inte artikelns APF-implementation eller dess noggrannhet. Kartfel, dålig start eller divergerad PDR kan omöjliggöra matchning.

Tester: väggkorsning, ställagekorsning, tillåten gång, partikellikelihood, tom geometri och 90° geo-kalibrering. Razor SVG text-taggar gav ett tidigt kompileringsfel och korrigerades före slutbygget. Fysisk gång/truck längs lagergångar, rå-vs-matchad positionsnoggrannhet och Google-bakgrund: NotRun. Inga fältprecisioner anges.

Filer ovan, MapMatchingTests, Core-projektreferens, Dashboard Routes och versionsmetadata. Full Android/Windows MSTest/smoke före commit i artifacts/iteration-16-*. Commit/datum i iterations.json. Verified/Released återstår; nästa iteration 17 är godkänd.

Slutresultat: Android/Windows 0 fel/varningar; 203 MSTest passed, 0 failed/skipped; smoke passed.
