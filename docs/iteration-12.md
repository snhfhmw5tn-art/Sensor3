# Iteration 12 – Human heading estimation

Version 0.12.0/build 12. Implemented. Nästa iteration 13 är godkänd; Verified/Released återstår.

## Implementation och forskning

Contracts/Heading.cs skiljer IDeviceOrientationEstimator, IHumanHeadingEstimator och IHumanHeadingSession. SensorFusion/MotionFusion implementerar enbart telefonpose. AdaptiveHumanHeadingEstimator ger personriktning, heuristisk osäkerhet och separata PCA/gyro-only/adaptiva jämförelser. Core/StepSession, native DI och HeadingDiagnostics kopplar den till verkligt native-dataflöde. En nyligen explicit startreferens kan sättas före första insamlingsstart; käll-/ramavbrott kräver ny bekräftelse när en gammal gait-alignment skulle återanvändas. Stegtotalen påverkas inte.

Aktiv research-baseline är dokumenterad i [RESEARCH.md](../RESEARCH.md). Varje gait-fönster omfattar ungefär två stegperioder, begränsat till 0.6–2 s. Horisontell kovarians ger PCA-axel; anisotropi måste vara minst 0.4. Tecknet på korrelationen med vertikal acceleration lärs vid initial rak gång i känd riktning. Denna experimentella gait-fasupplösning används för axelns 180°-ambiguity när korrelationen är minst 0.25. Svag/ändrad gait-fas lämnas osäker. Attituden måste vara trovärdig: detta löser inte felaktig world-frame.

HeldStable/Viewing använder försiktig gyroassistans med vikt 0.25 endast om gyroändringens tecken stämmer med gait-ändring och innovationen är under 30°. Pocket/HandSwinging förlitar sig på world-PCA/gait-fas. Unknown/HandRotating/Scanning sänker kvalitet och tillåter inga obevakade phone-yaw-svängar. Transition/PickingUp/PuttingAway/VerticalSwinging håller tidigare riktning med kvalitet 0 och ökad osäkerhet. Den hållna vinkeln är inte en tillförlitlig ny riktningsobservation.

Telefonens riktning härleds separat från transformerad device +Y-axel; personriktning är medurs från lokal +Y, `atan2(x,y)`. Gyro-only integrerar minus world-Z-rotation enligt den konventionen. Raw PCA-jämförelsen har egen kontinuitet och lånar inte aktiv modellens teckenval. Windows sensor-UTC används med bevarad TotalSeconds-upplösning, inte avrundning till millisekunder.

## Resultat

- MSTest: 186 passed, 0 failed, 0 skipped före slutbygge. Phone rotation och body turn: 5,10,15,20,30,45,60,90,180°. Kontrollerad rak gait med telefonrotation, motsatt phone/body-rotation, Pocket/HandSwinging, pickup samtidigt med sväng och återhämtning, S-kurva, 180°-ambiguity-jämförelse, saknad start, svag gait-fas och sensorlucka.
- Android + Windows VS MSBuild och browser-/metadata-smoke körs före commit; artifacts/iteration-12-*.
- Alla headingtestdata är uttryckligen syntetiska. Verkliga inspelningar med ground truth hittades inte i tillgängliga Sensor App 2-filer. Fysisk vinkel-/carry-/kurvvalidering på TC53/Windows: NotRun. Ingen verklig heading-/positionsnoggrannhet anges.

## Begränsningar

Gait-fas kan ändras mellan personer, tempo, gång/löpning och bärbyte. En dominant sidleds-/handrörelse kan ge fel axel trots hög anisotropi. Samtidig carry/turn kan vara oidentifierbar och ska då visa osäkerhet. Resultaten reproducerar inte artikelns tränade classifier/EKF eller dess publicerade precision. Ingen phone→human-riktning är garanterad genom en generell offset. Initial kalibrering måste göras under rak gång i den angivna riktningen; saknad absolut north-reference förblir lokal.

Commit/datum registreras i iterations.json efter bygge. Ingen skarp release.
