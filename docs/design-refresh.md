# Gemensam design för server och native-klient

Sensor 3 har en gemensam responsiv arbetsyta med mörk navigation, turkosa accenter, ljusa kort, systemtypografi och tydliga formulärkontroller. På större skärmar används en sidopanel; på telefoner en horisontell navigation. NavLink visar aktuell sida. Servern har även länkar till nedladdningar och releaseadministration.

Startsidan har genvägar till positionering, diagnostik och inspelning. Rörelsediagrammet är en uttryckligt märkt illustration, inte uppmätta sensordata. Version och iteration visas i sidhuvudet; commit och plattform i sidfoten. Klientens SVG-appikon, startbild och Androidfärger följer samma färgpalett.

Delade stilar omfattar diagnostikkort, tabeller, knappar, fält, statusmeddelanden och releasekort. Mobilvyn staplar definitionslistor och kort. Tabeller kan rullas separat. Kontroller har minst 44 px höjd, tangentbordsfokus är synligt och rörelseeffekter stängs av när användaren föredrar minskad rörelse. Native viewport tillåter zoom. Inga externa typsnitt eller bildtjänster krävs.

Validering: 225 befintliga MSTest-tester passerade. API-/dashboard-smoketest passerade. Serverlayout granskad vid 1280 px och 390 px; mobil About-vy hade bodyWidth 375 px vid viewport 390 px och tabellbredd 343 px, utan sidövergripande horisontellt överflöde. Skärmbild finns lokalt i artifacts/design-server.jpg.

Hela Android-/Windowslösningen byggd med 0 varningar och 0 fel. Android-debugappen installerad på ansluten Zebra TC53 och den nya startsidan/navigationen verifierad via UI och skärmbild. Efter kontrollen får klientens ContentPage SafeAreaEdges=All, så innehållet hålls utanför systemfält och tangentbord enligt [MAUI:s safe-area-API](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0). Native-projektets båda mål byggs om för denna justering.

Slutlig native-ombyggnad för Android och Windows efter SafeAreaEdges-ändringen: 0 fel och 0 varningar. Slutlig APK installerad på TC53 och skärmbild visar hela apphuvudet under systemets statusfält; bild artifacts/design-client.png. Inga produktionspaket publicerades.
