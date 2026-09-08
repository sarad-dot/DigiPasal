using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using Java.Util.Concurrent;

namespace DigiPasal;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static MainActivity? _instance;

    public static MainActivity? Instance => _instance;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _instance = this;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _instance = null;
    }

    public async Task<bool> AuthenticateWithDeviceLockAsync(string title = "Verify Identity", string subtitle = "Use your device lock screen")
    {
        var keyguardManager = (KeyguardManager?)GetSystemService(KeyguardService);
        if (keyguardManager == null || !keyguardManager.IsKeyguardSecure)
            return false;

        IExecutor executor = Build.VERSION.SdkInt >= BuildVersionCodes.P
            ? ContextCompat.GetMainExecutor(this) ?? Executors.NewSingleThreadExecutor()
            : Executors.NewSingleThreadExecutor();

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            var promptInfo = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle(title)
                .SetSubtitle(subtitle)
                .SetDescription("PIN, Pattern, or Fingerprint")
                .SetNegativeButtonText("Cancel")
                .Build();

            var callback = new BiometricCallback(tcs);
            var biometricPrompt = new BiometricPrompt(this, executor, callback);
            biometricPrompt.Authenticate(promptInfo);
        });

        return await tcs.Task;
    }

    public bool HasDeviceLockScreen()
    {
        try
        {
            var keyguardManager = (KeyguardManager?)GetSystemService(KeyguardService);
            return keyguardManager != null && keyguardManager.IsKeyguardSecure;
        }
        catch
        {
            return false;
        }
    }
}

public class BiometricCallback : BiometricPrompt.AuthenticationCallback
{
    private readonly TaskCompletionSource<bool> _tcs;

    public BiometricCallback(TaskCompletionSource<bool> tcs)
    {
        _tcs = tcs;
    }

    public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult? result)
    {
        _tcs.TrySetResult(true);
    }

    public override void OnAuthenticationFailed()
    {
    }

    public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
    {
        System.Diagnostics.Debug.WriteLine($"Biometric authentication error {errorCode}");
        _tcs.TrySetResult(false);
    }
}