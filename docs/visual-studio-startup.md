# F5 i Visual Studio

Startbara projekt: Sensor3.Api, Sensor3.Dashboard och Sensor3.Mobile. Beräkningsbibliotek och testprojekt ingår i bygget men har ingen egen applikationsstart.

Sensor3.slnLaunch innehåller den delade profilen Sensor 3 - alla startprojekt, med Start för alla tre i ordningen API, Dashboard, Mobile. Visual Studio använder klientens valda debugmål (Windows Machine eller vald Android-enhet); valet av enhet ändras inte av lösningens startprojekt.

I den redan öppna Visual Studio 18-instansen sattes SolutionBuild.StartupProjects för Sensor3.sln till alla tre projekt och lästes tillbaka för verifiering. Inställningen gäller nu för F5. På andra datorer kan den delade profilen väljas i Visual Studios startmeny; aktivera Multi-Project Launch Profiles om den inte visas.

API: http://localhost:5301, HTTPS-profil även https://localhost:7223. Dashboard: http://localhost:5302, HTTPS-profil även https://localhost:7166. Endast Dashboard öppnar webbläsaren automatiskt. Stäng separat körda utvecklingsservrar på samma portar före F5.

Startprofil och launchSettings JSON har parsats och alla projektvägar kontrollerats. Inställningen verifierades via Visual Studios automation; F5-debuggning startades inte automatiskt. HTTPS-certifikat, enhetstoken och produktionens prototyp.qsys.se konfigureras separat enligt DEPLOYMENT.md.

Referenser: [Visual Studio multi-project launch profiles](https://learn.microsoft.com/en-us/visualstudio/ide/how-to-set-multiple-startup-projects) och [SolutionBuild.StartupProjects](https://learn.microsoft.com/en-us/dotnet/api/envdte.solutionbuild.startupprojects).
