using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Sensor3;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
[IntentFilter([Android.Content.Intent.ActionView], Categories = [Android.Content.Intent.CategoryDefault, Android.Content.Intent.CategoryBrowsable], DataScheme = "sensor3", DataHost = "pair")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ReadPairingIntent(Intent);
    }
    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        ReadPairingIntent(intent);
    }
    private static void ReadPairingIntent(Android.Content.Intent? intent)
    {
        var value = intent?.DataString;
        if (value is null) return;
        try { Sensor3.Contracts.PairingLink.Parse(value); NativeDevicePairingClient.IncomingLink = value; }
        catch (ArgumentException) { }
    }
}

