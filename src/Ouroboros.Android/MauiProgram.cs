using Microsoft.Extensions.Logging;
using Ouroboros.Android.Services;

namespace Ouroboros.Android;

public static class MauiProgram
{
	/// <summary>
	/// Ouroboros API base URL.  Override at build time or via app settings.
	/// When co-hosting the API via the CLI's <c>--serve</c> flag on the same
	/// device, use <c>http://10.0.2.2:5000</c> (Android emulator → host loopback).
	/// </summary>
	private const string DefaultApiUrl = "http://10.0.2.2:5000";

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// ── Ouroboros Web API client (co-hosting contract) ────────────────────
		// The Android app delegates all AI pipeline work to a running Ouroboros
		// API instance (standalone WebApi, CLI with --serve, or any reachable
		// host) via a typed HttpClient.  Change DefaultApiUrl to point at your
		// deployed API endpoint.
		builder.Services.AddHttpClient(OuroborosApiService.HttpClientName, client =>
		{
			client.BaseAddress = new Uri(DefaultApiUrl.TrimEnd('/') + "/");
			client.DefaultRequestHeaders.Add("Accept", "application/json");
			client.Timeout = TimeSpan.FromSeconds(120);
		});

		builder.Services.AddSingleton<OuroborosApiService>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
