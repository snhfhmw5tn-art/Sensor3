# Sensor 3
Read existing code before changes. Preserve working implementations and user changes.
Implement one iteration at a time. Build and test each iteration; document results and commit completed work. Never force push.
Use shared C# contracts, dependency injection, ILogger, configured algorithm parameters and safe defaults. Calculation libraries must not depend on native platforms or UI.
Do not present simulated data as measured data. Document scientific methods and limitations. Report uncertainty.
Use MSTest: TestMethod, TestThat_snake_case names, DataRow DisplayName, Assert.ThrowsExactly<T>; no DataTestMethod. Helper methods use PascalCase.
Follow IDE0007, IDE0017, IDE0018, IDE0020, IDE0022, IDE0031, IDE0038. Avoid duplication and incompatible API changes. Handle exceptions and network interruptions.
Never claim tests ran if they did not. Verified requires all acceptance criteria, including required hardware checks. Released is separate from Verified. Changes to iterations.json must follow documented verification.
Android and Windows only. iOS is paused. Browser clients must not collect motion sensors.
