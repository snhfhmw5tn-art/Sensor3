# Inbyggd stegsensor

Steg i StepSession kommer från en vald native StepDetector eller StepCounter. IMU används fortsatt för aktivitet, bärläge och riktning, men dess beräknade steghändelser adderas inte. Utan native stegsensor är stegkällan Unknown och inga beräknade ersättningssteg visas.

Inventering förväljer tillgänglig StepDetector, annars StepCounter, även när Android behöver aktivitetsbehörighet. Begär behörighet för valda sensorer och bekräfta Androids dialog. Start lägger till en tillgänglig native stegsensor när enbart andra sensorer har valts. När båda är valda används endast StepDetector för sessionstotalen; kumulativa OS-värden visas separat.

StepCounter etablerar en baslinje med första provet. Sessionen räknar följande differenser, inte telefonens historiska total. Nollställning, stopp och återstart etablerar en ny baslinje; steg innan första provet räknas därför inte. Minskande räknare innebär ny baslinje. Dubbletter, gamla prov och orimliga hopp ignoreras. Windows stegkategorier har separata baslinjer.

Native steg räknas även innan aktivitetsklassificeringen är stabil. Gång/löpning och oklassificerade steg visas separat. Operatörsdeklarerad truck blockerar sessionsteg men uppdaterar OS-baslinjen. Steglängd och sträcka är uppskattningar från konfigurerad gång-/löpningslängd. Batchade steg saknar individuella tider och flyttar därför inte XY. Enskilda native steg kräver tillförlitlig riktning för att flytta XY.

Androids StepDetector rapporterar ett prov per steg; StepCounter är kumulativ och kan leverera fördröjda prov. Båda behöver ACTIVITY_RECOGNITION på Android 10 eller senare. Se [Android motion sensors](https://developer.android.com/develop/sensors-and-location/sensors/sensors_motion).

Syntetiska inspelningar innehåller explicit märkta syntetiska OS-steghändelser. De kontrollerar pipeline och regressioner, inte den verkliga sensorns noggrannhet. Fysisk gångräkning mot oberoende facit återstår.

Validering: 220 MSTest-tester passerade, inklusive native räknarbaslinje, dubbletter, kombinerade sensorer, återstart, nollställning, truck, orimliga hopp, inget IMU-fallback och ingen uppfunnen batchtrajektoria.

Hela lösningen byggd med Visual Studio MSBuild: 0 varningar, 0 fel. API-/dashboard-smoketest passerat. Ny Android-debugapp installerad och startad på ansluten Zebra TC53; uppdaterad native-stegräknarvy synlig. Aktivitetsbehörighet var ännu inte beviljad vid installationen.
