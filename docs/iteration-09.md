# Iteration 09 – Sensor fusion

Version 0.9.0/build 9. Implemented. Fältverifiering återstår; nästa iteration 10 är godkänd.

## Matematik och implementation

Contracts/Motion.cs innehåller double-precision Vector3D/QuaternionD, Hamiltonprodukt, normalisering, konjugat, axis-angle, antiparallell vektoralignment och 3×3 rotationsmatris. QuaternionD är `(x,y,z,w)`; vektorer transformeras aktivt `v_world = q * (v_device,0) * conjugate(q)`. Matrisen använder kolonnvektorer och ger samma resultat. Positiv rotation är högerhandsregeln. Enheter är m/s², rad/s, sekunder och meter. Displayrotation påverkar inte fysisk sensorreferensram.

Androids rotation-vector är native sensorfusion; vanlig world-frame är öst/nord/upp medan game-vector har godtycklig/drivande yaw. Windows orientation har native referensram och saknar ännu fysisk geografisk kalibrering. Katalogens valda sensor avgör vilken native variant som används. Native diagnostik bevarar råa tecken. Fusionen korrigerar Windows accelerometer/gravity/linear acceleration:s G-force-tecken för gemensam uppåtriktad specific force; Android är +g vid flat face-up och Windows -g. Världsramen namnges uttryckligen och används inte automatiskt som personriktning.

SensorFusion/MotionFusion.cs föredrar native quaternion när färsk. Utan quaternion initialiseras roll/pitch genom gravity-alignment; yaw är lokal och oankrad. Gyro integreras med `q_new = q * exp(omega_corrected * dt/2)` när native pose är för gammal. Gravity ger långsam tilt-korrektion utan att påstå absolut yaw. Saknad gyro ger enbart gravity-tilt. Långa luckor återinitialiserar evidence; yaw kan byta lokal referens och får inte användas som säker människosväng.

Gravity kommer från tidsmatchad native gravity, annars accelerometerns 0.3 Hz långsamma lågpass eller aktuell quaternion. Linear acceleration föredras när vald; annars subtraheras gravity från accelerometer. Världsacceleration filtreras separat med tidskonstant 0.04 s. Detta algoritmfilter ersätter inte diagnostikens rådata eller dess 0.25 s filter. Ingen dubbelintegrering av handacceleration införs.

TimeAlignedVectorBuffer.cs sparar högst 64 prover, avvisar bakåt-/dubbel tidsstämpel, interpolerar mellan närliggande vektorprover och avvisar extrapolation äldre än 0.2 s. Native quaternion använder senaste färska pose; ingen full quaternion-tidsresampling eller klocksynk mellan enheter påstås. Accelerometer och linear acceleration har separata ordningskontroller så att lika tidsstämplar inte slänger linear-sensorn.

Gyrobias kräver användarens uttryckliga kalibreringsbegäran, minst 20 lugna prov och 0.6 s låg acceleration/gyro. Rörelse bryter kalibreringsfönstret. Telefonen måste verkligen ligga stilla: mycket långsam verklig rotation kan inte säkert skiljas från bias med detta underlag. Gyro-RMS-brus och accelerationsbrus skattas från lugna prov; värden är inte fabrikantens brusdensitet eller statistiskt kalibrerad osäkerhet.

Core/StepSession.cs använder samma fusion för varje relevant native-prov och presenterar ISensorFusionSession. Step-kvalitet begränsas när gyro saknas. FusionDiagnostics visar pose, raw/filtrerad världsrörelse, gyrobias/brus och Unknown vid saknad gyro. Native DI och SharedUI-diagnostik uppdaterades. DeviceOrientation och DeviceMotion är separata; HumanHeading/HumanPosition tillkommer i senare separata moduler och genereras inte av telefonrotation.

## Resultat

- MSTest före slutbygge: 151 passed, 0 failed, 0 skipped. X/Y/Z-rotationer, matris/kvaternion/invers, antiparallell gravity, invalid quaternion, interpolation/åldersgräns, Android-/Windows-tecken, tilted stillastående telefon, uttrycklig bias-kalibrering och samtidiga accel/linear-tidsstämplar. Alla tidigare steg-/HTTPS-tester ingår.
- Android + Windows VS MSBuild, browser-/metadata-smoke körs före commit och loggas i artifacts/iteration-09-*.
- Fysisk sensoraxel-/yaw-kalibrering på TC53 och Windows sensorenhet, brus vid drift och bias under temperaturförändring: NotRun. Ingen positioneringsnoggrannhet eller release påstås.

Commit/datum registreras i iterations.json. Primärkällor: [Android motion sensors](https://developer.android.com/develop/sensors-and-location/sensors/sensors_motion), [Windows sensor axes](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/sensor-orientation), [Windows accelerometer-tecken](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/sensors). Aktiv fusion är en dokumenterad enkel komplementär baseline, inte en reproduktion av EKF/Madgwick eller en bevisad navigationslösning.
