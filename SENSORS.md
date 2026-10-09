# Sensorer och provenance

Android: verklig SensorManager-inventering av TYPE_ALL. Windows: Windows.Devices.Sensors-gränssnitt och faktiska enheter. Namn/vendor/native-ID, rapporteringsläge, enheter, status, source-timestamp, mottagningstid, kvalitet och koordinatram bevaras. Unsupported innebär att hårdvara/API saknas och ger inga mätvärden. Windows-värddatorn gav 0 tillgängliga sensorer i fysisk inventering; TC53 (Android14) gav 32 native-sensorer vid iteration6, inklusive accelerometer/gyro/gravity/linear/game rotation. Dessa upptäckter är inte bevis på algoritmnoggrannhet.

Samplingsönskemål 1–200Hz; faktisk frekvens räknas från source-tid. Diagnostik råvärden, EMA250ms (enbart diagnostik), max180 historikpunkter och ~10Hz grafer; alla giltiga prov räknas. Continuous tystnad blir Stale; event/on-change-tystnad är tillåten. Available/Running/Active skiljs. Stoppade/stale värden visas historiskt med status; saknade data blir Unknown.

## Algoritmernas verkliga källor

| Modul | Använda observationer | Begränsning |
|---|---|---|
| Fusion | accel/linear, gravity, gyro, rotation quaternion | gyro/pose kan saknas; yaw saknar ibland absolut referens |
| Gait/HAR | world-linear XYZ, gyroenergi, samplings-/gait-fönster | heuristik kan förväxla rytmisk handrörelse/gång |
| Carry | device/world accel, pose, gyro, proximity/light om faktiskt valda | scanning/mounted och andra priors är operatörsuppgifter |
| Human heading | world-horizontal PCA, vertikal gait-fas, begränsad gyroassistans | känd startriktning och tillförlitlig world-frame krävs |
| PDR | accepterade steg, amplitud, HAR/carry, heading vid stegtid | K kalibreras; utan heading hålls XY/osäkerhet växer |
| Truck | faktisk GPS lat/lon/accuracy/speed/course/time/mock + operatörskontext | IMU används inte för obekräftad trucktranslation |
| WiFi | native BSSID/RSSI/MHz/tid/cache | OS-throttling/cache, radiomiljö och kartkalibrering |
| BLE | faktisk annonsering/ID/RSSI/UTC + kända ankare | roterande ID, flerpaths; RSSI är inte exakt avstånd |
| Map matching | beräknad lokal delta/XY/uncertainty + importerad geometri | kartfel eller felaktig initial position kan förlora matchning |

Radioförgrundsprov begär OS-behörighet vid användarens val. Nekad/avstängd/throttlad/unsupported ger status, inga simulerade resultat. Senaste radiofix blir historisk med kvalitet0 efter freshness-gräns. Native step counter är absolut separat diagnostik och adderas aldrig till egen gait-total. Browser samlar inga motion/GPS/WiFi/BLE-sensorer. Simulation/input-stream är tydligt separat från native-hårdvara.
