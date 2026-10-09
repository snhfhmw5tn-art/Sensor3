# Iteration 06 – GPS, WiFi och Bluetooth

Version 0.6.0/build 6. Implemented; Verified och Released kräver separata bevis.

## Implementation

Contracts/RadioSensors.cs innehåller ILocationProvider, IWifiScanner, IBluetoothScanner, IStepSensorProvider och råa observationer. Sensors/RadioObservationRules.cs validerar plats, begränsar skanningstid och deduplicerar WiFi på BSSID, inte SSID. NativeStepSensorProvider använder samma inventering som IMU och tillverkar inga steg.

Mobile/NativeLocationProvider.cs använder MAUI:s native Geolocation på Android/Windows. Latitud, longitud, noggrannhet, höjd, hastighet, riktning och källtidsstämpel bevaras. Saknade värden är null/Unknown; OS-platsfix är inte garanterat satellit-GPS. Mock-källor märks uttryckligen.

Platforms/Android/AndroidRadioScanner.cs använder WifiManager och BluetoothLeScanner. Android 13+ använder WifiSsid; äldre Android använder den kompatibla äldre egenskapen. WiFi-begäranden begränsas till minst 35 s mellan anrop; OS kan ändå strypa skanning. Resultaten kan vara cache och deras native monotona mikrosekunder bevaras. BLE använder runtime-behörigheter, RSSI, adressidentifierare och rå annonsdata. BLE-adresser är inte stabila person-/enhetsidentiteter och kan rotera.

Platforms/Windows/WindowsRadioScanner.cs använder WiFiAdapter och BluetoothLEAdvertisementWatcher. WiFi/BSSID kräver Windows platsmedgivande. Frekvens normaliseras från kHz till MHz. BLE är passiv och råa annonssektioner exporteras som hex. Saknad BLE-adapter eller saknat LE-stöd ger Unsupported. Tomt skanningsresultat är inte bevis på saknad hårdvara.

RadioDiagnostics.razor ingår i Sensordiagnostik och visar rådata med explicita native-knappar för platsfix, WiFi, femsekunders BLE och avbrott. Browsern får inga egna providers. NativeObservationLifetime stoppar aktiva prov vid fokus/bakgrundsstopp; cancellation och uppdateringssessionslåset respekteras. Behörigheter begärs av OS och kan nekas; inga säkerhetsinställningar ändras automatiskt. BLE-skanning är 1–30 s och har högst 512 identifierare i minnet.

Manifest för Android/Windows, MauiProgram/App och tester ändrades. tools/Set-IterationVersion.ps1 synkroniserar appversion/build/testfixture utan att ändra OS-versioner eller NuGet-versioner. Inledande byggfel i versionhelper och en tvetydig ScanResult-typ korrigerades före godkänt slutbygge. Ingen positionsmodell från WiFi/RSSI införs i denna iteration.

## Resultat

- Android och Windows Debug via VS 2026 MSBuild: 0 fel, 0 varningar.
- MSTest: 124 passed, 0 failed, 0 skipped (7 nya plats-/radiofall).
- HTTP/metadata/browsergräns samt 32 distributions-/säkerhetskontroller passerade.
- TC53 ansluten via ADB, Android 14. OS-inventering bekräftar rörelsesensorer; detta är inte ett appbaserat GPS/WiFi/BLE-mätprov.
- Native platsfix, grant/deny, färskhet i WiFi-resultat, BLE-annonser och Windows radiofunktioner behöver manuell verifiering. Ingen skarp release publicerad.

Logg artifacts/iteration-06-build.log; tester artifacts/test-results/iteration-06.trx. Implementationscommit/datum registreras i iterations.json efter godkänt bygge. Fortsatt iteration 07 är redan godkänd av användaren.

## Primärkällor

[MAUI Geolocation](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device/geolocation?view=net-maui-10.0), [Android WiFi-skanning och begränsningar](https://developer.android.com/develop/connectivity/wifi/wifi-scan), [Windows WiFiAdapter och platsmedgivande](https://learn.microsoft.com/en-us/uwp/api/windows.devices.wifi.wifiadapter).
