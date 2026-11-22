using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace Reborn.Droid;

[Activity(
    MainLauncher = true,
    ConfigurationChanges = global::Uno.UI.ActivityHelper.AllConfigChanges,
    WindowSoftInputMode = SoftInput.AdjustNothing | SoftInput.StateHidden,
    LaunchMode = LaunchMode.SingleTask,
    Exported = true
)]
public class MainActivity : Uno.UI.Runtime.Android.ApplicationActivity
{
    const int PermissionRequest = 42;
    static TaskCompletionSource<bool>? _permissionTcs;

    public static MainActivity? Instance { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        global::AndroidX.Core.SplashScreen.SplashScreen.InstallSplashScreen(this);
        Instance = this;
        base.OnCreate(savedInstanceState);
        VolumeControlStream = Android.Media.Stream.Music;
    }

    protected override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    static string AudioPermission => Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu
        ? Manifest.Permission.ReadMediaAudio
        : Manifest.Permission.ReadExternalStorage;

    public static bool HasAudioPermission =>
        Android.App.Application.Context.CheckSelfPermission(AudioPermission) == Permission.Granted;

    /// <summary>Asks for the music (and, on Android 13+, notification) permission. True when music can be read.</summary>
    public static Task<bool> RequestPermissionsAsync(bool openSettingsIfBlocked = false)
    {
        if (HasAudioPermission) return Task.FromResult(true);
        var activity = Instance;
        if (activity == null) return Task.FromResult(false);

        // Denied twice: Android no longer shows the dialog, only the app's system settings can grant it.
        var prefs = activity.GetSharedPreferences("permissions", Android.Content.FileCreationMode.Private)!;
        bool askedBefore = prefs.GetBoolean("asked", false);
        if (openSettingsIfBlocked && askedBefore && !activity.ShouldShowRequestPermissionRationale(AudioPermission))
        {
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,
                Android.Net.Uri.FromParts("package", activity.PackageName, null));
            activity.StartActivity(intent);
            return Task.FromResult(false);
        }
        prefs.Edit()!.PutBoolean("asked", true)!.Apply();

        var wanted = new List<string> { AudioPermission };
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) wanted.Add(Manifest.Permission.PostNotifications);

        _permissionTcs?.TrySetResult(HasAudioPermission);
        _permissionTcs = new TaskCompletionSource<bool>();
        activity.RequestPermissions(wanted.ToArray(), PermissionRequest);
        return _permissionTcs.Task;
    }

    /// <summary>Dark status/navigation bar icons for the light theme, light icons for the dark one.</summary>
    public static void SetLightSystemBars(bool light)
    {
        var window = Instance?.Window;
        if (window == null) return;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var mask = (int)(WindowInsetsControllerAppearance.LightStatusBars | WindowInsetsControllerAppearance.LightNavigationBars);
                window.InsetsController?.SetSystemBarsAppearance(light ? mask : 0, mask);
            }
            else
            {
#pragma warning disable CA1422
                var flags = (SystemUiFlags)window.DecorView.SystemUiFlags;
                var bits = SystemUiFlags.LightStatusBar | SystemUiFlags.LightNavigationBar;
                window.DecorView.SystemUiFlags = (SystemUiFlags)(light ? flags | bits : flags & ~bits);
#pragma warning restore CA1422
            }
        }
        catch { }
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Back from the system settings with access granted: scan.
        if (HasAudioPermission && App.Library.NoAccess) _ = App.Library.ScanAsync();
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == PermissionRequest) _permissionTcs?.TrySetResult(HasAudioPermission);
    }
}
