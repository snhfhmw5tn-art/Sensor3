# Iteration 08 – Step counter

Version 0.8.0/build 8. Implemented; Verified/Released återstår. Nästa iteration 09 är godkänd av användaren.

## Återanvändning och filer

Verklig Sensor App 2-källa hittades i det lokala Qsys-Sensor-app-2-repot, commit `c754d9b0183fe9ebcadad845e9e50f4b00ce23b0`. Läsningen omfattade client/walking.js ImmediateStepDetector/GaitStepDetector, client/pipeline.js MotionFeatureExtractor/StepValidator, shared/config.js och tests/walking.test.js. Ingen browser-sensorinsamling återanvänds. C#-porten ligger i Sensor3.StepDetection/GaitStepDetector.cs med fristående kontrakt i Contracts/Steps.cs. Den enklare StepDetector i Qsys-sensor-app lästes också, men den starkare App 2-logiken valdes.

Tre toppar med regelbundna intervall, resamplad autocorrelation vid 50 Hz, minst 15 Hz källdata, prominens, minst 0.06 s uppgång, orienterings-/gravitationsunderlag och horisontell energi krävs. Periodiska vertikala lyft utan horisontell rörelse avvisas. Gyrogränsen normaliseras från App 2:s 100 grader/s till rad/s. Nästan lika autocorrelation-toppar föredrar den kortare fördröjningen för att minska kadensens halvering; flyttalstolerans 1 ns används vid 0.32/0.06 s gränser. Parametrar finns i StepOptions och valideras. Löpning är ännu en experimentell kadens-/energiregel, inte tränad aktivitetsklassificering.

Core/StepSession.cs kopplar native-rådata till modulen via ISensorProvider, väljer en källa per sensortyp och föredrar linear acceleration när vald. Gravity eller långsam accelerometeruppskattning ger vertikalkomponenten; gyro/gravity måste tidsmässigt matcha inom 0.2 s. Utan matchande gravity blir kvalitet Unknown/0 och inga steg skapas. Full världstransformation införs i iteration 09. Bekräftade steg räknas exakt en gång även när startup återför tre kandidater. Configure/reset-evidence vid källbyte ändrar inte totalt antal steg. Explicit sessionsreset är enda räknarnollställningen.

SharedUI/Components/StepDiagnostics.razor i native MainLayout visar total, gång, löpning, steg/min, kadens Hz, senaste uppskattade steglängd, beräknad sträcka och kvalitet på alla sidor. Androids native kumulativa counter visas separat som total sedan enhetens start; den adderas aldrig till algoritmens steg. Kvarvarande antal visas när insamling stoppas. Native DI och diagnostikens Start konfigurerar räknaren. Webbläsaren får ingen native räknartjänst.

## Resultat och kontroll

- MSTest: 141 passed, 0 failed, 0 skipped. Gång vid 2 Hz, löpning 3 Hz, vertikala telefonlyft, hög rotationshastighet, saknad orientation, enstaka spike, stillastående rotation, dubbel tidsstämpel, sensorlucka, sessionsräkning och native jämförelse.
- Android + Windows Debug VS MSBuild och metadata-/browser-smoke körs före commit; loggar artifacts/iteration-08-*.
- Fysiska prov normal gång/löpning, pendlande arm, ficka, stående skanning, truckvibration, stopp/start: NotRun. Syntetiska sinusfall är algoritmregression, inte påstådd noggrannhet på TC53.
- Steglängd är konfigurerad uppskattning 0.7 m gång/1 m löpning. Ingen personkalibrering eller bevisad fysisk sträcka. Gång kan inte säkert skiljas från alla imiterade handrörelser med IMU. Långa luckor tar bort evidence, och bärpositionsklassificering införs i iteration 11 utan räknarreset.

Commit/datum registreras i iterations.json efter bygge. Ingen skarp publicering. Källa för återanvändning: [Sensor App 2](https://github.com/snhfhmw5tn-art/Qsys-Sensor-app-2); exakt lokal commit och filer ovan.
