# Iteration 10 – Activity recognition

Version 0.10.0/build 10. Implemented; nästa iteration 11 är godkänd. Klassificeringen är en reproducerbar research-baseline, inte en tränad eller fältvaliderad modell.

Contracts/Activity.cs definierar Stationary, Walking, Running, Forklift och Unknown samt IActivityClassifier/IActivitySession. ActivityRecognition/BaselineActivityClassifier.cs använder fönstrets accelerations-RMS/energi, varians, amplitud, horisontell energi, gyro-RMS, resamplad autocorrelation och gait-frekvens. Dessa features är separata från orientering/position. ActivityOptions ger validerade parametrar. IActivityClassifier kan ersättas genom DI av en senare tränad modell utan att byta sensor- eller UI-kontrakt.

Kandidat måste vara stabil i 0.75 s; minsta tillståndstid är 1 s. För lite data, under 15 Hz, icke-ändliga features, tidslucka eller saknad orientering ger Unknown. Osäkra hand-/fordonsrörelser klassas inte automatiskt som truck. Trusted GPS-fart utan gångevidens antyder fordon men är fortfarande Unknown för trucktyp. Forklift kräver operatörens deklarerade truckprior och märks uttryckligen i förklaringen; detta är ingen IMU-tränad klassificeringsprestation.

Core/StepSession.cs driver feature-extraktion och aktivitet med samma native tidsstämpel. Kandidatsteg buffras kort under startup tills HAR blir stabil Walking/Running, därefter räknas varje bekräftat steg en gång. Byte till osäker/annan kandidat tar bort pending-evidence. Truckprior stoppar gångsteg direkt och ändrar inte tidigare total. Rörelse-/sensoravbrott återställer aktivitetens evidence. Sessionsreset och operatörsval är separata från bärposition.

ActivityDiagnostics visar tillstånd, kandidat, stabilitet, heuristisk kvalitet, förklaring och forskningsfeatures. Operatörsvalet återspeglar sessionens faktiska deklaration även efter sidbyte. Native DI och Core-projektreferenser uppdaterades. Browser saknar native HAR-tjänst.

## Resultat och begränsningar

- MSTest: 155 passed, 0 failed, 0 skipped. Stillastående/gång/löpning, saknat/långsamt/oregelbundet data, NaN, övergångsfördröjning, tidslucka, GPS utan påhittad trucktyp, uttrycklig truckprior och bibehållen stegtotal utan nya gångsteg i truckläge.
- Android + Windows VS MSBuild och HTTP metadata/browser-smoke körs före commit; artifacts/iteration-10-* innehåller resultat.
- Verkliga klassificeringsprov, confusion matrix, precision/recall mellan personer, sensorer, bärpositioner och trucktyper är NotRun. Ingen classifier-procent för verklig noggrannhet påstås. Kvalitetspoängen är inte kalibrerad sannolikhet.
- UCI HAR:s waist-mount-data stödjer metodundersökning men innehåller inte projektets fria hand-/truckscenarier. Inget dataset/model tränades eller importerades i denna iteration. Fick-/handvariation, gyrobrist och vibrationsmönster kräver separat insamling och validering.

Commit/datum: iterations.json efter bygge. Ingen skarp release. Primärkällor: [UCI HAR dataset](https://archive.ics.uci.edu/dataset/240/human+activity+recognition+using+smartphones), [Fusion of Smartphone Motion Sensors for Physical Activity Recognition](https://www.mdpi.com/1424-8220/14/6/10146). Fönster-/feature-metoden är forskningsinformerad; trösklarna är experimentella defaults, inte återgivna universella forskningskonstanter.
