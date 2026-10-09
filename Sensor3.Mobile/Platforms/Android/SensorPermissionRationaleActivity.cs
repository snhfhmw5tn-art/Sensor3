using Android.App;
using Android.OS;
using Android.Widget;

namespace Sensor3;

[Activity(Name = "se.qsys.sensor3.SensorPermissionRationaleActivity", Label = "Sensor 3 – sensorbehörigheter", Exported = true)]
[IntentFilter(["androidx.health.ACTION_SHOW_PERMISSIONS_RATIONALE"])]
public sealed class SensorPermissionRationaleActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var text = new TextView(this)
        {
            Text = "Sensor 3 läser endast sensorer som du väljer och startar i appen. Rörelse-, steg- och eventuella kroppssensorvärden används för lokal visning under mätningen. Värdena sparas inte och skickas inte till servern i denna version. Insamlingen stoppas när appen lämnar förgrunden eller när du trycker Stoppa. Behörigheter kan nekas eller återkallas i Androids inställningar. Appen läser eller skriver inte historiska data i Health Connect. Råvärden från kroppssensorer används inte för medicinska bedömningar.",
            TextSize = 18
        };
        text.SetPadding(24, 24, 24, 24);
        var scroll = new Android.Widget.ScrollView(this);
        scroll.AddView(text);
        SetContentView(scroll);
    }
}
