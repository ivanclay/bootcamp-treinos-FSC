using Android.App;
using Android.Content;
using Android.Content.PM;

namespace FitAi.App;

/// <summary>Recebe o retorno do login (fitai://auth?code=...) e entrega ao WebAuthenticator.</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = Services.AppConfig.CallbackScheme)]
public class WebAuthenticatorCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
