# Algoritmer

Alla parametrar är research-baselines, inte statistiskt validerade sannolikheter. Ändringar behöver ground-truth-data per telefon/person/lager. Rena bibliotek refererar Contracts och inga native/UI-paket.

| Kedja | Aktiv modell/parametrar | Kvalitet och fail-safe |
|---|---|---|
| Steg | Sensor App2-inspirerade peaks, refractory .32s, minperiod .26s, amplitud/prominence1.1, minst3 regelbundna kandidater, autocorrelation resamplad50Hz | raw source återanvändning förhindras, nativecounterseparat, rotation/vertikala lyft avvisas; pendlande arm med hög gyro kan missas |
| Fusion | native quaternion om färsk ≤.2s, annars gyro/gravity tilt; gravity subtraction + world-transform; EMA .04s för beräkning | ingen pose/gyro blir Unknown eller lägre kvalitet; Windows rawtecken bevaras utanför fusion |
| Bias/noise | explicit stillhetskalibrering ≥20 samples/≥.6s | långsam verklig rotation kan förväxlas med bias |
| HAR | 2s gait features/periodicity, RMS/amp/energi, .75s candidate-hysteresis | minimum state1s; truck är deklarerad prior, GPS ensam identifierar inte truck |
| Carry | 2s/50%-överlap, extrema/cross-window och tilt/gyro, proximity/light om verkliga | transition fryser pendingsteg/riktning; scores är heuristiska, inte classifiertraining |
| Human heading | PCA + initial gait-fastecken i ungefär2 stegperioder/.6–2s, anisotropy≥.4, phasecorr≥.25, stabilcarry gyroassist .25 | handrotation utan gaitändring styr inte personriktning; Unknown håller gammal vinkel med kvalitet0 |
| PDR | K × amplitud^¼, K .43 walk/.65 run, personscale; .25–1.2/.45–2m | XY explicit start, heading vid stegets tid; saknad heading skapar ingen translation. Stegfart hålls inte som aktuell vid stopp/Unknown |
| Truck | GPS-gate: age≤5s, accuracy≤20m, max15m/s, course vid ≥.5m/s, spatial innovation/jitter | hand-IMU dubbelintegreras inte. GPS-gap skapar ingen brosträcka; senaste XY är historisk |
| WiFi | WKNN3, ≥3 AP samma frekvens, RMS-RSSI + saknad-AP-penalty, quality/(1+error)^2 | cached/old/outlierUnknown; minimum heuristic2m; fysisk felradie inte verifierad |
| BLE | tydligt starkast känt ankare, minst6dB separation, färsk observation | operatorns uncertainty1–100m; ingen RSSI-ranging |
| Radio fusion | ≤.5 innovation-weight, jump-gate max(20m,PDR+radio uncertainty) | rawposition/rawpath och stegdistanser bevaras |
| Kartmatchning | 256partiklar, lokalt delta + .1m processnoise, rå-/gånglikelihood, resampling | korsande vägg/rack/blocked förkastas. Förlorad matchning låses Unknown; ny bekräftelse behövs |

HAR state-hysteresis är .75s transition och minimum1s state. Klassifikation/pipeline kan avvisa legitim gång i stark handrotation; se syntetiska jämförelser och KNOWN_LIMITATIONS. Position, fart, distans och heading har olika provenance; en held historisk position är inte en aktuell tillförlitlig fix. Google-ram är explicit lokal XY-origin och +Y-bäring→lokal öst/nord→lat/lon.

Simulation går genom samma Core/AnalysisSession. Ground truth är separat och aldrig hämtad från modellens eget resultat. Export bevarar source-klocka, operator-context och initial radio/GPS-referens. Syntetiska tester kan validera aritmetik/livscykel men bevisar inte robust verklig gait eller positionsnoggrannhet.
