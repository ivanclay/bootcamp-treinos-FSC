using Android.App;
using Android.Runtime;

namespace FitAi.App;

#if DEBUG
// Debug: permite HTTP sem TLS para falar com a API local (http://10.0.2.2:8080). Release exige HTTPS.
[Application(UsesCleartextTraffic = true)]
#else
[Application(UsesCleartextTraffic = false)]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
