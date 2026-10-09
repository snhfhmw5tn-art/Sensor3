# Iteration 13 – PDR positioning and speed

Version 0.13.0/build 13. Implemented; fysisk verifiering återstår.

Positioning/PedestrianPositionEstimator, Contracts/Positioning, Core/StepSession, native DI och SharedUI/Navigation kopplar accepterade gait-steg till lokal XY. Startpunkt är uttrycklig operatörsuppgift. Riktningen hämtas vid stegets ursprungliga tidpunkt, inte från en senare telefonpose. Gång/löpning hålls separat; native stegtotal adderas aldrig. Truck-kontext spärrar gait i befintligt HAR. UI samlar sensorer på samma sida och stänger dem vid navigering.

Steglängd: K × amplitud^¼, gång K=0.43, löpning K=0.65, konfigurerbar personskala och gränser 0.25–1.2 respektive 0.45–2 m. K måste kalibreras. Handacceleration dubbelintegreras inte. Saknad riktning ger bibehållen XY, växande heuristisk osäkerhet, kvalitet 0. Ingen start ger Unknown position. Hastighet härleds från steglängd/intervall; avbrott ger Unknown.

Metoden inspireras av [Weinberg AN602](https://www.analog.com/media/en/technical-documentation/application-notes/513772624an602.pdf). [AN900](https://www.analog.com/media/en/technical-documentation/application-notes/47076299220991AN_900.pdf) beskriver person-/tempoberoende och begränsad reproducerbarhet. Detta är en konfigurerbar baseline, inte publicerad eller uppmätt precision.

Tester: kontrollerade syntetiska 10/20/40/100 m, 90° sväng, löpning, duplicerat steg, saknad heading och avbrott. Dessa testar matematik, inte verklig distansnoggrannhet. Full Android-/Windows-bygglogg och MSTest i artifacts/iteration-13-*. Manuella banor med ground truth, olika bärrörelser och telefonen i verklig truck: NotRun. Browser visar ännu rå fjärrdiagnostik; fjärrberäknad navigation återstår i slutlig integration.

Resultat kompletteras efter bygge. Commit/datum i iterations.json. Ingen skarp release. Nästa iteration 14 är godkänd.

Slutresultat: 192 tester godkända; Android och Windows full lösning 0 fel/0 varningar; API/dashboard-smoke godkänd.
