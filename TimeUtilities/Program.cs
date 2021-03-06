using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TimeUtilities.Services;
using BlazorUtils.JsInterop;
using BlazorUtils.Firebase;
using Microsoft.AspNetCore.Components.Authorization;

namespace TimeUtilities
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // Add root component
            builder.RootComponents.Add<App>("app");

            // Add services
            builder.Services.AddTransient(sp => new HttpClient {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            });

            // Jsinterop service
            builder.Services.AddSingleton<IJsInteropService, JsInteropService>();

            // Storage service
            builder.Services.AddSingleton<IStorageService, StorageService>();

            // Services for enabling Authentication
            builder.Services.AddOptions();
            builder.Services.AddAuthorizationCore();

            // Firebase auth
            builder.Services.AddSingleton<FirebaseGoogleAuthService>();
            builder.Services.AddScoped<IFirebaseGoogleAuthService>(
                provider => provider.GetRequiredService<FirebaseGoogleAuthService>());
            builder.Services.AddScoped<AuthenticationStateProvider>(
                provider => provider.GetRequiredService<FirebaseGoogleAuthService>());

            // Firestore service
            builder.Services.AddSingleton<IFirestoreService, FirestoreService>();

            // Run the app
            await builder.Build().RunAsync();
        }
    }
}
