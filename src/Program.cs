﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

// sudo apt-get install libasound2-dev
// dotnet add package Alsa.Net
// sudo usermod -aG audio $USER

class Program
{
    static async Task Main(string[] args)
    {
        // 创建 audio 保存文件夹
        if (!Directory.Exists("audio"))
        {
            Directory.CreateDirectory("audio");
        }

        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<RealtimeAPIOptions>(context.Configuration.GetSection("RealtimeAPI"));
                services.Configure<SessionUpdateOptions>(context.Configuration.GetSection("SessionUpdate"));
                services.Configure<AudioSettings>(context.Configuration.GetSection("AudioSettings"));
                services.Configure<LcdSettings>(context.Configuration.GetSection("LcdSettings"));
                services.AddSingleton<AudioService>();
                services.AddSingleton<WebSocketService>();
                services.AddSingleton<LcdService>();
                services.AddHostedService<Worker>();
            });
}