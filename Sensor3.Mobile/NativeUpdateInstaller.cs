using Sensor3.Contracts;

namespace Sensor3;

public sealed class NativeUpdateInstaller : IUpdateInstaller
{
    public Task StartAsync(string packagePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
#if ANDROID
            var context = Android.App.Application.Context;
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && !context.PackageManager!.CanRequestPackageInstalls())
            {
                var settings = new Android.Content.Intent(Android.Provider.Settings.ActionManageUnknownAppSources,
                    Android.Net.Uri.Parse("package:" + context.PackageName));
                settings.AddFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(settings);
                throw new InvalidOperationException("Tillåt installation från Sensor 3 i Androids inställningar och välj Installera igen.");
            }
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, context.PackageName + ".updates", new Java.IO.File(packagePath));
            var intent = new Android.Content.Intent(Android.Content.Intent.ActionView);
            intent.SetDataAndType(uri, "application/vnd.android.package-archive");
            intent.AddFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.GrantReadUriPermission);
            context.StartActivity(intent);
            await Task.CompletedTask;
#else
            if (!await Launcher.Default.OpenAsync(new OpenFileRequest("Installera Sensor 3", new ReadOnlyFile(packagePath))))
                throw new InvalidOperationException("Windows App Installer kunde inte öppnas.");
#endif
        });
    }
}
