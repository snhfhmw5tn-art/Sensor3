# Forskningsunderlag och aktiv implementation

## Personriktning

[Deng et al. 2016, Carrying Position Independent User Heading Estimation](https://pmc.ncbi.nlm.nih.gov/articles/PMC4883368/) använder igenkänd bärposition för metodval, med attitude/offset för stabil placering och RMPCA för ficka/pendlande hand. Studien behöver initial riktningsinformation och behandlar övergångar separat från svängar; samtidig övergång och sväng ligger utanför dess centrala antagande. Artikelns tränade classifier, EKF och noggrannhetsresultat är inte implementerade eller validerade resultat för Sensor3. Fulltext lästes från offentlig Europe PMC XML, med huvudkälla ovan.

Sensor3:s aktiva modell är `sensor3-world-pca-gait-phase-v1`, en egen research-baseline. Den transformerar varje native acceleration till aktuell värld-/nivåram, använder ett kadensanpassat gait-fönster, PCA-anisotropi och kalibrerat tecken på kopplingen mellan vertikal gait-fas och horisontell huvudaxel. Stabila bärlägen tillåter begränsad gyroassistans bara när den stöds av gait-riktningen. Dynamiska/okända lägen använder accelerationsevidens med lägre kvalitet. Byte/lyft eller svag evidens ger Pending/Unknown. Separat gyro-only och kontinuitetsupplöst PCA är jämförelsebaselines och styr inte automatiskt personpositionen.

| Metod | Evidens | Betydande begränsning |
|---|---|---|
| DeviceOrientation | Native quaternion eller gyro/gravity | Telefonpose; yaw kan driva och är inte kroppsriktning |
| Gyro-only research | Integrerad världsgyro från angiven start | Handrotation kan ge felaktig människosväng |
| PCA research | Horisontell kovarians och kontinuitet | Axel med 180°-ambiguity; kan följa sidled/handrörelse |
| Aktiv adaptiv baseline | PCA, initial gait-fas, bärrisk, försiktig gyroassistans | Kräver stabil gait-fas/attitude; ingen garanti för fri hand eller verklig 180°-vändning |

Ingen mätbaserad headingnoggrannhet har bestämts. Kontrollerade syntetiska tester verifierar kod/invarianter under sina angivna antaganden. En PCA-dominerande lateral rörelse eller ändrad gait-fas kan ge stor riktningsbias. Native quaternion behöver fysisk axel-/yaw-validering, och kalibreringsriktningen kräver initial rak gång. Efter avbrott måste startreferensen bekräftas. Heuristisk osäkerhet är inte ett statistiskt konfidensintervall.

## Aktivitet och steg

Stegdetektering porterades från verklig Sensor App 2-kod; se [iteration 08](docs/iteration-08.md). Aktivitetsmodulen använder reproducerbara fönsterfeatures och tillståndsövergångar. [UCI HAR](https://archive.ics.uci.edu/dataset/240/human+activity+recognition+using+smartphones) och [Shoaib et al. 2014](https://www.mdpi.com/1424-8220/14/6/10146) användes för metodundersökning. Inga tränade modeller eller påståenden om deras publicerade noggrannhet överförs till projektet.

## Bevisnivå

Implemented betyder att kod, bygge och angivna regressionstester finns. Verified kräver acceptanskriterier på verkliga Android-/Windows-enheter, med märkt ground truth. Released kräver separat signering, installations-/uppgraderingstest och kontrollerad publicering. Simulering tillhör aldrig fältverifiering. Alla dokumenterade standardparametrar är experimentella och måste kalibreras mot det aktuella användningsfallet.
