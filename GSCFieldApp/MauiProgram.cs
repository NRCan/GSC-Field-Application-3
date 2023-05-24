﻿using GSCFieldApp.ViewModel;
using Microsoft.Extensions.Logging;

namespace GSCFieldApp;

public static class MauiProgram
{
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

		// Need to add these to actually create them on start
		//Singleton will be created once
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddSingleton<MainViewModel>();

		//Transient will be created/deleted each time
        builder.Services.AddTransient<DetailPage>();
        builder.Services.AddTransient<DetailVieModel>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
